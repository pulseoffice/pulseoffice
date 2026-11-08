using DocumentFormat.OpenXml;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.OpenXml.DrawingML;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DrawingIdentifiers
{
    private readonly Dictionary<string, uint> _byName = new(StringComparer.Ordinal);
    private uint _last = 1;

    public uint Next() => ++_last;

    public uint Register(string name) => _byName[name] = Next();

    public uint? Of(string? name) => name is not null && _byName.TryGetValue(name, out var id) ? id : null;
}

internal readonly record struct DrawingBox(long X, long Y, long Width, long Height)
{
    public DrawingBox Child(GroupChild child)
    {
        var (x, y, width, height) = child.Scaled(Width, Height);
        return new DrawingBox(X + (long)Math.Round(x), Y + (long)Math.Round(y), Math.Max(1, (long)Math.Round(width)), Math.Max(1, (long)Math.Round(height)));
    }

    public A.Offset Offset => new() { X = X, Y = Y };

    public A.Extents Extents => new() { Cx = Width, Cy = Height };
}

internal sealed class SheetDrawingVisitor(
    XlsxShapeStyleMapper styles, IShapePresets presets, DrawingIdentifiers ids, string name, uint id, DrawingBox box, IDrawingRunLinks links, double extraRotation = 0,
    IGroupPictureBlips? groupPictures = null)
    : DrawingObjectVisitor<OpenXmlElement?>
{
    private readonly DrawingShapeTextRuns _textRuns = new();
    private readonly DrawingPresetGeometry _presetGeometry = new(presets);
    private readonly DrawingDimensionLines _dimensions = new();
    private readonly OoxmlVectorPaths _paths = new();
    private readonly DrawingColours _colours = new();

    public override OpenXmlElement? VisitShape(ShapeObject original)
    {
        var shape = _dimensions.Fallback(original, new DocumentSize(new Emu(box.Width).ToTwips(), new Emu(box.Height).ToTwips()));
        var properties = new Xdr.ShapeProperties(
            new A.Transform2D(box.Offset, box.Extents)
            {
                Rotation = Rotation(shape.RotationDegrees + extraRotation),
                HorizontalFlip = shape.FlipHorizontal ? true : null,
                VerticalFlip = shape.FlipVertical ? true : null,
            },
            original.Dimension is { } dimension ? _dimensions.Geometry(shape, dimension, original.Style.Line) : _presetGeometry.Build(shape));

        properties.AppendChild(styles.Fill(shape.Style.Fill));
        properties.AppendChild(styles.Line(shape.Style.Line));
        if (styles.Effects(shape.Style) is { } effects) properties.AppendChild(effects);
        if (styles.Scene(shape.Style) is { } scene) properties.AppendChild(scene);
        if (styles.Bevel(shape.Style) is { } bevel) properties.AppendChild(bevel);
        _dimensions.Write(properties, original);

        var element = new Xdr.Shape(
            new Xdr.NonVisualShapeProperties(
                new Xdr.NonVisualDrawingProperties { Id = id, Name = name },
                new Xdr.NonVisualShapeDrawingProperties()),
            properties);

        if (!string.IsNullOrEmpty(shape.Text))
            element.AppendChild(new Xdr.TextBody(
                styles.Body(shape.Style.Text),
                new A.ListStyle(),
                new A.Paragraph(styles.Paragraph(shape.Style.Text), new A.Run(styles.Run(shape.Style.Text), new A.Text(shape.Text)))));

        return _textRuns.Write(element, shape, links);
    }

    public override OpenXmlElement? VisitFreeform(PulseOffice.Drawing.Freeform.FreeformObject freeform)
    {
        var properties = new Xdr.ShapeProperties(
            new A.Transform2D(box.Offset, box.Extents) { Rotation = Rotation(freeform.RotationDegrees + extraRotation) },
            _paths.Geometry(freeform.Path.ToOutline()));
        properties.AppendChild(styles.Fill(freeform.Style.Fill));
        properties.AppendChild(styles.Line(freeform.Style.Line));
        if (styles.Effects(freeform.Style) is { } effects) properties.AppendChild(effects);

        return new Xdr.Shape(
            new Xdr.NonVisualShapeProperties(
                new Xdr.NonVisualDrawingProperties { Id = id, Name = name },
                new Xdr.NonVisualShapeDrawingProperties()),
            properties);
    }

    public override OpenXmlElement? VisitFontwork(PulseOffice.Drawing.Fontwork.FontworkObject fontwork)
    {
        var text = new OoxmlFontworkText(styles);
        return new Xdr.Shape(
            new Xdr.NonVisualShapeProperties(
                new Xdr.NonVisualDrawingProperties { Id = id, Name = name },
                new Xdr.NonVisualShapeDrawingProperties()),
            new Xdr.ShapeProperties(
                new A.Transform2D(box.Offset, box.Extents),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                new A.NoFill(),
                new A.Outline(new A.NoFill())),
            new Xdr.TextBody(text.Body(fontwork), new A.ListStyle(), text.Paragraph(fontwork)));
    }

    public override OpenXmlElement? VisitGroup(ShapeGroupObject group)
    {
        var element = new Xdr.GroupShape(
            new Xdr.NonVisualGroupShapeProperties(
                new Xdr.NonVisualDrawingProperties { Id = ids.Next(), Name = name },
                new Xdr.NonVisualGroupShapeDrawingProperties()),
            new Xdr.GroupShapeProperties(
                new A.TransformGroup(box.Offset, box.Extents, new A.ChildOffset { X = box.X, Y = box.Y }, new A.ChildExtents { Cx = box.Width, Cy = box.Height })
                {
                    Rotation = Rotation(group.RotationDegrees + extraRotation),
                }));

        foreach (var child in group.Children)
        {
            if (child.Content is not IDrawingObject) continue;

            var childId = child.Content is ShapeObject or PulseOffice.Drawing.Freeform.FreeformObject or PulseOffice.Drawing.Assets.RasterImageObject ? ids.Next() : 0u;
            var visitor = new SheetDrawingVisitor(styles, presets, ids, child.Content.Id, childId, box.Child(child), links, child.RotationDegrees, groupPictures);
            if (((IDrawingObject)child.Content).Accept(visitor) is { } built) element.AppendChild(built);
        }
        return element;
    }

    public override OpenXmlElement? VisitConnector(ConnectorObject connector)
    {
        var visual = new Xdr.NonVisualConnectorShapeDrawingProperties();
        if (ids.Of(connector.Start.ObjectId) is { } from) visual.AppendChild(new A.StartConnection { Id = from, Index = 2U });
        if (ids.Of(connector.End.ObjectId) is { } to) visual.AppendChild(new A.EndConnection { Id = to, Index = 0U });

        var line = new A.Outline { Width = (int)Emu.FromTwips(connector.Style.Line.Width).Value };
        line.AppendChild(new A.SolidFill(_colours.Of(connector.Style.Line.ColorHex, 1)));
        if (connector.StartArrow is not ArrowEnd.None) line.AppendChild(new A.HeadEnd { Type = new A.LineEndValues(styles.Arrow(connector.StartArrow)) });
        if (connector.EndArrow is not ArrowEnd.None) line.AppendChild(new A.TailEnd { Type = new A.LineEndValues(styles.Arrow(connector.EndArrow)) });

        return new Xdr.ConnectionShape(
            new Xdr.NonVisualConnectionShapeProperties(new Xdr.NonVisualDrawingProperties { Id = id, Name = name }, visual),
            new Xdr.ShapeProperties(
                new A.Transform2D(new A.Offset { X = 0, Y = 0 }, box.Extents),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = new A.ShapeTypeValues(styles.Connector(connector.Shape)) },
                line));
    }

    public override OpenXmlElement? VisitRaster(PulseOffice.Drawing.Assets.RasterImageObject raster) =>
        groupPictures?.Blip(raster) is not { } blip ? null : new Xdr.Picture(
            new Xdr.NonVisualPictureProperties(
                new Xdr.NonVisualDrawingProperties { Id = id, Name = name },
                new Xdr.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
            new Xdr.BlipFill(blip, new A.Stretch(new A.FillRectangle())),
            new Xdr.ShapeProperties(
                new A.Transform2D(box.Offset, box.Extents) { Rotation = Rotation(extraRotation) },
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));

    protected override OpenXmlElement? Otherwise(EmbeddedObject content) => null;

    private int Rotation(double degrees) =>
        double.IsFinite(degrees) ? (int)(((long)Math.Round(degrees * 60000) % 21600000 + 21600000) % 21600000) : 0;
}

