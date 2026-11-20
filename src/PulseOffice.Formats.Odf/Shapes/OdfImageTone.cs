using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Assets;

namespace PulseOffice.Formats.Odf;

public sealed class OdfImageTone
{
    private const double WashoutBrightness = 70;

    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public void Write(XElement graphic, ImageTone tone)
    {
        ArgumentNullException.ThrowIfNull(graphic);
        ArgumentNullException.ThrowIfNull(tone);
        if (Math.Abs(tone.Brightness) > 1e-6) graphic.SetAttributeValue(Draw + "luminance", Percent(tone.Brightness));
        if (Math.Abs(tone.Contrast) > 1e-6) graphic.SetAttributeValue(Draw + "contrast", Percent(tone.Contrast));
        if (tone.Transparency > 1e-6) graphic.SetAttributeValue(Draw + "image-opacity", Percent(tone.Opacity * 100));
    }

    public ImageTone Read(XElement? graphic)
    {
        if (graphic is null) return ImageTone.Neutral;
        var washout = (string?)graphic.Attribute(Draw + "color-mode") == "watermark";
        var opacity = Number((string?)graphic.Attribute(Draw + "image-opacity"));
        var tone = new ImageTone(
            Math.Clamp(Number((string?)graphic.Attribute(Draw + "luminance")) ?? (washout ? WashoutBrightness : 0), -100, 100),
            Math.Clamp(Number((string?)graphic.Attribute(Draw + "contrast")) ?? (washout ? -WashoutBrightness : 0), -100, 100),
            opacity is { } value ? Math.Clamp(100 - value, 0, 100) : 0);
        return tone.IsNeutral ? ImageTone.Neutral : tone;
    }

    private string Percent(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture) + "%";

    private double? Number(string? text) =>
        text is { Length: > 0 } && double.TryParse(text.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value
            : null;
}
