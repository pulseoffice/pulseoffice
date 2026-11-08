using PulseOffice.Foundation.Colors;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DrawingShapeColourNormalizer(IDrawingColorReader colours, DrawingColourModifiers modifiers)
{
    private const int ThemeStyleBase = 1;
    private const int BackgroundStyleBase = 1000;

    public void Normalize(OpenXmlElement? tree, ThemePart? theme, Func<OpenXmlElement?> background)
    {
        if (tree is null) return;
        var scheme = theme?.Theme?.ThemeElements?.FormatScheme;

        foreach (var shape in tree.Descendants().Where(element => element is P.Shape or P.ConnectionShape or P.Picture or WPS.WordprocessingShape
                     or Xdr.Shape or Xdr.ConnectionShape or Xdr.Picture).ToList())
        {
            var properties = shape.ChildElements.OfType<OpenXmlCompositeElement>().FirstOrDefault(child => child is P.ShapeProperties or WPS.ShapeProperties or Xdr.ShapeProperties);
            if (properties is null) continue;
            var style = shape.ChildElements.FirstOrDefault(child => child is P.ShapeStyle or WPS.ShapeStyle or Xdr.ShapeStyle);

            if (shape is P.Shape { UseBackgroundFill.Value: true } && !HasFill(properties) && background() is { } fill and not D.BlipFill)
                properties.InsertAfter(fill, Geometry(properties));
            else if (shape is not (P.Picture or Xdr.Picture) && style?.GetFirstChild<D.FillReference>() is { } fillReference && !HasFill(properties))
                ApplyFill(properties, fillReference, scheme?.FillStyleList?.ChildElements.ToList(), scheme?.BackgroundFillStyleList?.ChildElements.ToList());

            if (style?.GetFirstChild<D.LineReference>() is { } lineReference) ApplyLine(properties, lineReference, scheme?.LineStyleList?.Elements<D.Outline>().ToList());
            if (style is null && !Placeholder(shape)) Unstyled(shape, properties);

            Resolve(properties);
            if (shape is WPS.WordprocessingShape && style?.GetFirstChild<D.FontReference>() is { } fontReference) Resolve(fontReference);
        }
    }

    private void Unstyled(OpenXmlElement shape, OpenXmlCompositeElement properties)
    {
        if (shape is not (P.Picture or Xdr.Picture) && !HasFill(properties) && !(shape is P.Shape { UseBackgroundFill.Value: true }))
            properties.InsertAfter(new D.NoFill(), Geometry(properties));
        if (properties.GetFirstChild<D.Outline>() is null)
            properties.InsertAfter(new D.Outline(new D.NoFill()), properties.ChildElements.LastOrDefault(IsFill) ?? Geometry(properties));
    }

    private bool Placeholder(OpenXmlElement shape) =>
        shape.Descendants<P.PlaceholderShape>().Any();

    private void ApplyFill(OpenXmlCompositeElement properties, D.FillReference reference, List<OpenXmlElement>? styles, List<OpenXmlElement>? backgroundStyles)
    {
        var index = (int)Math.Min(reference.Index?.Value ?? 0, int.MaxValue);
        var anchor = Geometry(properties);
        if (index > BackgroundStyleBase)
        {
            styles = backgroundStyles;
            index -= BackgroundStyleBase;
        }
        if (index < ThemeStyleBase)
        {
            properties.InsertAfter(new D.NoFill(), anchor);
            return;
        }
        if (styles is null || index - ThemeStyleBase >= styles.Count) return;
        var fill = styles[index - ThemeStyleBase].CloneNode(true);
        Substitute(fill, Colour(reference));
        properties.InsertAfter(fill, anchor);
    }

    private void ApplyLine(OpenXmlCompositeElement properties, D.LineReference reference, List<D.Outline>? styles)
    {
        var index = (int)Math.Min(reference.Index?.Value ?? 0, int.MaxValue);
        var outline = properties.GetFirstChild<D.Outline>();
        if (outline is not null && HasFill(outline)) return;
        if (index < ThemeStyleBase)
        {
            if (outline is null) properties.Append(new D.Outline(new D.NoFill()));
            return;
        }
        if (styles is null || index - ThemeStyleBase >= styles.Count) return;

        var themed = (D.Outline)styles[index - ThemeStyleBase].CloneNode(true);
        Substitute(themed, Colour(reference));
        if (outline is null)
        {
            properties.Append(themed);
            return;
        }
        if (outline.Width is null && themed.Width is not null) outline.Width = themed.Width.Value;
        if (themed.ChildElements.FirstOrDefault(IsFill) is { } fill) outline.PrependChild(fill.CloneNode(true));
        if (outline.GetFirstChild<D.PresetDash>() is null && themed.GetFirstChild<D.PresetDash>() is { } dash)
            outline.InsertAfter(dash.CloneNode(true), outline.ChildElements.FirstOrDefault(IsFill));
    }

    private void Substitute(OpenXmlElement fill, uint? placeholder)
    {
        foreach (var colour in fill.Descendants<D.SchemeColor>().Where(colour => colour.Val?.Value == D.SchemeColorValues.PhColor).ToList())
        {
            if (placeholder is not { } argb) continue;
            colour.Parent!.ReplaceChild(Rgb(modifiers.Apply(argb | 0xFF000000u, colour)), colour);
        }
    }

    private void Resolve(OpenXmlElement properties)
    {
        foreach (var colour in properties.Descendants().Where(IsIndirectColour).ToList())
        {
            if (colour is D.SchemeColor { Val.Value: var value } && value == D.SchemeColorValues.PhColor) continue;
            var container = colour.Parent!;
            if (Read(container, colour) is not { } argb) continue;
            container.ReplaceChild(Rgb(modifiers.Apply(argb | 0xFF000000u, colour)), colour);
        }
        foreach (var direct in properties.Descendants<D.RgbColorModelHex>().Where(colour => colour.ChildElements.Any(child => child is not D.Alpha)).ToList())
            if (uint.TryParse(direct.Val?.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                direct.Parent!.ReplaceChild(Rgb(modifiers.Apply(rgb | 0xFF000000u, direct)), direct);
    }

    private uint? Read(OpenXmlElement container, OpenXmlElement colour)
    {
        var probe = (OpenXmlElement)container.CloneNode(false);
        probe.AppendChild(colour.CloneNode(false));
        return colours.Read(probe);
    }

    private uint? Colour(OpenXmlElement reference)
    {
        var colour = reference.ChildElements.FirstOrDefault(child => IsIndirectColour(child) || child is D.RgbColorModelHex);
        if (colour is null || Read(reference, colour) is not { } argb) return null;
        return modifiers.Apply(argb | 0xFF000000u, colour);
    }

    private D.RgbColorModelHex Rgb(uint argb)
    {
        var rgb = new D.RgbColorModelHex { Val = new ArgbColor(argb).ToRgbDigits() };
        if (argb >> 24 is var alpha and < 255) rgb.AppendChild(new D.Alpha { Val = (int)Math.Round(alpha / 255.0 * 100000) });
        return rgb;
    }

    private bool IsIndirectColour(OpenXmlElement element) =>
        element is D.SchemeColor or D.PresetColor or D.SystemColor;

    private bool IsFill(OpenXmlElement element) =>
        element is D.NoFill or D.SolidFill or D.GradientFill or D.BlipFill or D.PatternFill or D.GroupFill;

    private bool HasFill(OpenXmlElement properties) => properties.ChildElements.Any(IsFill);

    private OpenXmlElement? Geometry(OpenXmlCompositeElement properties) =>
        (OpenXmlElement?)properties.GetFirstChild<D.PresetGeometry>()
        ?? (OpenXmlElement?)properties.GetFirstChild<D.CustomGeometry>()
        ?? properties.GetFirstChild<D.Transform2D>();
}