internal interface IGroupPictureBlips
{
    A.Blip? Blip(PulseOffice.Drawing.Assets.RasterImageObject raster);

    EmbeddedObject? Read(Xdr.Picture picture, string name);
}

internal sealed class SheetReadContext(XlsxShapeStyleMapper styles, IShapePresets presets, IReadOnlyDictionary<uint, string> names, IDrawingRunLinks links,
    IGroupPictureBlips? pictures = null)
{
    public IGroupPictureBlips? Pictures => pictures;

    public IDrawingRunLinks Links => links;

    public DrawingShapeTextRuns TextRuns { get; } = new();

    private int _ordinal;

    public XlsxShapeStyleMapper Styles => styles;
    public IShapePresets Presets => presets;

    public DrawingPresetGeometry Geometry { get; } = new(presets);

    public int Next() => ++_ordinal;

    public string? Name(uint? id) => id is { } value && names.TryGetValue(value, out var name) ? name : null;
}

internal abstract class SheetDrawingReader
{
    public abstract EmbeddedObject? Read(OpenXmlElement element, SheetReadContext context);
}

internal sealed class SheetFontworkReader(OoxmlFontworkText text) : SheetDrawingReader
{
    public override EmbeddedObject? Read(OpenXmlElement element, SheetReadContext context)
    {
        if (element is not Xdr.Shape shape || shape.GetFirstChild<Xdr.TextBody>() is not { } body || !text.Reads(body)) return null;
        var name = shape.GetFirstChild<Xdr.NonVisualShapeProperties>()?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value;
        return text.Read(body, name ?? $"fontwork{context.Next()}");
    }
}

