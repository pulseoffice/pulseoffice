using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Objects;

public sealed record RecolorResult(RasterImageObject Image, int Replaced);

public interface IRasterRecolor
{
    Task<RecolorResult?> AskAsync(RasterImageObject raster, string titleKey, CancellationToken ct);

    RecolorResult? Apply(RasterImageObject raster, IReadOnlyList<ColorReplacementRule> rules);
}

public sealed class RasterRecolor(IBitmapPixels pixels, IDialogService dialogs, ITextCatalog texts) : IRasterRecolor
{
    public async Task<RecolorResult?> AskAsync(RasterImageObject raster, string titleKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(raster);

        var rules = await dialogs.ShowAsync<IReadOnlyList<ColorReplacementRule>>(
            new ColorReplacerView(raster.Data.ToArray(), pixels, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 860 },
            ct).ConfigureAwait(true);

        return rules is { Count: > 0 } ? Apply(raster, rules) : null;
    }

    public RecolorResult? Apply(RasterImageObject raster, IReadOnlyList<ColorReplacementRule> rules)
    {
        ArgumentNullException.ThrowIfNull(raster);
        if (pixels.Decode(raster.Data.ToArray()) is not { } bitmap) return null;

        var filter = new ColorReplacementFilter(rules);
        var edited = pixels.Edit(bitmap, filter.Apply);
        if (filter.Replaced == 0) return new RecolorResult(raster, 0);

        return new RecolorResult(raster with { Data = pixels.EncodePng(edited), MediaType = "image/png" }, filter.Replaced);
    }
}

public interface IRasterColorAdjust
{
    Task<RasterImageObject?> AskAsync(RasterImageObject raster, string titleKey, CancellationToken ct);
}

public sealed class RasterColorAdjust(
    IBitmapPixels pixels, IDialogService dialogs, ITextCatalog texts, IColorCorrectionPresets presets,
    Galleries.IGalleryNavigator navigator, Fields.IMeasureUnits units) : IRasterColorAdjust
{
    public async Task<RasterImageObject?> AskAsync(RasterImageObject raster, string titleKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(raster);
        if (pixels.Decode(raster.Data.ToArray()) is not { } bitmap) return null;

        var adjustment = await dialogs.ShowAsync<ColorAdjustment>(
            new ColorCorrectionView(bitmap, pixels, presets, navigator, units, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 900 }, ct).ConfigureAwait(true);
        if (adjustment is null || adjustment.IsIdentity) return null;

        var edited = pixels.Edit(bitmap, new ColorAdjustmentFilter(adjustment).Apply);
        return raster with { Data = pixels.EncodePng(edited), MediaType = "image/png" };
    }
}
