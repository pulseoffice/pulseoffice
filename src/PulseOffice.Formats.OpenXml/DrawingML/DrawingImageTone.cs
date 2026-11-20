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
        var tone = new ImageTone(
            Math.Clamp((luminance?.Brightness?.Value ?? 0) / 1000.0, -100, 100),
            Math.Clamp((luminance?.Contrast?.Value ?? 0) / 1000.0, -100, 100),
            alpha is { } amount ? Math.Clamp((Whole - amount) / 1000.0, 0, 100) : 0);
        return tone.IsNeutral ? ImageTone.Neutral : tone;
    }

    public void Write(D.Blip blip, ImageTone tone)
    {
        ArgumentNullException.ThrowIfNull(blip);
        ArgumentNullException.ThrowIfNull(tone);
        if (tone.Transparency > 1e-6) blip.Append(new D.AlphaModulationFixed { Amount = (int)Math.Round(tone.Opacity * Whole) });
        if (tone.ChangesColours)
            blip.Append(new D.LuminanceEffect { Brightness = Thousandths(tone.Brightness), Contrast = Thousandths(tone.Contrast) });
    }

    private int Thousandths(double percent) => (int)Math.Round(Math.Clamp(double.IsFinite(percent) ? percent : 0, -100, 100) * 1000);
}
