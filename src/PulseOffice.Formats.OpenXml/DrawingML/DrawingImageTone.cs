using PulseOffice.Drawing.Assets;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingImageTone
{
    private const double Whole = 100000;

    public ImageTone Read(D.Blip? blip)
    {
        if (blip is null) return ImageTone.Neutral;
        var luminance = blip.GetFirstChild<D.LuminanceEffect>();
        var alpha = blip.GetFirstChild<D.AlphaModulationFixed>()?.Amount?.Value;
        var brightness = Math.Clamp((luminance?.Brightness?.Value ?? 0) / 1000.0, -100, 100);
        var contrast = Math.Clamp((luminance?.Contrast?.Value ?? 0) / 1000.0, -100, 100);
        var transparency = alpha is { } amount ? Math.Clamp((Whole - amount) / 1000.0, 0, 100) : 0;
        var washout = Math.Abs(brightness - ImageTone.WatermarkBrightness) < 1e-6 && Math.Abs(contrast - ImageTone.WatermarkContrast) < 1e-6;
        var mode = blip.GetFirstChild<D.BiLevel>() is not null ? ImageColorMode.BlackWhite : washout ? ImageColorMode.Watermark : ImageColorMode.Standard;
        var tone = mode == ImageColorMode.Watermark
            ? new ImageTone(0, 0, transparency, mode)
            : new ImageTone(brightness, contrast, transparency, mode);
        return tone.IsNeutral ? ImageTone.Neutral : tone;
    }

    public void Write(D.Blip blip, ImageTone tone)
    {
        ArgumentNullException.ThrowIfNull(blip);
        ArgumentNullException.ThrowIfNull(tone);
        if (tone.Transparency > 1e-6) blip.Append(new D.AlphaModulationFixed { Amount = (int)Math.Round(tone.Opacity * Whole) });
        if (tone.Mode == ImageColorMode.BlackWhite) blip.Append(new D.BiLevel { Threshold = (int)(Whole / 2) });
        if (Math.Abs(tone.EffectiveBrightness) > 1e-6 || Math.Abs(tone.EffectiveContrast) > 1e-6)
            blip.Append(new D.LuminanceEffect { Brightness = Thousandths(tone.EffectiveBrightness), Contrast = Thousandths(tone.EffectiveContrast) });
    }

    private int Thousandths(double percent) => (int)Math.Round(Math.Clamp(double.IsFinite(percent) ? percent : 0, -100, 100) * 1000);
}
