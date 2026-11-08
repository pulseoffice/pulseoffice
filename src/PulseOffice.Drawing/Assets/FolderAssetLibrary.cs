using System.Text.Json;
using System.Text.Json.Serialization;

namespace PulseOffice.Drawing.Assets;

public interface IAssetSource
{
    IReadOnlyCollection<AssetPack> Packs { get; }

    IReadOnlyList<VectorAsset> Items(string packId);
}

public sealed record InstalledPackManifest
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("kind")] public string? Kind { get; init; }
    [JsonPropertyName("license")] public string? License { get; init; }
    [JsonPropertyName("homepage")] public string? Homepage { get; init; }
    [JsonPropertyName("attribution")] public string? Attribution { get; init; }
    [JsonPropertyName("monochrome")] public bool Monochrome { get; init; } = true;
}

public sealed class FolderAssetSource : IAssetSource
{
    private readonly string _root;
    private readonly Dictionary<string, AssetPack> _packs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _directories = new(StringComparer.Ordinal);

    public FolderAssetSource(string root)
    {
        _root = root;
        Scan();
    }

    public IReadOnlyCollection<AssetPack> Packs => _packs.Values;

    public void Refresh()
    {
        _packs.Clear();
        _directories.Clear();
        Scan();
    }

    private void Scan()
    {
        if (!Directory.Exists(_root)) return;

        foreach (var directory in Directory.EnumerateDirectories(_root).Order(StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(directory, "pack.json");
            var id = Path.GetFileName(directory);

            var manifest = ReadManifest(manifestPath);

            var pack = new AssetPack
            {
                Id = manifest?.Id is { Length: > 0 } declared && !_packs.ContainsKey(declared) ? declared : id,
                DisplayName = manifest?.Name ?? id,
                Kind = KindOf(manifest?.Kind),
                License = LicenseOf(manifest),
                HomepageUrl = manifest?.Homepage,
                IsMonochrome = manifest?.Monochrome ?? true,
                IsBundled = false,
                ItemCount = Directory.EnumerateFiles(directory, "*.svg").Count(),
            };

            if (_packs.ContainsKey(pack.Id)) continue;

            _packs[pack.Id] = pack;
            _directories[pack.Id] = directory;
        }
    }

    private static InstalledPackManifest? ReadManifest(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<InstalledPackManifest>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static AssetPackKind KindOf(string? kind) =>
        Enum.TryParse<AssetPackKind>(kind, ignoreCase: true, out var parsed) ? parsed : AssetPackKind.Icons;

    private static AssetLicense LicenseOf(InstalledPackManifest? manifest)
    {
        var license = manifest?.License?.ToUpperInvariant() switch
        {
            "MIT" => AssetLicense.Mit,
            "ISC" => AssetLicense.Isc,
            "APACHE-2.0" or "APACHE2" => AssetLicense.Apache2,
            "CC0-1.0" or "CC0" => AssetLicense.Cc0,
            "CC-BY-4.0" or "CCBY4" => AssetLicense.CcBy4,
            "CC-BY-SA-4.0" or "CCBYSA4" => AssetLicense.CcBySa4,
            "OFL-1.1" or "OFL" => AssetLicense.Ofl,
            null => AssetLicense.Mit,
            _ => new AssetLicense(manifest!.License!, manifest.License!, true, true),
        };

        return manifest?.Attribution is { Length: > 0 } attribution
            ? license with { AttributionText = attribution }
            : license;
    }

    public IReadOnlyList<VectorAsset> Items(string packId)
    {
        if (!_directories.TryGetValue(packId, out var directory)) return [];

        var monochrome = _packs.TryGetValue(packId, out var pack) && pack.IsMonochrome;
        var items = new List<VectorAsset>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.svg").OrderBy(path => path))
        {
            string svg;

            try
            {
                svg = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(file);

            items.Add(new VectorAsset
            {
                Id = $"{packId}/{name}",
                Name = name.Replace('-', ' ').Replace('_', ' '),
                SourceKind = VectorSourceKind.InlineSvg,
                SvgContent = svg,
                PackId = packId,
                ItemId = name,
                IsRecolorable = monochrome,
                Keywords = [.. name.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)],
            });
        }

        return items;
    }
}

public sealed class AssetLibrary : IAssetLibrary
{
    private readonly IReadOnlyList<IAssetSource> _sources;

    public AssetLibrary(IEnumerable<IAssetSource> sources) => _sources = [.. sources];

    public IReadOnlyCollection<AssetPack> Packs => [.. _sources.SelectMany(source => source.Packs)];

    public Task<IReadOnlyList<VectorAsset>> SearchAsync(
        string query, AssetPackKind? kind = null, string? packId = null, CancellationToken ct = default)
    {
        var terms = (query ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matches = new List<VectorAsset>();

        foreach (var source in _sources)
            foreach (var pack in source.Packs)
            {
                if (packId is not null && pack.Id != packId) continue;
                if (kind is { } wanted && pack.Kind != wanted) continue;

                foreach (var item in source.Items(pack.Id))
                    if (Matches(item, terms)) matches.Add(item);
            }

        return Task.FromResult<IReadOnlyList<VectorAsset>>(matches);
    }

    private static bool Matches(VectorAsset asset, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0) return true;

        foreach (var term in terms)
        {
            var hit = asset.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || asset.Keywords.Any(word => word.Contains(term, StringComparison.OrdinalIgnoreCase));

            if (!hit) return false;
        }

        return true;
    }

    public Task<VectorAsset?> GetAsync(string packId, string itemId, CancellationToken ct = default)
    {
        foreach (var source in _sources)
            foreach (var item in source.Items(packId))
                if (item.ItemId == itemId)
                    return Task.FromResult<VectorAsset?>(item);

        return Task.FromResult<VectorAsset?>(null);
    }

    public Task<VectorAsset> ResolveGlyphAsync(GlyphOrigin origin, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Glyph outlining needs a font rasteriser and is not available yet.");
}
