using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Styles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxListRestart(int Id, int Abstract, int Level, int Start);

public sealed class DocxListMap
{
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _names = [];
    private readonly Dictionary<string, int> _abstracts = new(StringComparer.Ordinal);
    private readonly Dictionary<int, (int Level, int Start)> _starts = [];
    private readonly List<DocxListRestart> _restarts = [];
    private readonly Dictionary<int, string> _threads = [];

    private const string ThreadSeparator = "\u0001";

    public IReadOnlyList<DocxListRestart> Restarts => _restarts;

    public void Bind(string name, int id)
    {
        _ids[name] = id;
        _names[id] = name;
    }

    public void Bind(string name, int id, int abstractId)
    {
        Bind(name, id);
        _abstracts[name] = abstractId;
    }

    public int? Restart(string name, int level, int start) => Restart(name, null, level, start);

    public int? Restart(string name, string? thread, int level, int start)
    {
        if (!_abstracts.TryGetValue(name, out var abstractId)) return IdOf(name);
        var id = Math.Max(_names.Keys.DefaultIfEmpty(0).Max(), _restarts.Select(restart => restart.Id).DefaultIfEmpty(0).Max()) + 1;
        _restarts.Add(new DocxListRestart(id, abstractId, level, start));
        _ids[Key(name, thread)] = id;
        _names[id] = name;
        return id;
    }

    public int? IdOf(string name, string? thread) => _ids.TryGetValue(Key(name, thread), out var id) ? id : null;

    public void Alias(int id, string name, int? level, int? start)
    {
        _names[id] = name;
        if (level is { } at && start is { } value) _starts[id] = (at, value);
    }

    public void Thread(int id, string thread) => _threads[id] = thread;

    public string? ThreadOf(int? id) => id is { } key && _threads.TryGetValue(key, out var thread) ? thread : null;

    private static string Key(string name, string? thread) => thread is null ? name : name + ThreadSeparator + thread;

    public int? TakeRestart(int? id)
    {
        if (id is not { } key || !_starts.Remove(key, out var start)) return null;
        return start.Start;
    }

    public int? IdOf(string? name) => name is not null && _ids.TryGetValue(name, out var id) ? id : null;

    public string? NameOf(int? id) => id is { } key && _names.TryGetValue(key, out var name) ? name : null;
}

public sealed class DocxNumberingCodec(DocxValues values, DocxPictureBullets pictures)
{
    private const int WordLevels = 9;
    private const int LargestStart = 32_767;
    private const string AbstractThread = "docx-abstract-";
    private const string InstanceThread = "docx-num-";

    private readonly IReadOnlyDictionary<NumberFormat, W.NumberFormatValues> _formats = new Dictionary<NumberFormat, W.NumberFormatValues>
    {
        [NumberFormat.None] = W.NumberFormatValues.None,
        [NumberFormat.Bullet] = W.NumberFormatValues.Bullet,
        [NumberFormat.Arabic] = W.NumberFormatValues.Decimal,
        [NumberFormat.LowerLetter] = W.NumberFormatValues.LowerLetter,
        [NumberFormat.UpperLetter] = W.NumberFormatValues.UpperLetter,
        [NumberFormat.LowerRoman] = W.NumberFormatValues.LowerRoman,
        [NumberFormat.UpperRoman] = W.NumberFormatValues.UpperRoman,
    };

    public W.Numbering Write(NumberingDefinitionsPart part, IReadOnlyList<ListStyle> lists, ListStyle outline, DocxListMap map, Func<int, string?> outlineStyle)
    {
        IReadOnlyList<ListStyle> all = outline.Levels.Any(level => level.Format != NumberFormat.None) ? [.. lists, outline] : lists;
        var numbering = new W.Numbering();
        for (var index = 0; index < all.Count; index++)
        {
            var abstractNum = new W.AbstractNum { AbstractNumberId = index };
            abstractNum.Append(new W.AbstractNumDefinitionName { Val = all[index].Name });
            for (var level = 0; level < Math.Min(WordLevels, all[index].Levels.Count); level++)
            {
                var element = Level(all[index].Levels[level], level);
                if (all[index].Levels[level] is { Format: NumberFormat.Bullet, Picture: { } picture })
                    element.InsertAfter(new W.LevelPictureBulletId { Val = pictures.Add(numbering, part, picture) }, element.GetFirstChild<W.LevelText>());
                if (ReferenceEquals(all[index], outline) && outlineStyle(level + 1) is { } styleId)
                    element.InsertAfter(new W.ParagraphStyleIdInLevel { Val = styleId }, element.GetFirstChild<W.NumberingFormat>());
                abstractNum.Append(element);
            }
            numbering.Append(abstractNum);
        }

        for (var index = 0; index < all.Count; index++)
        {
            numbering.Append(new W.NumberingInstance(new W.AbstractNumId { Val = index }) { NumberID = index + 1 });
            map.Bind(all[index].Name, index + 1, index);
        }

        return numbering;
    }

    public void WriteRestarts(W.Numbering numbering, DocxListMap map)
    {
        foreach (var restart in map.Restarts)
            numbering.Append(new W.NumberingInstance(
                new W.AbstractNumId { Val = restart.Abstract },
                new W.LevelOverride(new W.StartOverrideNumberingValue { Val = restart.Start }) { LevelIndex = restart.Level })
            { NumberID = restart.Id });
    }

