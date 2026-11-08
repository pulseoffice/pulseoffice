using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Sources;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Typography.Catalog;

public interface IFontCatalog
{
    event EventHandler? Changed;

    Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default);

    Task RefreshAsync(CancellationToken ct = default);

    FontFamilyInfo? Find(string family);

    Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> AcquireAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default);
}

public sealed class CompositeFontCatalog : IFontCatalog
{
    private readonly IReadOnlyList<IFontSource> _sources;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, IReadOnlyList<FontFamilyInfo>> _stable = new(StringComparer.Ordinal);
    private IReadOnlyList<FontFamilyInfo> _merged = [];
    private bool _loaded;

    public CompositeFontCatalog(IEnumerable<IFontSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = [.. sources];
    }

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default)
    {
        if (!_loaded) await LoadAsync(force: false, ct).ConfigureAwait(false);
        else await MergeAsync(ct).ConfigureAwait(false);
        return _merged;
    }

    public Task RefreshAsync(CancellationToken ct = default) => LoadAsync(force: true, ct);

    public FontFamilyInfo? Find(string family) =>
        string.IsNullOrWhiteSpace(family)
            ? null
            : _merged.FirstOrDefault(entry => string.Equals(entry.Family, family.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> AcquireAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(family);
        var source = _sources.FirstOrDefault(candidate => candidate.Id == family.SourceId);
        if (source is null)
            return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(Diagnostic.Error("FONT051", $"Unknown font source '{family.SourceId}'.").With(family.SourceId));

        var result = await source.FetchAsync(family, progress, ct).ConfigureAwait(false);
        if (result.Succeeded && family.Origin == FontOrigin.Online)
        {
            await MergeAsync(ct, family with { Origin = FontOrigin.Downloaded }).ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    private async Task LoadAsync(bool force, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_loaded && !force) return;

            var lists = await Task.WhenAll(_sources.Where(source => !source.IsVolatile).Select(async source =>
            {
                try
                {
                    return (source.Id, Families: await source.ListAsync(ct).ConfigureAwait(false));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return (source.Id, Families: (IReadOnlyList<FontFamilyInfo>)[]);
                }
            })).ConfigureAwait(false);

            _stable = lists.ToDictionary(entry => entry.Id, entry => entry.Families, StringComparer.Ordinal);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }

        await MergeAsync(ct).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task MergeAsync(CancellationToken ct, FontFamilyInfo? promoted = null)
    {
        if (promoted is not null && _stable.TryGetValue(promoted.SourceId, out var list))
            _stable[promoted.SourceId] = [.. list.Select(entry => string.Equals(entry.Family, promoted.Family, StringComparison.OrdinalIgnoreCase) ? promoted : entry)];

        var merged = new Dictionary<string, (FontFamilyInfo Family, int Rank)>(StringComparer.OrdinalIgnoreCase);
        var inDocument = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var markers = new List<FontFamilyInfo>();

        for (var order = 0; order < _sources.Count; order++)
        {
            var source = _sources[order];
            IReadOnlyList<FontFamilyInfo> families = source.IsVolatile
                ? await source.ListAsync(ct).ConfigureAwait(false)
                : _stable.GetValueOrDefault(source.Id, []);

            foreach (var family in families)
            {
                if (family.Origin == FontOrigin.Document) inDocument.Add(family.Family);
                if (family.Origin == FontOrigin.Document && family.Faces.Count == 0)
                {
                    markers.Add(family);
                    continue;
                }

                var rank = ((int)family.Origin * 100) + order;
                if (merged.TryGetValue(family.Family, out var existing) && existing.Rank <= rank) continue;
                merged[family.Family] = (family, rank);
            }
        }

        foreach (var marker in markers.Where(marker => !merged.ContainsKey(marker.Family)))
            merged[marker.Family] = (marker, int.MaxValue);

        _merged = [.. merged.Values
            .Select(entry => inDocument.Contains(entry.Family.Family) ? entry.Family with { InDocument = true } : entry.Family)
            .OrderBy(entry => entry.Family, StringComparer.CurrentCultureIgnoreCase)];
    }
}
