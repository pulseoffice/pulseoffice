using PulseOffice.Foundation.Colors;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.DrawingML;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DrawingColourModifiers
{
    private const double Whole = 100000.0;

    public uint Apply(uint argb, OpenXmlElement colour)
    {
        var (red, green, blue) = ((argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
        var (r, g, b) = (red / 255.0, green / 255.0, blue / 255.0);
        var alpha = (argb >> 24) / 255.0;

        foreach (var modifier in colour.ChildElements)
        {
            var value = modifier.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val").Value is { } text
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed / Whole : (double?)null;
            if (value is not { } amount) continue;
            switch (modifier.LocalName)
            {
                case "tint":
                    (r, g, b) = (Tint(r, amount), Tint(g, amount), Tint(b, amount));
                    break;
                case "shade":
                    (r, g, b) = (r * amount, g * amount, b * amount);
                    break;
                case "lumMod":
                case "lumOff":
                    (r, g, b) = Luminance(r, g, b, modifier.LocalName == "lumMod" ? amount : 1, modifier.LocalName == "lumOff" ? amount : 0);
                    break;
                case "alpha":
                    alpha = amount;
                    break;
            }
        }

        return ((uint)Math.Round(Math.Clamp(alpha, 0, 1) * 255) << 24)
            | ((uint)Math.Round(Math.Clamp(r, 0, 1) * 255) << 16)
            | ((uint)Math.Round(Math.Clamp(g, 0, 1) * 255) << 8)
            | (uint)Math.Round(Math.Clamp(b, 0, 1) * 255);
    }

    private double Tint(double channel, double amount) => 1 - (1 - channel) * amount;

    private (double, double, double) Luminance(double r, double g, double b, double factor, double offset)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var delta = max - min;
        var saturation = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * lightness - 1));
        var hue = delta == 0 ? 0
            : max == r ? 60 * (((g - b) / delta % 6 + 6) % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);

        lightness = Math.Clamp(lightness * factor + offset, 0, 1);
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - chroma / 2;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0.0),
            < 120 => (x, chroma, 0.0),
            < 180 => (0.0, chroma, x),
            < 240 => (0.0, x, chroma),
            < 300 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        return (red + m, green + m, blue + m);
    }
}

internal sealed class PptxBackgroundFillReader(IDrawingColorReader colours, DrawingColourModifiers modifiers, DrawingDuotone duotones)
{
    public PulseOffice.Drawing.Assets.ImageDuotone? Duotone(D.Blip? blip) => duotones.Read(blip);

    private const uint FirstBackgroundStyle = 1001;

    public ShapeFill? Read(P.Background? background, ThemePart? theme)
    {
        if (background?.BackgroundProperties is { } properties) return Fill(properties, null);
        if (background?.BackgroundStyleReference is not { Index.Value: >= FirstBackgroundStyle } reference) return null;

        var styles = theme?.Theme?.ThemeElements?.FormatScheme?.BackgroundFillStyleList?.ChildElements.ToList();
        var position = (int)(reference.Index.Value - FirstBackgroundStyle);
        return styles is not null && position < styles.Count && styles[position] is OpenXmlCompositeElement style
            ? Fill(new P.BackgroundProperties(style.CloneNode(true)), Colour(reference))
            : null;
    }

    public OpenXmlElement? FillElement(P.Background? background, ThemePart? theme)
    {
        if (background?.BackgroundProperties is { } properties)
            return properties.ChildElements.FirstOrDefault(child => child is D.SolidFill or D.GradientFill or D.BlipFill or D.PatternFill)?.CloneNode(true);
        if (background?.BackgroundStyleReference is not { Index.Value: >= FirstBackgroundStyle } reference) return null;
        var styles = theme?.Theme?.ThemeElements?.FormatScheme?.BackgroundFillStyleList?.ChildElements.ToList();
        var position = (int)(reference.Index.Value - FirstBackgroundStyle);
        if (styles is null || position >= styles.Count) return null;
        var fill = styles[position].CloneNode(true);
        if (Colour(reference) is { } placeholder)
            foreach (var colour in fill.Descendants<D.SchemeColor>().Where(colour => colour.Val?.Value == D.SchemeColorValues.PhColor).ToList())
                colour.Parent!.ReplaceChild(Rgb(modifiers.Apply(placeholder, colour)), colour);
        return fill;
    }

    private D.RgbColorModelHex Rgb(uint argb)
    {
        var rgb = new D.RgbColorModelHex { Val = new ArgbColor(argb).ToRgbDigits() };
        if (argb >> 24 is var alpha and < 255) rgb.AppendChild(new D.Alpha { Val = (int)Math.Round(alpha / 255.0 * 100000) });
        return rgb;
    }

    public uint? Colour(P.Background? background, ThemePart? theme) =>
        background?.BackgroundProperties?.GetFirstChild<D.SolidFill>() is { } solid ? Colour(solid) : Solid(background, theme);

    public uint? Solid(P.Background? background, ThemePart? theme)
    {
        if (background?.BackgroundProperties is not null || background?.BackgroundStyleReference is not { Index.Value: var index } reference) return null;
        if (index < FirstBackgroundStyle) return Colour(reference);
        var styles = theme?.Theme?.ThemeElements?.FormatScheme?.BackgroundFillStyleList?.ChildElements.ToList();
        var position = (int)(index - FirstBackgroundStyle);
        return styles is not null && position < styles.Count && styles[position] is D.SolidFill solid ? Colour(solid, Colour(reference)) : null;
    }

