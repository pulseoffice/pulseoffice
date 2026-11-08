using System.Text.Json;
using PulseOffice.Document.Fonts;
using System.Collections.Concurrent;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.OpenType;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Typography.Sources;

public sealed class CachedFontSource(IFontSource inner, IFontCacheStore store, FontServiceOptions options, TimeProvider time, IOpenTypeReader reader) : IFontSource
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _downloads = new(StringComparer.OrdinalIgnoreCase);

    public string Id => inner.Id;

    public FontOrigin Origin => inner.Origin;

    public async Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default)
    {
        var cached = store.LoadListing(inner.Id, out var savedAt);
        IReadOnlyList<FontFamilyInfo> families;

        var age = time.GetUtcNow() - savedAt;
        if (cached is not null && age >= TimeSpan.Zero && age < options.ListingLifetime)
        {
            families = cached;
        }
        else
        {
            try
            {
                families = await inner.ListAsync(ct).ConfigureAwait(false);
                if (families.Count > 0) Remember(families);
                else families = cached ?? families;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException or InvalidDataException
                || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                families = cached ?? [];
            }
        }

        return [.. families.Select(family => store.Contains(inner.Id, family.Family) ? family with { Origin = FontOrigin.Downloaded } : family)];
    }

    private void Remember(IReadOnlyList<FontFamilyInfo> families)
    {
        try
        {
            store.SaveListing(inner.Id, families);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public async Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (Cached(family, progress) is { } early) return early;

        var gate = _downloads.GetOrAdd(family.Family, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return Cached(family, progress) ?? await Download(family, progress, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private OperationResult<IReadOnlyList<EmbeddedFontFile>>? Cached(FontFamilyInfo family, IProgress<FontProgress>? progress)
    {
        if (store.Load(inner.Id, family.Family) is not { } files || !files.All(file => reader.LooksLikeFont(file.Data))) return null;
        progress?.Report(new FontProgress(files.Count, files.Count, files.Sum(file => (long)file.Data.Length)));
        return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(files);
    }

    private async Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> Download(
        FontFamilyInfo family, IProgress<FontProgress>? progress, CancellationToken ct)
    {
        var fetched = await inner.FetchAsync(family, progress, ct).ConfigureAwait(false);
        if (fetched.Succeeded && fetched.Value is { Count: > 0 } downloaded && downloaded.All(file => reader.LooksLikeFont(file.Data)))
        {
            try
            {
                store.Save(inner.Id, family.Family, downloaded);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(downloaded, [Diagnostic.Warning("FONT030", ex.Message).With(ex.Message)]);
            }
        }

        return fetched;
    }
}
