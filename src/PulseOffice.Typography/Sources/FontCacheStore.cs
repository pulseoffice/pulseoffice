using System.Text.Json;
using System.Text.Json.Serialization;
using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;

namespace PulseOffice.Typography.Sources;

public interface IFontCacheStore
{
    void SaveListing(string sourceId, IReadOnlyList<FontFamilyInfo> families);

    IReadOnlyList<FontFamilyInfo>? LoadListing(string sourceId, out DateTimeOffset savedAt);

    bool Contains(string sourceId, string family);

    IReadOnlyList<EmbeddedFontFile>? Load(string sourceId, string family);

    void Save(string sourceId, string family, IReadOnlyList<EmbeddedFontFile> files);

    IReadOnlyList<(string SourceId, string Family)> Downloaded();
}

public sealed class FileFontCacheStore(string root) : IFontCacheStore
{
    private const string ListingFile = "catalog.json";
    private const string ManifestFile = "manifest.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed record CachedFace(int Weight, bool Italic, string Location, int CollectionIndex);

    private sealed record CachedFamily(
        string Family, FontCategory Category, List<CachedFace> Faces, List<FontScript> Scripts,
        FontLicenseKind LicenseKind, string LicenseName, string? Spdx, string? LicenseUrl,
        EmbeddingLevel Embedding, string? Designer, string? Homepage, string? OnlineId);

    private sealed record CachedListing(DateTimeOffset SavedAt, List<CachedFamily> Families);

    private sealed record CachedFile(int Weight, bool Italic, string FileName);

    private sealed record CachedManifest(string Family, List<CachedFile> Files);

    public void SaveListing(string sourceId, IReadOnlyList<FontFamilyInfo> families)
    {
        ArgumentNullException.ThrowIfNull(families);
        var listing = new CachedListing(DateTimeOffset.UtcNow, [.. families.Select(Store)]);
        Write(Path.Combine(Folder(sourceId), ListingFile), JsonSerializer.SerializeToUtf8Bytes(listing, Json));
    }

    public IReadOnlyList<FontFamilyInfo>? LoadListing(string sourceId, out DateTimeOffset savedAt)
    {
        savedAt = DateTimeOffset.MinValue;
        var listing = Read<CachedListing>(Path.Combine(Folder(sourceId), ListingFile));
        if (listing?.Families is null) return null;
        savedAt = listing.SavedAt;
        return [.. listing.Families.Select(family => Restore(sourceId, family))];
    }

    public bool Contains(string sourceId, string family) =>
        Read<CachedManifest>(Path.Combine(FamilyFolder(sourceId, family), ManifestFile)) is { Family: { } name } && Same(name, family);

    public IReadOnlyList<EmbeddedFontFile>? Load(string sourceId, string family)
    {
        var folder = FamilyFolder(sourceId, family);
        var manifest = Read<CachedManifest>(Path.Combine(folder, ManifestFile));
        if (manifest?.Files is not { Count: > 0 } || !Same(manifest.Family, family)) return null;

        var files = new List<EmbeddedFontFile>();
        foreach (var file in manifest.Files)
        {
            var path = Path.Combine(folder, Path.GetFileName(file.FileName));
            if (!File.Exists(path)) return null;
            files.Add(new EmbeddedFontFile(manifest.Family, file.Weight, file.Italic, Path.GetFileName(file.FileName), File.ReadAllBytes(path)));
        }

        return files;
    }

    public void Save(string sourceId, string family, IReadOnlyList<EmbeddedFontFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var folder = FamilyFolder(sourceId, family);
        Directory.CreateDirectory(folder);

        foreach (var file in files) Write(Path.Combine(folder, Path.GetFileName(file.FileName)), file.Data);

        var manifest = new CachedManifest(family, [.. files.Select(file => new CachedFile(file.Weight, file.Italic, Path.GetFileName(file.FileName)))]);
        Write(Path.Combine(folder, ManifestFile), JsonSerializer.SerializeToUtf8Bytes(manifest, Json));
    }

    public IReadOnlyList<(string SourceId, string Family)> Downloaded()
    {
        if (!Directory.Exists(root)) return [];

        var found = new List<(string, string)>();
        foreach (var source in Directory.EnumerateDirectories(root))
            foreach (var family in Directory.EnumerateDirectories(source))
                if (Read<CachedManifest>(Path.Combine(family, ManifestFile)) is { Family: { Length: > 0 } name })
                    found.Add((Path.GetFileName(source), name));
        return found;
    }

    private bool Same(string? stored, string requested) => string.Equals(stored, requested, StringComparison.OrdinalIgnoreCase);

    private string Folder(string sourceId) => Path.Combine(root, Safe(sourceId));

    private string FamilyFolder(string sourceId, string family) => Path.Combine(Folder(sourceId), Safe(family.ToLowerInvariant()));

    private string Safe(string name)
    {
        var cleaned = new string([.. name.Select(letter => char.IsLetterOrDigit(letter) || letter is '-' or '_' ? letter : '-')]).Trim('-');
        return cleaned.Length == 0 ? "font" : cleaned;
    }

    private T? Read<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void Write(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private CachedFamily Store(FontFamilyInfo family) => new(
        family.Family, family.Category,
        [.. family.Faces.Select(face => new CachedFace(face.Weight, face.Italic, face.Location, face.CollectionIndex))],
        [.. family.Scripts], family.License.Kind, family.License.Name, family.License.SpdxId, family.License.Url,
        family.Embedding.Level, family.Designer, family.Homepage, family.OnlineId);

    private FontFamilyInfo Restore(string sourceId, CachedFamily family) => new()
    {
        Family = family.Family,
        SourceId = sourceId,
        Origin = FontOrigin.Online,
        Category = family.Category,
        Faces = [.. (family.Faces ?? []).Select(face => new FontFaceRef(face.Weight, face.Italic, face.Location ?? string.Empty, face.CollectionIndex))],
        Scripts = family.Scripts ?? [],
        License = new FontLicense(family.LicenseKind, family.LicenseName ?? "Unknown", family.Spdx, family.LicenseUrl),
        Embedding = new EmbeddingPermission(family.Embedding, false, false),
        Designer = family.Designer,
        Homepage = family.Homepage,
        OnlineId = family.OnlineId,
    };
}
