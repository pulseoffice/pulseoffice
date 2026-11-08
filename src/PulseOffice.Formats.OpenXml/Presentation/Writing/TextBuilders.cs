using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class TextShapeBuilder(OoxmlWriteUnits units, ShapeTreeFactory trees, ParagraphBuilder paragraphs, BodyPropertiesBuilder bodies)
{
    public P.Shape Build(uint id, TextElement element, Theme theme, IDrawingRunLinks links)
    {
        var body = new P.TextBody(bodies.Build(element.Text.Frame), new D.ListStyle());
        foreach (var paragraph in element.Text.Paragraphs) body.Append(paragraphs.Build(paragraph, theme, links));
        if (element.Text.Paragraphs.Count == 0) body.Append(new D.Paragraph());

        var type = element.Text.Role == PlaceholderRole.Subtitle ? P.PlaceholderValues.SubTitle
            : element.Text.Role == PlaceholderRole.Title ? P.PlaceholderValues.Title
            : P.PlaceholderValues.Body;
        var shape = element.Text.Role == PlaceholderRole.Free
            ? trees.TextBox(id, units.Transform(element.Bounds), body)
            : trees.Placeholder(id, element.Text.Role, type, units.Transform(element.Bounds), body);
        if (element.Backdrop is { } backdrop && shape.ShapeProperties is { } properties)
        {
            properties.GetFirstChild<D.PresetGeometry>()?.Remove();
            properties.GetFirstChild<D.NoFill>()?.Remove();
            properties.Append(backdrop.Outline is { } outline ? _custom.CustomGeometry(outline.ToOutline()) : _geometry.Build(backdrop.AsShape(element.Id)));
            properties.Append(_styles.Fill(backdrop.Fill));
            properties.Append(_styles.Line(backdrop.Line));
        }
        return shape;
    }

    private readonly XlsxShapeStyleMapper _styles = new();

    private readonly DrawingPresetGeometry _geometry = new();

    private readonly OoxmlGeometry _custom = new();
}

internal sealed class SeedListStyleBuilder(OoxmlWriteUnits units)
{
    private const int MaximumIndentEmu = 51206400;

    public D.ListStyle Build(TextParagraph seed)
    {
        var defaults = new D.DefaultRunProperties
        {
            FontSize = (int)Math.Clamp(double.IsFinite(seed.FontSize) ? Math.Round(seed.FontSize * 100) : 1800, 100, 400000),
            Bold = seed.Bold,
            Italic = seed.Italic,
        };
        if (seed.ColorRole is { } role) defaults.Append(new D.SolidFill(new D.SchemeColor { Val = units.Scheme(role) }));
        else if (seed.Color is { } argb) defaults.Append(new D.SolidFill(new D.RgbColorModelHex { Val = units.Hex(argb) }));
        defaults.Append(new D.LatinFont { Typeface = seed.MajorFont ? "+mj-lt" : "+mn-lt" });

        var level = new D.Level1ParagraphProperties(
            seed.Bullet ? new D.CharacterBullet { Char = "•" } : new D.NoBullet(),
            defaults)
        {
            Alignment = units.Align(seed.Align),
        };
        if (seed.MarginLeft is { } margin && double.IsFinite(margin)) level.LeftMargin = (int)Math.Clamp(Math.Round(margin * 12700), 0, MaximumIndentEmu);
        if (seed.Indent is { } indent && double.IsFinite(indent)) level.Indent = (int)Math.Clamp(Math.Round(indent * 12700), -MaximumIndentEmu, MaximumIndentEmu);
        return new D.ListStyle(level);
    }
}
