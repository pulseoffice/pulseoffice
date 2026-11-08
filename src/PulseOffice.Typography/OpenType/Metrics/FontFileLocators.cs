using System.Collections.Concurrent;

namespace PulseOffice.Typography.OpenType.Metrics;

public interface IFontFileLocator
{
    FontMetricsFace? Find(string family, bool bold, bool italic);
}

public sealed class DirectoryFontLocator : IFontFileLocator
{
    private readonly Lazy<IReadOnlyList<FontMetricsFace>> _fonts;
    private readonly HashSet<string> FontExtensions = new([".ttf", ".otf", ".ttc", ".otc"], StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(string, bool, bool), FontMetricsFace?> _matches = new();

    private readonly IFontMetricsReader _reader;
    private readonly EnumerationOptions _walk = new() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };

    public DirectoryFontLocator(IEnumerable<string> directories) : this(directories, new FontMetricsReader())
    {
    }

    public DirectoryFontLocator(IEnumerable<string> directories, IFontMetricsReader reader)
    {
        _reader = reader;
        var list = directories.ToList();
        _fonts = new Lazy<IReadOnlyList<FontMetricsFace>>(() => Load(list));
    }

    public FontMetricsFace? Find(string family, bool bold, bool italic) =>
        _matches.GetOrAdd((family.ToLowerInvariant(), bold, italic), key => Match(key.Item1, key.Item2, key.Item3));

    private FontMetricsFace? Match(string family, bool bold, bool italic)
    {
        var weight = bold ? 700 : 400;
        return _fonts.Value.Where(font => font.Family.Equals(family, StringComparison.OrdinalIgnoreCase))
            .OrderBy(font => font.Italic == italic ? 0 : 1)
            .ThenBy(font => Math.Abs(font.Face.Weight - weight))
            .ThenBy(font => Math.Abs(font.Face.Width - 5))
            .FirstOrDefault();
    }

    private IReadOnlyList<FontMetricsFace> Load(IReadOnlyList<string> directories)
    {
        var fonts = new List<FontMetricsFace>();
        foreach (var directory in directories.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", _walk))
            {
                var extension = Path.GetExtension(file);
                if (!FontExtensions.Contains(extension)) continue;
                fonts.AddRange(TryLoad(file));
            }
        }
        return fonts;
    }

    private IReadOnlyList<FontMetricsFace> TryLoad(string file)
    {
        try
        {
            return _reader.Read(File.ReadAllBytes(file));
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or IndexOutOfRangeException or ArgumentException)
        {
            return [];
        }
    }
}

public sealed class IndexedFontLocator : IFontFileLocator
{
    private readonly HashSet<string> _extensions = new([".ttf", ".otf", ".ttc", ".otc"], StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<IReadOnlyDictionary<string, List<IndexedFace>>> _index;
    private readonly ConcurrentDictionary<(string, bool, bool), FontMetricsFace?> _matches = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<FontMetricsFace>> _files = new(StringComparer.Ordinal);
    private readonly IOpenTypeReader _names;
    private readonly IFontMetricsReader _reader;
    private readonly EnumerationOptions _walk = new() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };

    public IndexedFontLocator(PulseOffice.Platform.ISystemFontDirectories directories, IOpenTypeReader names, IFontMetricsReader reader)
        : this(directories.Directories, names, reader)
    {
    }

    public IndexedFontLocator(IEnumerable<string> directories, IOpenTypeReader names, IFontMetricsReader reader)
    {
        _names = names;
        _reader = reader;
        var list = directories.ToList();
        _index = new Lazy<IReadOnlyDictionary<string, List<IndexedFace>>>(() => Scan(list));
    }

    public IEnumerable<string> Families => _index.Value.Keys;

    public FontMetricsFace? Find(string family, bool bold, bool italic) =>
        _matches.GetOrAdd((family.ToLowerInvariant(), bold, italic), key => Match(key.Item1, key.Item2, key.Item3));

    private FontMetricsFace? Match(string family, bool bold, bool italic)
    {
        if (!_index.Value.TryGetValue(family, out var candidates)) return null;
        var weight = bold ? 700 : 400;
        var chosen = candidates
            .OrderBy(face => face.Italic == italic ? 0 : 1)
            .ThenBy(face => Math.Abs(face.Weight - weight))
            .ThenBy(face => Math.Abs(face.Width - 5))
            .First();
        return _files.GetOrAdd(chosen.Path, Load).FirstOrDefault(face => face.Face.CollectionIndex == chosen.CollectionIndex);
    }

    private IReadOnlyList<FontMetricsFace> Load(string path)
    {
        try
        {
            return _reader.Read(File.ReadAllBytes(path));
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or IndexOutOfRangeException or ArgumentException)
        {
            return [];
        }
    }

    private Dictionary<string, List<IndexedFace>> Scan(IReadOnlyList<string> directories)
    {
        var index = new Dictionary<string, List<IndexedFace>>(StringComparer.Ordinal);
        foreach (var directory in directories.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", _walk).Where(file => _extensions.Contains(Path.GetExtension(file))).ToList();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var file in files) Add(index, file);
        }
        return index;
    }

    private void Add(Dictionary<string, List<IndexedFace>> index, string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
            foreach (var face in _names.Read(stream))
            {
                var entry = new IndexedFace(file, face.CollectionIndex, face.Weight, face.Italic, face.Width);
                foreach (var name in new[] { face.Family, face.FullName, face.PostScriptName }.OfType<string>().Select(name => name.ToLowerInvariant()).Distinct())
                {
                    if (!index.TryGetValue(name, out var list)) index[name] = list = [];
                    list.Add(entry);
                }
            }
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or IndexOutOfRangeException or ArgumentException or EndOfStreamException)
        {
        }
    }

    private sealed record IndexedFace(string Path, int CollectionIndex, int Weight, bool Italic, int Width);
}