    public IEnumerable<ListStyle> Read(W.Numbering? numbering, DocxListMap map)
    {
        if (numbering is null) yield break;
        var abstracts = numbering.Elements<W.AbstractNum>()
            .Where(item => item.AbstractNumberId?.Value is not null)
            .GroupBy(item => item.AbstractNumberId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var seen = new Dictionary<int, string>();
        foreach (var instance in numbering.Elements<W.NumberingInstance>())
        {
            if (instance.NumberID?.Value is not { } id || instance.AbstractNumId?.Val?.Value is not { } abstractId) continue;
            if (!abstracts.TryGetValue(abstractId, out var definition)) continue;
            var overrides = instance.Elements<W.LevelOverride>().ToList();
            if (seen.TryGetValue(abstractId, out var shared) && overrides.All(over => over.Level is null) && overrides.Count <= 1)
            {
                var single = overrides.FirstOrDefault();
                var restarted = single?.StartOverrideNumberingValue?.Val?.Value is { } restart ? values.Bounded(restart, 0, LargestStart) : (int?)null;
                map.Alias(id, shared, single?.LevelIndex?.Value, restarted);
                map.Thread(id, restarted is null ? AbstractThread + values.Text(abstractId) : InstanceThread + values.Text(id));
                continue;
            }
            var name = definition.GetFirstChild<W.AbstractNumDefinitionName>()?.Val?.Value is { Length: > 0 } stored && map.IdOf(stored) is null
                ? stored
                : "WWNum" + values.Text(id);
            var levels = Enumerable.Range(0, ListStyle.LevelCount).Select(level => new ListLevel(NumberFormat.Arabic)).ToArray();
            foreach (var level in definition.Elements<W.Level>())
            {
                var index = level.LevelIndex?.Value ?? -1;
                if (index is >= 0 and < ListStyle.LevelCount) levels[index] = Pictured(Read(level), level, numbering);
            }

            foreach (var over in instance.Elements<W.LevelOverride>())
            {
                var index = over.LevelIndex?.Value ?? -1;
                if (index is < 0 or >= ListStyle.LevelCount) continue;
                if (over.Level is { } replaced) levels[index] = Pictured(Read(replaced), replaced, numbering);
                if (over.StartOverrideNumberingValue?.Val?.Value is { } start) levels[index] = levels[index] with { StartAt = values.Bounded(start, 0, LargestStart) };
            }

            map.Bind(name, id);
            map.Thread(id, AbstractThread + values.Text(abstractId));
            seen.TryAdd(abstractId, name);
            yield return new ListStyle(name, levels);
        }
    }

    private W.Level Level(ListLevel level, int index)
    {
        var text = level.Format == NumberFormat.Bullet
            ? level.Bullet
            : level.Prefix + string.Join(".", Enumerable.Range(index + 2 - Math.Clamp(level.DisplayLevels, 1, index + 1), Math.Clamp(level.DisplayLevels, 1, index + 1)).Select(n => "%" + values.Text(n))) + level.Suffix;
        var element = new W.Level(
            new W.StartNumberingValue { Val = level.StartAt },
            new W.NumberingFormat { Val = _formats[level.Format] },
            new W.LevelText { Val = text },
            new W.LevelJustification { Val = W.LevelJustificationValues.Left },
            new W.PreviousParagraphProperties(new W.Indentation
            {
                Left = values.Twips(level.Indent + level.Hanging),
                Hanging = values.Twips(level.Hanging),
            }))
        { LevelIndex = index };
        if (level.Legal && level.Format != NumberFormat.Bullet) element.InsertAfter(new W.IsLegalNumberingStyle(), element.GetFirstChild<W.NumberingFormat>());
        return element;
    }

    public bool LosesPictures(W.Numbering? numbering) =>
        numbering is not null && numbering.Descendants<W.LevelPictureBulletId>().Any(id => id.Val?.Value is not { } value || pictures.Read(numbering, value) is null);

    private ListLevel Pictured(ListLevel read, W.Level level, W.Numbering numbering) =>
        read.Format == NumberFormat.Bullet && level.GetFirstChild<W.LevelPictureBulletId>()?.Val?.Value is { } id && pictures.Read(numbering, id) is { } picture
            ? read with { Picture = picture }
            : read;

    private ListLevel Read(W.Level level)
    {
        var format = level.NumberingFormat?.Val?.Value;
        var kind = format is null ? NumberFormat.Arabic : _formats.FirstOrDefault(pair => pair.Value == format.Value, new(NumberFormat.Arabic, W.NumberFormatValues.Decimal)).Key;
        var text = level.LevelText?.Val?.Value ?? string.Empty;
        var indentation = level.PreviousParagraphProperties?.GetFirstChild<W.Indentation>();
        var hanging = values.Points(indentation?.Hanging?.Value) ?? 0;
        var left = values.Points(indentation?.Left?.Value ?? indentation?.Start?.Value) ?? hanging;
        var start = values.Bounded(level.StartNumberingValue?.Val?.Value ?? 1, 0, LargestStart);
        if (kind == NumberFormat.Bullet)
            return new ListLevel(kind) { Bullet = text.Length > 0 ? text : "•", Prefix = string.Empty, Suffix = string.Empty, StartAt = start, Indent = left - hanging, Hanging = hanging };
        var first = text.IndexOf('%');
        var last = text.LastIndexOf('%');
        var placeholders = text.Count(character => character == '%');
        return new ListLevel(kind)
        {
            Prefix = first > 0 ? text[..first] : string.Empty,
            Suffix = last >= 0 && last + 2 <= text.Length ? text[(last + 2)..] : string.Empty,
            DisplayLevels = Math.Max(1, placeholders),
            Legal = level.IsLegalNumberingStyle is { } legal && (legal.Val?.Value ?? true),
            StartAt = start,
            Indent = left - hanging,
            Hanging = hanging,
        };
    }
}
