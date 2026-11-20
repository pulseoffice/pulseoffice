using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Assets;

namespace PulseOffice.Formats.Odf;

public sealed class OdfImageTone
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    private static readonly XNamespace Pulse = "urn:pulseoffice:drawing";

    private static readonly IReadOnlyDictionary<ImageColorMode, string> ColorModes = new Dictionary<ImageColorMode, string>
    {
        [ImageColorMode.BlackWhite] = "mono",
        [ImageColorMode.Watermark] = "watermark",
    };

    public void Write(XElement graphic, ImageTone tone) => Write(graphic, tone, Draw);

    public ImageTone Read(XElement? graphic) => Read(graphic, Draw);

    public void WriteBackground(XElement pageProperties, ImageTone tone)
    {
        ArgumentNullException.ThrowIfNull(pageProperties);
        ArgumentNullException.ThrowIfNull(tone);
        if (!tone.ChangesColours) return;
        if (pageProperties.GetPrefixOfNamespace(Pulse) is null) pageProperties.Add(new XAttribute(XNamespace.Xmlns + "pulsedraw", Pulse.NamespaceName));
        Write(pageProperties, tone with { Transparency = 0 }, Pulse);
    }

    public ImageTone ReadBackground(XElement? pageProperties) => Read(pageProperties, Pulse);

    private void Write(XElement graphic, ImageTone tone, XNamespace space)
    {
        ArgumentNullException.ThrowIfNull(graphic);
        ArgumentNullException.ThrowIfNull(tone);
        if (Math.Abs(tone.Brightness) > 1e-6) graphic.SetAttributeValue(space + "luminance", Percent(tone.Brightness));
        if (Math.Abs(tone.Contrast) > 1e-6) graphic.SetAttributeValue(space + "contrast", Percent(tone.Contrast));
        if (tone.Transparency > 1e-6) graphic.SetAttributeValue(space + "image-opacity", Percent(tone.Opacity * 100));
        if (ColorModes.GetValueOrDefault(tone.Mode) is { } mode) graphic.SetAttributeValue(space + "color-mode", mode);
    }

    private ImageTone Read(XElement? graphic, XNamespace space)
    {
        if (graphic is null) return ImageTone.Neutral;
        var mode = ColorModes.FirstOrDefault(pair => pair.Value == (string?)graphic.Attribute(space + "color-mode")).Key;
        var opacity = Number((string?)graphic.Attribute(space + "image-opacity"));
        var tone = new ImageTone(
            Math.Clamp(Number((string?)graphic.Attribute(space + "luminance")) ?? 0, -100, 100),
            Math.Clamp(Number((string?)graphic.Attribute(space + "contrast")) ?? 0, -100, 100),
            opacity is { } value ? Math.Clamp(100 - value, 0, 100) : 0,
            mode);
        return tone.IsNeutral ? ImageTone.Neutral : tone;
    }

    private string Percent(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture) + "%";

    private double? Number(string? text) =>
        text is { Length: > 0 } && double.TryParse(text.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value
            : null;
}
