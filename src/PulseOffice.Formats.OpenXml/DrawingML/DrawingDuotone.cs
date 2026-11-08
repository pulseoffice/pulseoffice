using PulseOffice.Foundation.Colors;
using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Assets;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingDuotone(DrawingColourModifiers modifiers, PresetColors presets, IDrawingColorReader? schemes = null)
{
    public DrawingDuotone() : this(new DrawingColourModifiers(), new PresetColors()) { }

    public ImageDuotone? Read(D.Blip? blip)
    {
        if (blip?.GetFirstChild<D.Duotone>() is not { } duotone) return null;
        var colours = duotone.ChildElements.Select(Colour).ToList();
        return colours.Count == 2 && colours[0] is { } dark && colours[1] is { } light ? new ImageDuotone(Hex(dark), Hex(light)) : null;
    }

    public void Write(D.Blip blip, ImageDuotone? duotone)
    {
        ArgumentNullException.ThrowIfNull(blip);
        if (duotone is null) return;
        blip.Append(new D.Duotone(Rgb(duotone.DarkHex), Rgb(duotone.LightHex)));
    }

    private uint? Colour(OpenXmlElement element)
    {
        var basis = element switch
        {
            D.RgbColorModelHex { Val.Value: { Length: 6 } hex } => Parsed(hex),
            D.PresetColor preset => presets.Of(preset.Val?.InnerText),
            D.SystemColor { LastColor.Value: { Length: 6 } last } => Parsed(last),
            D.SchemeColor => schemes?.Read(new D.SolidFill(element.CloneNode(false))),
            _ => null,
        };
        return basis is { } argb ? modifiers.Apply(argb | 0xFF000000u, element) : null;
    }

    private uint? Parsed(string hex) =>
        uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) ? value : null;

    private D.RgbColorModelHex Rgb(string hex)
    {
        var digits = hex.TrimStart('#').PadLeft(6, '0')[^6..].ToUpperInvariant();
        return new() { Val = digits.All(char.IsAsciiHexDigit) ? digits : "000000" };
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();
}
