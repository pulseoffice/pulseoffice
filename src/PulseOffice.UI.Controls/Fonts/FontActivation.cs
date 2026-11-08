using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.UI.Controls.Fonts;

public interface IFontActivation
{
    Task<OperationResult<bool>> ActivateAsync(FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default);

    void RegisterDocument(IFontDocument? document);

    Task WarmUpAsync(CancellationToken ct = default);
}

public sealed class FontActivation(IFontCatalog catalog, IRuntimeFonts runtime, PulseOffice.Typography.Sources.IFontCacheStore cache) : IFontActivation
{
    public async Task<OperationResult<bool>> ActivateAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (family.Origin == FontOrigin.System || runtime.Contains(family.Family)) return OperationResult<bool>.Ok(true);
        if (family.Faces.Count == 0) return OperationResult<bool>.Ok(true);

        var acquired = await catalog.AcquireAsync(family, progress, ct).ConfigureAwait(true);
        if (!acquired.Succeeded || acquired.Value is null)
            return OperationResult<bool>.Fail([.. acquired.Diagnostics]);

        if (runtime.Register(acquired.Value) == 0 && !runtime.Contains(family.Family))
            return OperationResult<bool>.Fail([.. acquired.Diagnostics, Diagnostic.Error("FONT050", $"'{family.Family}' could not be loaded as a font.").With(family.Family)]);
        return OperationResult<bool>.Ok(true, acquired.Diagnostics);
    }

    public void RegisterDocument(IFontDocument? document)
    {
        if (document?.Fonts is { HasFiles: true } fonts) runtime.Register(fonts.Files);
    }

    public async Task WarmUpAsync(CancellationToken ct = default)
    {
        var downloaded = await Task.Run(() => cache.Downloaded()
            .Select(entry => cache.Load(entry.SourceId, entry.Family))
            .OfType<IReadOnlyList<EmbeddedFontFile>>()
            .ToList(), ct).ConfigureAwait(true);

        foreach (var files in downloaded) runtime.Register(files);
    }
}
