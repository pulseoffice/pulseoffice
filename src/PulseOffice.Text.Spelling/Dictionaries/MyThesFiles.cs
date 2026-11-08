using System.Globalization;
using System.Text;
using System.Xml;

namespace PulseOffice.Text.Spelling.Dictionaries;

public sealed record MyThesEntry(string Headword, IReadOnlyList<ThesaurusMeaning> Meanings);

public sealed record MyThesFiles(byte[] Data, byte[] Index);

public interface IMyThesWriter
{
    MyThesFiles Write(IEnumerable<MyThesEntry> entries);
}

public sealed class MyThesWriter : IMyThesWriter
{
    private const string Encoding = "UTF-8";

    public MyThesFiles Write(IEnumerable<MyThesEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var utf8 = new UTF8Encoding(false);
        var data = new MemoryStream();
        var index = new List<(string Word, long Offset)>();

        Append(data, utf8, Encoding);

        foreach (var entry in entries
            .Where(entry => Clean(entry.Headword).Length > 0 && entry.Meanings.Count > 0)
            .OrderBy(entry => entry.Headword, StringComparer.Ordinal))
        {
            var headword = Clean(entry.Headword);
            index.Add((headword, data.Length));
            Append(data, utf8, $"{headword}|{entry.Meanings.Count.ToString(CultureInfo.InvariantCulture)}");

            foreach (var meaning in entry.Meanings)
                Append(data, utf8, string.Join('|', [$"({Clean(meaning.PartOfSpeech)})", .. meaning.Synonyms.Select(Clean)]));
        }

        var text = new StringBuilder();
        text.Append(Encoding).Append('\n').Append(index.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var (word, offset) in index)
            text.Append(word).Append('|').Append(offset.ToString(CultureInfo.InvariantCulture)).Append('\n');

        return new MyThesFiles(data.ToArray(), utf8.GetBytes(text.ToString()));
    }

    private void Append(MemoryStream stream, Encoding encoding, string line)
    {
        var bytes = encoding.GetBytes(line + "\n");
        stream.Write(bytes);
    }

    private string Clean(string text) => text.Replace('|', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
}

public interface IWordNetThesaurusConverter
{
    IReadOnlyList<MyThesEntry> Convert(Stream lexicalMarkup);
}

public sealed class WordNetThesaurusConverter : IWordNetThesaurusConverter
{
    private const string GenericSuffix = " (generic term)";
    private const string SimilarSuffix = " (similar term)";

    private sealed record Sense(string Synset);

    private sealed record Entry(string Form, string PartOfSpeech, List<Sense> Senses);

    private sealed record Synset(IReadOnlyList<string> Members, List<string> Generic, List<string> Similar);

    public IReadOnlyList<MyThesEntry> Convert(Stream lexicalMarkup)
    {
        ArgumentNullException.ThrowIfNull(lexicalMarkup);

        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var order = new List<string>();
        var synsets = new Dictionary<string, Synset>(StringComparer.Ordinal);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        using var reader = XmlReader.Create(lexicalMarkup, settings);
        Entry? entry = null;
        Synset? synset = null;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.LocalName == "LexicalEntry") entry = null;
                else if (reader.LocalName == "Synset") synset = null;
                continue;
            }

            if (reader.NodeType != XmlNodeType.Element) continue;

            switch (reader.LocalName)
            {
                case "LexicalEntry" when reader.GetAttribute("id") is { } id:
                    entry = new Entry(string.Empty, string.Empty, []);
                    entries[id] = entry;
                    order.Add(id);
                    if (reader.IsEmptyElement) entry = null;
                    break;
                case "Lemma" when entry is not null:
                    var form = reader.GetAttribute("writtenForm") ?? string.Empty;
                    var updated = entry with { Form = form, PartOfSpeech = reader.GetAttribute("partOfSpeech") ?? string.Empty };
                    entries[order[^1]] = updated;
                    entry = updated;
                    break;
                case "Sense" when entry is not null && reader.GetAttribute("synset") is { } target:
                    entry.Senses.Add(new Sense(target));
                    break;
                case "Synset" when reader.GetAttribute("id") is { } id:
                    synset = new Synset(
                        (reader.GetAttribute("members") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries),
                        [],
                        []);
                    synsets[id] = synset;
                    if (reader.IsEmptyElement) synset = null;
                    break;
                case "SynsetRelation" when synset is not null && reader.GetAttribute("target") is { } related:
                    var kind = reader.GetAttribute("relType");
                    if (kind is "hypernym" or "instance_hypernym") synset.Generic.Add(related);
                    else if (kind == "similar") synset.Similar.Add(related);
                    break;
            }
        }

        return Assemble(entries, order, synsets);
    }

    private IReadOnlyList<MyThesEntry> Assemble(
        Dictionary<string, Entry> entries, List<string> order, Dictionary<string, Synset> synsets)
    {
        var forms = entries.ToDictionary(pair => pair.Key, pair => pair.Value.Form, StringComparer.Ordinal);
        var grouped = new Dictionary<string, (string Headword, List<ThesaurusMeaning> Meanings)>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in order)
        {
            var entry = entries[id];
            if (entry.Form.Length == 0) continue;

            if (!grouped.TryGetValue(entry.Form, out var target))
            {
                target = (entry.Form, []);
                grouped[entry.Form] = target;
            }

            foreach (var sense in entry.Senses)
            {
                if (!synsets.TryGetValue(sense.Synset, out var synset)) continue;

                var words = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entry.Form };
                Add(words, seen, synset.Members, forms, string.Empty);
                foreach (var generic in synset.Generic)
                    if (synsets.TryGetValue(generic, out var parent)) Add(words, seen, parent.Members, forms, GenericSuffix);
                foreach (var similar in synset.Similar)
                    if (synsets.TryGetValue(similar, out var neighbour)) Add(words, seen, neighbour.Members, forms, SimilarSuffix);

                if (words.Count > 0) target.Meanings.Add(new ThesaurusMeaning(PartOfSpeech(entry.PartOfSpeech), words));
            }
        }

        return [.. grouped.Values.Where(pair => pair.Meanings.Count > 0).Select(pair => new MyThesEntry(pair.Headword, pair.Meanings))];
    }

    private void Add(List<string> words, HashSet<string> seen, IReadOnlyList<string> members, Dictionary<string, string> forms, string suffix)
    {
        foreach (var member in members)
        {
            if (!forms.TryGetValue(member, out var form) || form.Length == 0 || !seen.Add(form)) continue;
            words.Add(form + suffix);
        }
    }

    private string PartOfSpeech(string code) => code switch
    {
        "n" => "noun",
        "v" => "verb",
        "a" or "s" => "adj",
        "r" => "adv",
        _ => "-",
    };
}
