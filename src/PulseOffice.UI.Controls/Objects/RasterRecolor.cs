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

    Task<RasterImageObject?> ToneAsync(RasterImageObject raster, string titleKey, CancellationToken ct);
}

public sealed class RasterColorAdjust(
    IBitmapPixels pixels, IDialogService dialogs, ITextCatalog texts, IColorCorrectionPresets presets,
    Galleries.IGalleryNavigator navigator, Fields.IMeasureUnits units, IPixelFilterCatalog filters) : IRasterColorAdjust
{
    public async Task<RasterImageObject?> AskAsync(RasterImageObject raster, string titleKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(raster);
        if (pixels.Decode(raster.Data.ToArray()) is not { } bitmap) return null;

        var view = new ColorCorrectionView(bitmap, pixels, presets, navigator, units, texts);
        view.Apply(new ColorAdjustment(raster.Tone.Brightness, raster.Tone.Contrast));
        var adjustment = await dialogs.ShowAsync<ColorAdjustment>(
            view, new DialogOptions { TitleKey = titleKey, PreferredWidth = 900 }, ct).ConfigureAwait(true);
        if (adjustment is null) return null;

        if (IsTone(adjustment))
        {
            var tone = raster.Tone with { Brightness = adjustment.Brightness, Contrast = adjustment.Contrast };
            return tone == raster.Tone ? null : raster with { Tone = tone.IsNeutral ? ImageTone.Neutral : tone };
        }

        var edited = pixels.Edit(bitmap, new ColorAdjustmentFilter(adjustment).Apply);
        return raster with { Data = pixels.EncodePng(edited), MediaType = "image/png" };
    }

    public async Task<RasterImageObject?> ToneAsync(RasterImageObject raster, string titleKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(raster);
        if (pixels.Decode(raster.Data.ToArray()) is not { } bitmap) return null;

        var tone = await dialogs.ShowAsync<ImageTone>(
            new ImageToneView(bitmap, raster.Tone, pixels, filters, units, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 820 }, ct).ConfigureAwait(true);
        return tone is null || tone == raster.Tone ? null : raster with { Tone = tone };
    }

    private bool IsTone(ColorAdjustment adjustment) =>
        adjustment.Red == 0 && adjustment.Green == 0 && adjustment.Blue == 0 && Math.Abs(adjustment.Gamma - 1) < 1e-9;
}
