using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Sources;

namespace PulseOffice.UI.Controls.Fonts;

public sealed class FontFamilyMappingTable : IReadOnlyDictionary<string, FontFamily>
{
    private readonly ConcurrentDictionary<string, FontFamily> _map = new(StringComparer.OrdinalIgnoreCase);

    public FontFamily this[string key] => _map[key];

    public IEnumerable<string> Keys => _map.Keys;

    public IEnumerable<FontFamily> Values => _map.Values;

    public int Count => _map.Count;

    public bool ContainsKey(string key) => _map.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out FontFamily value) => _map.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, FontFamily>> GetEnumerator() => _map.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void Map(string family, FontFamily target) => _map[family] = target;
}

public interface IRuntimeFonts
{
    bool Contains(string family);

    int Register(IEnumerable<EmbeddedFontFile> files);

    FontFamily Resolve(string? family);
}

public sealed class RuntimeFonts(FontFamilyMappingTable mappings) : IRuntimeFonts
{
    private const string CollectionKey = "fonts:PulseOffice";

    private readonly RuntimeFontCollection _collection = new(new Uri(CollectionKey));
    private readonly HashSet<string> _faces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private bool _attached;

    public bool Contains(string family) => mappings.ContainsKey(family);

    public int Register(IEnumerable<EmbeddedFontFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var added = 0;

        lock (_lock)
        {
            if (!_attached)
            {
                FontManager.Current.AddFontCollection(_collection);
                _attached = true;
            }

            foreach (var file in files)
            {
                var key = $"{file.Family}|{file.Weight}|{file.Italic}";
                if (_faces.Contains(key) || Load(file) is not { } typeface) continue;
                _faces.Add(key);

                var target = new FontFamily($"{CollectionKey}#{typeface.FamilyName}");
                mappings.Map(file.Family, target);
                if (!string.Equals(typeface.FamilyName, file.Family, StringComparison.OrdinalIgnoreCase))
                    mappings.Map(typeface.FamilyName, target);
                added++;
            }
        }

        return added;
    }

    private GlyphTypeface? Load(EmbeddedFontFile file)
    {
        try
        {
            using var stream = new MemoryStream(file.Data, writable: false);
            return _collection.TryAddGlyphTypeface(stream, out var typeface) ? typeface : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or InvalidOperationException
            or IndexOutOfRangeException or EndOfStreamException or NotSupportedException)
        {
            return null;
        }
    }

    public FontFamily Resolve(string? family)
    {
        if (string.IsNullOrWhiteSpace(family)) return FontFamily.Default;
        return mappings.TryGetValue(family, out var mapped) ? mapped : new FontFamily(family);
    }

    private sealed class RuntimeFontCollection(Uri key) : FontCollectionBase
    {
        public override Uri Key { get; } = key;
    }
}

public sealed class AvaloniaInstalledFontNames : IInstalledFontNames
{
    public IReadOnlyList<string> Families
    {
        get
        {
            try
            {
                return [.. FontManager.Current.SystemFonts.Select(family => family.Name).Where(name => !name.StartsWith('.'))];
            }
            catch (InvalidOperationException)
            {
                return [];
            }
        }
    }
}
