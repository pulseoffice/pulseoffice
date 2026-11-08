using System.IO.Compression;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlPackageRepair
{
    private const long MaxPackageBytes = 512L * 1024 * 1024;
    private const long MaxExpandedBytes = 1024L * 1024 * 1024;

    private readonly IReadOnlyList<IPackageRepairStep> _steps =
    [
        new MalformedXmlRepair(new WorksheetSalvage()),
        new DuplicateRelationshipRepair(),
        new DanglingRelationshipRepair(),
        new ContentTypeRepair(),
        new UndeclaredPartRepair(),
        new DeepNestingRepair(),
    ];

    public Stream Repair(Stream stream) => Repair(stream, []);

    public Stream Repair(Stream stream, ICollection<PartDamage> damage)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(damage);
        var copy = new MemoryStream();
        CopyLimited(stream, copy);
        copy.Position = 0;
        RejectExpansionBeyondBudget(copy);
        copy.Position = 0;

        try
        {
            var package = PackageModel.Load(copy);
            var changed = package.Renamed;
            foreach (var step in _steps) changed |= step.Apply(package);
            foreach (var entry in package.Damage) damage.Add(entry);
            changed |= package.Damage.Count > 0;
            if (!changed)
            {
                copy.Position = 0;
                return copy;
            }

            var rebuilt = package.Save();
            copy.Dispose();
            return rebuilt;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or IOException)
        {
            copy.Position = 0;
            return copy;
        }
    }

    private void RejectExpansionBeyondBudget(Stream package)
    {
        long declared = 0;
        try
        {
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                declared += Math.Max(0, entry.Length);
                if (declared > MaxExpandedBytes) break;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return;
        }

        if (declared > MaxExpandedBytes) throw new InvalidDataException("OOXML package expands beyond the size limit.");
    }

    private void CopyLimited(Stream source, Stream target)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxPackageBytes) throw new InvalidDataException("OOXML package is too large.");
            target.Write(buffer, 0, read);
        }
    }
}

public enum PartDamageKind { Recovered, Skipped, Trimmed }

public sealed record PartDamage(string Part, PartDamageKind Kind);

internal interface IPackageRepairStep
{
    bool Apply(PackageModel package);
}

internal sealed class PackageModel
{
    public const string ContentTypesPart = "[Content_Types].xml";
    private const long MaxEntryBytes = 512L * 1024 * 1024;

    private readonly Dictionary<string, byte[]> _parts;
    private readonly HashSet<string> _names;

    private PackageModel(Dictionary<string, byte[]> parts, bool renamed, List<PartDamage> damage)
    {
        _parts = parts;
        _names = new HashSet<string>(parts.Keys, StringComparer.OrdinalIgnoreCase);
        Renamed = renamed;
        Damage = damage;
    }

    public List<PartDamage> Damage { get; }

    public byte[]? Truncated(string name) => _truncated.GetValueOrDefault(name);

    private readonly Dictionary<string, byte[]> _truncated = new(StringComparer.OrdinalIgnoreCase);

    public bool Renamed { get; }

    public IEnumerable<string> Names => _parts.Keys;

    public IReadOnlyList<string> TruncatedNames => [.. _truncated.Keys];

    public void Resolve(string name) => _truncated.Remove(name);

    public bool Contains(string name) => _parts.ContainsKey(name);

    public string? ActualName(string name) => _names.TryGetValue(name, out var actual) ? actual : null;

    public byte[]? this[string name]
    {
        get => _parts.GetValueOrDefault(name);
        set
        {
            if (value is null)
            {
                _parts.Remove(name);
                _names.Remove(name);
            }
            else
            {
                _parts[name] = value;
                _names.Add(name);
            }
        }
    }

    public static PackageModel Load(Stream stream)
    {
        var parts = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var renamed = false;
        var truncated = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            var name = Normalize(entry.FullName);
            if (name.Length == 0 || name.EndsWith('/')) { renamed = true; continue; }
            if (string.Equals(name, ContentTypesPart, StringComparison.OrdinalIgnoreCase)) name = ContentTypesPart;
            if (name != entry.FullName || name.Any(character => character > 127)) renamed = true;
            if (entry.Length > MaxEntryBytes) throw new InvalidDataException("OOXML part is too large.");
            using var buffer = new MemoryStream();
            try
            {
                using var input = entry.Open();
                input.CopyTo(buffer);
                parts[name] = buffer.ToArray();
            }
            catch (InvalidDataException)
            {
                truncated[name] = buffer.ToArray();
            }
        }

        var parents = new HashSet<string>(parts.Keys.SelectMany(ParentsOf), StringComparer.OrdinalIgnoreCase);
        var folders = parts.Keys.Where(parents.Contains).ToList();
        foreach (var folder in folders) parts.Remove(folder);
        var model = new PackageModel(parts, renamed || folders.Count > 0, []);
        foreach (var (name, bytes) in truncated) model._truncated[name] = bytes;
        return model;
    }

    public MemoryStream Save()
    {
        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in _parts)
            {
                var entry = archive.CreateEntry(Escape(name), CompressionLevel.Fastest);
                using var stream = entry.Open();
                stream.Write(bytes);
            }
        }

        output.Position = 0;
        return output;
    }

    private static IEnumerable<string> ParentsOf(string name)
    {
        for (var slash = name.IndexOf('/'); slash > 0; slash = name.IndexOf('/', slash + 1))
            yield return name[..slash];
    }

    private static string Normalize(string name) => Uri.UnescapeDataString(name.Replace('\\', '/').TrimStart('/'));

    private static string Escape(string name) =>
        name == ContentTypesPart ? name : string.Join('/', name.Split('/').Select(Uri.EscapeDataString));
}