    private ShapeFill? Fill(OpenXmlCompositeElement properties, uint? placeholder)
    {
        if (properties.GetFirstChild<D.GradientFill>() is { } gradient) return Gradient(gradient, placeholder);
        if (properties.GetFirstChild<D.PatternFill>() is { } pattern) return Pattern(pattern, placeholder);
        return null;
    }

    private ShapeFill? Gradient(D.GradientFill gradient, uint? placeholder)
    {
        var stops = gradient.GradientStopList?.Elements<D.GradientStop>()
            .Select(stop => (Colour: Colour(stop, placeholder), Position: (stop.Position?.Value ?? 0) / 100000.0))
            .Where(stop => stop.Colour is not null)
            .OrderBy(stop => stop.Position)
            .ToList() ?? [];
        if (stops.Count == 0) return null;
        if (stops.Count == 1) stops.Add(stops[0] with { Position = 1 });

        var path = gradient.GetFirstChild<D.PathGradientFill>();
        var linear = gradient.GetFirstChild<D.LinearGradientFill>();
        var centre = path?.FillToRectangle;
        return ShapeFill.Default with
        {
            Kind = ShapeFillKind.Gradient,
            Opacity = (stops[0].Colour!.Value >> 24) / 255.0,
            Gradient = ShapeGradient.Default with
            {
                Kind = path is null ? GradientKind.Linear : path.Path?.InnerText == "rect" ? GradientKind.Rectangular : GradientKind.Radial,
                AngleDegrees = (linear?.Angle?.Value ?? 5400000) / 60000.0,
                Stops = [.. stops.Select(stop => new GradientStop(Hex(stop.Colour!.Value), stop.Position))],
                CentreX = centre is null ? 0.5 : (centre.Left?.Value ?? 0) / 100000.0 + (1 - (centre.Left?.Value ?? 0) / 100000.0 - (centre.Right?.Value ?? 0) / 100000.0) / 2,
                CentreY = centre is null ? 0.5 : (centre.Top?.Value ?? 0) / 100000.0 + (1 - (centre.Top?.Value ?? 0) / 100000.0 - (centre.Bottom?.Value ?? 0) / 100000.0) / 2,
            },
        };
    }

    private ShapeFill? Pattern(D.PatternFill pattern, uint? placeholder)
    {
        if (Colour(pattern.ForegroundColor, placeholder) is not { } foreground) return null;
        return ShapeFill.Default with
        {
            Kind = ShapeFillKind.Hatch,
            Hatch = ShapeHatch.Default with
            {
                ColorHex = Hex(foreground),
                BackgroundHex = Colour(pattern.BackgroundColor, placeholder) is { } back ? Hex(back) : null,
            },
        };
    }

    private uint? Colour(OpenXmlElement? container, uint? placeholder = null)
    {
        if (container is null) return null;
        var element = container.ChildElements.FirstOrDefault(child => child is D.RgbColorModelHex or D.SchemeColor or D.PresetColor or D.SystemColor or D.HslColor or D.RgbColorModelPercentage);
        if (element is null) return null;
        var basis = element is D.SchemeColor { Val.Value: var scheme } && scheme == D.SchemeColorValues.PhColor
            ? placeholder
            : colours.Read(container);
        return basis is { } argb ? modifiers.Apply(argb | 0xFF000000u, element) : null;
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();
}

internal sealed class PptxTextBackdrops(XlsxShapeStyleMapper styles, DrawingPresetGeometry geometry)
{
    private readonly OoxmlGeometry _custom = new();

    public PulseOffice.Presentations.TextBackdrop? Read(IEnumerable<P.Shape> chain)
    {
        var properties = chain.Select(shape => shape.ShapeProperties).OfType<P.ShapeProperties>().ToList();
        var fill = properties.Select(item => item.ChildElements.FirstOrDefault(child => child is D.NoFill or D.SolidFill or D.GradientFill or D.PatternFill or D.BlipFill)).FirstOrDefault(found => found is not null);
        var line = properties.Select(item => item.GetFirstChild<D.Outline>()).FirstOrDefault(found => found is not null && found.ChildElements.Any(child => child is D.NoFill or D.SolidFill or D.GradientFill));
        if (fill is null or D.NoFill && (line is null || line.GetFirstChild<D.NoFill>() is not null)) return null;

        var merged = new P.ShapeProperties();
        if (fill is not null) merged.Append(fill.CloneNode(true));
        merged.Append(line?.CloneNode(true) ?? new D.Outline(new D.NoFill()));
        var style = styles.Read(merged, null);
        var shaped = properties.FirstOrDefault(item => item.GetFirstChild<D.PresetGeometry>() is not null || item.GetFirstChild<D.CustomGeometry>() is not null);
        var preset = shaped?.GetFirstChild<D.PresetGeometry>();
        var kind = geometry.Shaped(new PulseOffice.Drawing.Shapes.ShapeObject { Id = "backdrop", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle }, preset?.Preset?.InnerText ?? "rect", preset).Shape;
        var custom = preset is null ? shaped?.GetFirstChild<D.CustomGeometry>() : null;
        var outline = custom is null ? null : _custom.OutlineOf(custom);
        return new PulseOffice.Presentations.TextBackdrop(kind, fill is null ? PulseOffice.Drawing.Shapes.ShapeFill.None : style.Fill, style.Line)
        {
            Outline = outline is { Figures.Count: > 0 } ? PulseOffice.Drawing.Freeform.FreeformPath.FromOutline(outline) : null,
        };
    }
}