internal sealed class SheetShapeElementReader : SheetDrawingReader
{
    private readonly DrawingDimensionLines _dimensions = new();

    public override EmbeddedObject? Read(OpenXmlElement element, SheetReadContext context)
    {
        if (element is not Xdr.Shape shape) return null;

        var properties = shape.GetFirstChild<Xdr.ShapeProperties>();
        var preset = properties?.GetFirstChild<A.PresetGeometry>()?.Preset?.InnerText;
        var dimension = _dimensions.Carries(properties);
        if (properties is null || preset is null && !dimension) return null;

        var transform = properties.GetFirstChild<A.Transform2D>();
        var body = shape.GetFirstChild<Xdr.TextBody>();
        var name = shape.GetFirstChild<Xdr.NonVisualShapeProperties>()?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value;

        var read = context.TextRuns.Read(body, new ShapeObject
        {
            Id = name ?? $"shape{context.Next()}",
            Shape = ShapeKind.Rectangle,
            RotationDegrees = (transform?.Rotation?.Value ?? 0) / 60000.0,
            FlipHorizontal = transform?.HorizontalFlip?.Value ?? false,
            FlipVertical = transform?.VerticalFlip?.Value ?? false,
            Text = body?.InnerText is { Length: > 0 } text ? text : null,
            Style = context.Styles.Read(properties, body),
        }, context.Links);
        return dimension ? _dimensions.Restore(read, properties) : context.Geometry.Shaped(read, preset, properties.GetFirstChild<A.PresetGeometry>());
    }
}

internal sealed class SheetConnectorReader : SheetDrawingReader
{
    public override EmbeddedObject? Read(OpenXmlElement element, SheetReadContext context)
    {
        if (element is not Xdr.ConnectionShape connection) return null;

        var visual = connection.GetFirstChild<Xdr.NonVisualConnectionShapeProperties>();
        var properties = connection.GetFirstChild<Xdr.ShapeProperties>();
        var line = properties?.GetFirstChild<A.Outline>();
        var drawing = visual?.GetFirstChild<Xdr.NonVisualConnectorShapeDrawingProperties>();

        return new ConnectorObject
        {
            Id = visual?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value ?? $"connector{context.Next()}",
            Shape = context.Styles.Connector(properties?.GetFirstChild<A.PresetGeometry>()?.Preset?.InnerText),
            Start = new ConnectorEndpoint(context.Name(drawing?.GetFirstChild<A.StartConnection>()?.Id?.Value)),
            End = new ConnectorEndpoint(context.Name(drawing?.GetFirstChild<A.EndConnection>()?.Id?.Value)),
            Style = ShapeStyle.Default with
            {
                Fill = ShapeFill.None,
                Line = ShapeLine.Default with
                {
                    ColorHex = line?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value is { } stroke ? "#" + stroke : ShapeLine.Default.ColorHex,
                    Width = line?.Width?.Value is { } width ? new Emu(width).ToTwips() : ShapeLine.Default.Width,
                    StartArrow = line?.GetFirstChild<A.HeadEnd>() is null ? ArrowEnd.None : ArrowEnd.Arrow,
                    EndArrow = line?.GetFirstChild<A.TailEnd>() is null ? ArrowEnd.None : ArrowEnd.Arrow,
                },
            },
        };
    }
}

internal sealed class SheetFreeformReader : SheetDrawingReader
{
    private const char PartSeparator = '#';
    private const string LegacyPrefix = "pulsefreeform:";
    private readonly OoxmlVectorPaths _paths = new();

