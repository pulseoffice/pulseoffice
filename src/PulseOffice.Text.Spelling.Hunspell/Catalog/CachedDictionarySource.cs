using System.Text.Json;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public sealed record CachedListing(DateTimeOffset SavedAt, IReadOnlyList<DictionaryPackage> Packages, string? Fingerprint = null);

public interface IDictionaryCacheStore
{
    CachedListing? Load(string sourceId);

    void Save(string sourceId, CachedListing listing);
}

public sealed class DictionaryCacheStore(string folder) : IDictionaryCacheStore
{
    public CachedListing? Load(string sourceId)
    {
        var path = PathOf(sourceId);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<CachedListing>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(string sourceId, CachedListing listing)
    {
        Directory.CreateDirectory(folder);
        var path = PathOf(sourceId);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(listing));
        File.Move(temporary, path, overwrite: true);
    }

    private string PathOf(string sourceId) =>
        Path.Combine(folder, new string([.. sourceId.Where(char.IsAsciiLetterOrDigit)]) + ".json");
}

public interface IRefreshableDictionarySource : IDictionarySource
{
    bool LastListingFailed { get; }

    Task<IReadOnlyList<DictionaryPackage>> ListAsync(bool refresh, CancellationToken ct = default);
}

public sealed class CachedDictionarySource(
    IDictionarySource inner, IDictionaryCacheStore store, DictionaryServiceOptions options, TimeProvider time, IDictionaryCodes codes)
    : IRefreshableDictionarySource
{
    public const int CatalogFormat = 2;

    public string Fingerprint => $"{inner.Id}/{CatalogFormat}/{codes.Version}";

    public string Id => inner.Id;

    public DictionaryPart Part => inner.Part;

    public int Priority => inner.Priority;

    public bool LastListingFailed { get; private set; }

    public Task<IReadOnlyList<DictionaryPackage>> ListAsync(CancellationToken ct = default) => ListAsync(false, ct);

    public async Task<IReadOnlyList<DictionaryPackage>> ListAsync(bool refresh, CancellationToken ct = default)
    {
        var cached = store.Load(inner.Id) is { } saved && saved.Fingerprint == Fingerprint ? saved : null;
        var age = cached is null ? TimeSpan.MaxValue : time.GetUtcNow() - cached.SavedAt;
        LastListingFailed = false;

        if (!refresh && cached is not null && age >= TimeSpan.Zero && age < options.ListingLifetime) return cached.Packages;

        try
        {
            var packages = await inner.ListAsync(ct).ConfigureAwait(false);
            if (packages.Count == 0) return cached?.Packages ?? packages;

            Remember(packages);
            return packages;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException or InvalidDataException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            LastListingFailed = true;
            return cached?.Packages ?? [];
        }
    }

    public Task<IReadOnlyList<DictionaryFileContent>> FetchAsync(
        DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default) =>
        inner.FetchAsync(package, progress, ct);

    private void Remember(IReadOnlyList<DictionaryPackage> packages)
    {
        try
        {
            store.Save(inner.Id, new CachedListing(time.GetUtcNow(), packages, Fingerprint));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
