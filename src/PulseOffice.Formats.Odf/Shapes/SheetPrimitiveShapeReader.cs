using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal abstract class SheetPrimitiveShapeReader : SheetShapeReader
{
    private readonly XName _name;
    private readonly IOdfParagraphText _paragraphs;
    private readonly OdfShapeTextRuns _links;

    protected SheetPrimitiveShapeReader(string localName, IOdfParagraphText paragraphs)
    {
        _name = Namespaces.Draw + localName;
        _paragraphs = paragraphs;
        _links = new OdfShapeTextRuns(paragraphs);
    }

    public override bool Reads(XElement element) => element.Name == _name;

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context)
    {
        var ordinal = context.Next();
        var text = _paragraphs.Read(TextSource(element));
        var frame = Frame(element, context);
        var shape = Shaped(_links.Read(TextSource(element), new ShapeObject
        {
            Id = (string?)element.Attribute(Namespaces.Draw + "name") ?? $"shape{ordinal}",
            Shape = KindOf(element),
            FlipHorizontal = frame.Flip,
            Text = text.Length > 0 ? text : null,
            Style = Styled(context.Style((string?)element.Attribute(Namespaces.Draw + "style-name")), element, context),
        }, context.TextStyle, context.Lists), element);
        return Ordered(new PlacedObject
        {
            Id = shape.Id,
            Content = shape with { RotationDegrees = frame.Degrees },
            Anchor = anchor,
            OffsetX = frame.X,
            OffsetY = frame.Y,
            Size = frame.Size,
        }, element, context);
    }

    protected abstract ShapeKind KindOf(XElement element);

    protected virtual ShapeObject Shaped(ShapeObject shape, XElement element) => shape;

    protected virtual XElement TextSource(XElement element) => element;

    protected virtual ShapeStyle Styled(ShapeStyle style, XElement element, SheetShapeReadContext context) => style;

    protected virtual PrimitiveFrame Frame(XElement element, SheetShapeReadContext context)
    {
        var units = context.Units;
        var size = new DocumentSize(
            units.Parse((string?)element.Attribute(Namespaces.Svg + "width"), 4),
            units.Parse((string?)element.Attribute(Namespaces.Svg + "height"), 3));
        var position = context.Rotation.Read(element, size.Width, size.Height);
        return new PrimitiveFrame(position.X, position.Y, size, position.Degrees, false);
    }
}

internal readonly record struct PrimitiveFrame(Twip X, Twip Y, DocumentSize Size, double Degrees, bool Flip);

internal sealed class SheetRectangleReader(IOdfParagraphText paragraphs) : SheetPrimitiveShapeReader("rect", paragraphs)
{
    protected override ShapeKind KindOf(XElement element) =>
        new OdfLength().Parse((string?)element.Attribute(Namespaces.Draw + "corner-radius"), Twip.Zero).Value > 0
            ? ShapeKind.RoundedRectangle
            : ShapeKind.Rectangle;
}

internal sealed class SheetTextFrameReader(IOdfParagraphText paragraphs) : SheetPrimitiveShapeReader("frame", paragraphs)
{
    private readonly XName _textBox = Namespaces.Draw + "text-box";

    public override bool Reads(XElement element) =>
        base.Reads(element) && element.Element(_textBox) is not null && element.Element(Namespaces.Draw + "custom-shape") is null;

    protected override ShapeKind KindOf(XElement element) => ShapeKind.Rectangle;

    protected override XElement TextSource(XElement element) => element.Element(_textBox)!;

    private readonly XName _graphic = Namespaces.Style + "graphic-properties";

    protected override ShapeStyle Styled(ShapeStyle style, XElement element, SheetShapeReadContext context)
    {
        var name = (string?)element.Attribute(Namespaces.Draw + "style-name");
        var text = style.Text;
        if (!context.Declares(name, Namespaces.Style + "text-properties", Namespaces.Fo + "color")) text = text with { AutomaticColor = true };
        if (!context.Declares(name, _graphic, Namespaces.Draw + "textarea-horizontal-align")) text = text with { Horizontal = TextHorizontalPlacement.Left };
        if (!context.Declares(name, _graphic, Namespaces.Draw + "textarea-vertical-align")) text = text with { Vertical = TextVerticalPlacement.Top };
        if (!context.Declares(name, _graphic, Namespaces.Draw + "fill")) style = style with { Fill = ShapeFill.None };
        if (!context.Declares(name, _graphic, Namespaces.Draw + "stroke")) style = style with { Line = ShapeLine.None };
        return style with { Text = text };
    }
}

internal sealed class SheetEllipseReader(string localName, IOdfParagraphText paragraphs) : SheetPrimitiveShapeReader(localName, paragraphs)
{
    private readonly OdfArcGeometry _arcs = new();

    protected override ShapeKind KindOf(XElement element) => _arcs.KindOfEllipse(element);

    protected override ShapeObject Shaped(ShapeObject shape, XElement element) => _arcs.FromEllipse(shape, element);
}

internal class SheetLineReader(string localName, IOdfParagraphText paragraphs) : SheetPrimitiveShapeReader(localName, paragraphs)
{
    protected override ShapeKind KindOf(XElement element) => ShapeKind.Line;

    protected override PrimitiveFrame Frame(XElement element, SheetShapeReadContext context)
    {
        var units = context.Units;
        var x1 = units.Parse((string?)element.Attribute(Namespaces.Svg + "x1"));
        var y1 = units.Parse((string?)element.Attribute(Namespaces.Svg + "y1"));
        var x2 = units.Parse((string?)element.Attribute(Namespaces.Svg + "x2"));
        var y2 = units.Parse((string?)element.Attribute(Namespaces.Svg + "y2"));
        var size = new DocumentSize(new Twip(Math.Abs(x2.Value - x1.Value)), new Twip(Math.Abs(y2.Value - y1.Value)));
        return new PrimitiveFrame(
            new Twip(Math.Min(x1.Value, x2.Value)), new Twip(Math.Min(y1.Value, y2.Value)), size, 0, (x2.Value < x1.Value) != (y2.Value < y1.Value));
    }
}

internal sealed class SheetDimensionLineReader(IOdfParagraphText paragraphs) : SheetLineReader("measure", paragraphs)
{
    private readonly OdfDimensionLines _dimensions = new();

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context)
    {
        if (base.Read(element, anchor, context) is not { Content: ShapeObject shape } placed) return null;
        var line = Frame(element, context);
        var (bounds, dimension) = _dimensions.Read(element, shape, context.GraphicStyle);
        return placed with
        {
            Content = dimension with { RotationDegrees = 0 },
            OffsetX = new Twip(placed.OffsetX.Value + bounds.Left.Value - line.X.Value),
            OffsetY = new Twip(placed.OffsetY.Value + bounds.Top.Value - line.Y.Value),
            Size = new DocumentSize(bounds.Width, bounds.Height),
        };
    }
}