    public override EmbeddedObject? Read(OpenXmlElement element, SheetReadContext context)
    {
        if (element is not Xdr.Shape shape) return null;
        var properties = shape.GetFirstChild<Xdr.ShapeProperties>();
        if (properties?.GetFirstChild<A.CustomGeometry>() is not { } geometry) return null;
        var name = shape.GetFirstChild<Xdr.NonVisualShapeProperties>()?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value;
        if (name?.Contains(PartSeparator, StringComparison.Ordinal) == true) return null;
        var transform = properties.GetFirstChild<A.Transform2D>();
        return new PulseOffice.Drawing.Freeform.FreeformObject
        {
            Id = name?.StartsWith(LegacyPrefix, StringComparison.Ordinal) == true ? name[LegacyPrefix.Length..] : name ?? $"freeform{context.Next()}",
            Path = PulseOffice.Drawing.Freeform.FreeformPath.FromOutline(_paths.Outline(geometry)),
            Style = context.Styles.Read(properties, shape.GetFirstChild<Xdr.TextBody>()),
            RotationDegrees = (transform?.Rotation?.Value ?? 0) / 60000.0,
        };
    }
}

internal sealed class SheetGroupPictureReader : SheetDrawingReader
{
    public override EmbeddedObject? Read(OpenXmlElement element, SheetReadContext context) =>
        element is Xdr.Picture picture && context.Pictures is { } pictures
            ? pictures.Read(picture, picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value is { Length: > 0 } given ? given : $"image{context.Next()}")
            : null;
}

internal sealed class SheetGroupReader(IReadOnlyList<SheetDrawingReader> members) : SheetDrawingReader
{
    public override EmbeddedObject? Read(OpenXmlElement element, SheetReadContext context)
    {
        if (element is not Xdr.GroupShape group) return null;

        var transform = group.GetFirstChild<Xdr.GroupShapeProperties>()?.GetFirstChild<A.TransformGroup>();
        var left = transform?.ChildOffset?.X?.Value ?? 0;
        var top = transform?.ChildOffset?.Y?.Value ?? 0;
        var width = Math.Max(1, transform?.ChildExtents?.Cx?.Value ?? 1);
        var height = Math.Max(1, transform?.ChildExtents?.Cy?.Value ?? 1);

        var children = new List<GroupChild>();
        foreach (var child in group.ChildElements)
        {
            var content = child is Xdr.GroupShape ? Read(child, context) : members.Select(reader => reader.Read(child, context)).FirstOrDefault(read => read is not null);
            if (content is null) continue;

            var (x, y, w, h) = Box(child);
            children.Add(new GroupChild(content, (x - left) / (double)width, (y - top) / (double)height, w / (double)width, h / (double)height));
        }

        if (children.Count == 0) return null;

        var name = group.GetFirstChild<Xdr.NonVisualGroupShapeProperties>()?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value;
        return new ShapeGroupObject
        {
            Id = name ?? $"group{context.Next()}",
            Children = children,
            RotationDegrees = (transform?.Rotation?.Value ?? 0) / 60000.0,
        };
    }

    private (long X, long Y, long Width, long Height) Box(OpenXmlElement child) => child switch
    {
        Xdr.Shape shape when shape.GetFirstChild<Xdr.ShapeProperties>()?.GetFirstChild<A.Transform2D>() is { } local =>
            (local.Offset?.X?.Value ?? 0, local.Offset?.Y?.Value ?? 0, local.Extents?.Cx?.Value ?? 1, local.Extents?.Cy?.Value ?? 1),
        Xdr.ConnectionShape connection when connection.GetFirstChild<Xdr.ShapeProperties>()?.GetFirstChild<A.Transform2D>() is { } local =>
            (local.Offset?.X?.Value ?? 0, local.Offset?.Y?.Value ?? 0, local.Extents?.Cx?.Value ?? 1, local.Extents?.Cy?.Value ?? 1),
        Xdr.Picture picture when picture.ShapeProperties?.GetFirstChild<A.Transform2D>() is { } local =>
            (local.Offset?.X?.Value ?? 0, local.Offset?.Y?.Value ?? 0, local.Extents?.Cx?.Value ?? 1, local.Extents?.Cy?.Value ?? 1),
        Xdr.GroupShape nested when nested.GetFirstChild<Xdr.GroupShapeProperties>()?.GetFirstChild<A.TransformGroup>() is { } local =>
            (local.Offset?.X?.Value ?? 0, local.Offset?.Y?.Value ?? 0, local.Extents?.Cx?.Value ?? 1, local.Extents?.Cy?.Value ?? 1),
        _ => (0L, 0L, 1L, 1L),
    };
}
