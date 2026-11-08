using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxThemeWriter(OoxmlWriteUnits units, ColorMapCodec maps)
{
    public ThemePart Write(OpenXmlPartContainer container, Theme theme)
    {
        var part = container.AddNewPart<ThemePart>();
        var colors = maps.Physical(theme.Colors, maps.For(theme.Colors));

        part.Theme = new D.Theme(
            new D.ThemeElements(
                new D.ColorScheme(
                    new D.Dark1Color(Rgb(colors.Text)),
                    new D.Light1Color(Rgb(colors.Background)),
                    new D.Dark2Color(Rgb(colors.Text2)),
                    new D.Light2Color(Rgb(colors.Background2)),
                    new D.Accent1Color(Rgb(colors.Accent1)),
                    new D.Accent2Color(Rgb(colors.Accent2)),
                    new D.Accent3Color(Rgb(colors.Accent3)),
                    new D.Accent4Color(Rgb(colors.Accent4)),
                    new D.Accent5Color(Rgb(colors.Accent5)),
                    new D.Accent6Color(Rgb(colors.Accent6)),
                    new D.Hyperlink(Rgb(colors.Hyperlink)),
                    new D.FollowedHyperlinkColor(Rgb(colors.FollowedHyperlink)))
                { Name = "Office" },
                new D.FontScheme(
                    new D.MajorFont(new D.LatinFont { Typeface = theme.Fonts.Major }),
                    new D.MinorFont(new D.LatinFont { Typeface = theme.Fonts.Minor }))
                { Name = theme.Name },
                FormatScheme()),
            new D.ObjectDefaults())
        { Name = theme.Name };

        part.Theme.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
        return part;
    }

    private D.RgbColorModelHex Rgb(uint argb) => new() { Val = units.Hex(argb) };

    private D.SolidFill Placeholder() => new(new D.SchemeColor { Val = D.SchemeColorValues.PhColor });

    private D.FormatScheme FormatScheme() =>
        new(new D.FillStyleList(Placeholder(), Placeholder(), Placeholder()),
            new D.LineStyleList(
                new D.Outline(Placeholder()) { Width = 6350 },
                new D.Outline(Placeholder()) { Width = 12700 },
                new D.Outline(Placeholder()) { Width = 19050 }),
            new D.EffectStyleList(
                new D.EffectStyle(new D.EffectList()),
                new D.EffectStyle(new D.EffectList()),
                new D.EffectStyle(new D.EffectList())),
            new D.BackgroundFillStyleList(Placeholder(), Placeholder(), Placeholder()))
        { Name = "Office" };
}

internal sealed class PptxMasterWriter(
    OoxmlWriteUnits units, ShapeTreeFactory trees, SeedListStyleBuilder seeds, ISlideLayoutRegistry layouts,
    BodyPropertiesBuilder bodies, ColorMapCodec maps, PptxDeckShapeMapper shapes)
{
    private readonly OoxmlPendingImages _pending = new();
    private readonly DrawingML.DrawingDuotone _duotones = new();

    private D.Blip Duotoned(D.Blip blip, PulseOffice.Drawing.Assets.RasterImageObject image)
    {
        _duotones.Write(blip, image.Duotone);
        return blip;
    }

    private uint _nextLayoutId;

    public void Reset() => _nextLayoutId = 2147483648U;

    public uint NextId() => _nextLayoutId++;

    private readonly XlsxShapeStyleMapper _fills = new();

    public SlideMasterPart Write(PresentationPart presentation, SlideMaster master, Theme theme)
    {
        var part = presentation.AddNewPart<SlideMasterPart>();
        var tree = trees.Empty();
        uint shapeId = 2;
        if (master.Elements.Date) tree.Append(Element(shapeId++, "Date", P.PlaceholderValues.DateAndTime, 0.04));
        if (master.Elements.Footer) tree.Append(Element(shapeId++, "Footer", P.PlaceholderValues.Footer, 0.36));
        if (master.Elements.SlideNumber) tree.Append(Element(shapeId++, "SlideNumber", P.PlaceholderValues.SlideNumber, 0.72));
        Decorate(tree, master.Objects, ref shapeId);
        var background = master.BackgroundImage is { } image
            ? new P.Background(new P.BackgroundProperties(
                master.BackgroundLayout.Mode is BackgroundImageMode.Tile
                    ? new D.BlipFill(Duotoned(_pending.Blip(image.Data.ToArray()), image), new D.Tile { HorizontalRatio = 100000, VerticalRatio = 100000, Alignment = D.RectangleAlignmentValues.TopLeft, Flip = D.TileFlipValues.None })
                    : new D.BlipFill(Duotoned(_pending.Blip(image.Data.ToArray()), image), new D.Stretch(new D.FillRectangle())),
                new D.EffectList()))
            : master.BackgroundFill is { } fill
                ? new P.Background(new P.BackgroundProperties(_fills.Fill(fill), new D.EffectList()))
            : master.BackgroundColor is { } explicitColor ? units.Background(explicitColor) : units.Background(master.Background);
        var common = new P.CommonSlideData(background, tree) { Name = master.Name };
        part.SlideMaster = new P.SlideMaster(
            common,
            maps.Build(maps.For(theme.Colors)));
        return part;
    }

    private void Decorate(P.ShapeTree tree, IEnumerable<SlideElement> objects, ref uint shapeId)
    {
        OpenXmlElement anchor = tree.GetFirstChild<P.GroupShapeProperties>()!;
        foreach (var element in objects.OfType<ObjectElement>().Where(shapes.IsDecoration).OrderBy(element => element.ZOrder))
            anchor = tree.InsertAfter(shapes.BuildDecoration(element, shapeId++), anchor);
    }

    private P.Shape Element(uint id, string name, P.PlaceholderValues type, double left) =>
        new(new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = type })),
            new P.ShapeProperties(new D.Transform2D(
                new D.Offset { X = (long)(left * 12192000), Y = 6356350 },
                new D.Extents { Cx = 2743200, Cy = 365125 })),
            new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph()));

    public SlideLayoutPart Layout(SlideMasterPart master, SlideLayoutTemplate template)
    {
        var part = master.AddNewPart<SlideLayoutPart>();
        part.AddPart(master);
        var tree = trees.Empty();
        uint shapeId = 2;
        foreach (var placeholder in template.Placeholders)
            tree.Append(trees.Placeholder(shapeId++, placeholder.Role, units.Placeholder(placeholder.Role),
                units.Transform(placeholder.Bounds),
                new P.TextBody(bodies.Build(placeholder.Frame), seeds.Build(placeholder.Seed), new D.Paragraph())));
        Decorate(tree, template.Objects, ref shapeId);

        var common = template.Background is { } background
            ? new P.CommonSlideData(units.Background(background), tree)
            : new P.CommonSlideData(tree);
        common.Name = template.Id.StartsWith(PptxLayoutIdentity.CustomPrefix, StringComparison.Ordinal) ? template.Name : template.Kind.ToString();

        part.SlideLayout = new P.SlideLayout(common)
        {
            Type = new EnumValue<P.SlideLayoutValues>(new P.SlideLayoutValues(layouts.OoxmlType(template.Kind))),
        };
        if (template.HideMasterObjects) part.SlideLayout.ShowMasterShapes = false;

        var ids = master.SlideMaster!.GetFirstChild<P.SlideLayoutIdList>();
        if (ids is null)
        {
            ids = new P.SlideLayoutIdList();
            master.SlideMaster.Append(ids);
        }
        ids.Append(new P.SlideLayoutId { Id = _nextLayoutId++, RelationshipId = master.GetIdOfPart(part) });

        return part;
    }
}

internal interface IPptxObjectWriter
{
    bool CanWrite(PulseOffice.Document.Embedding.EmbeddedObject content);

    OpenXmlElement? Write(ObjectElement element, uint shapeId, SlidePart slide);
}

internal abstract class PptxObjectWriter<TContent>(OoxmlAltTextMapper altText) : IPptxObjectWriter
    where TContent : PulseOffice.Document.Embedding.EmbeddedObject
{
    public virtual bool CanWrite(PulseOffice.Document.Embedding.EmbeddedObject content) => content is TContent;

    private readonly OoxmlObjectNameMapper _names = new();

    public OpenXmlElement? Write(ObjectElement element, uint shapeId, SlidePart slide) =>
        Build(element, (TContent)element.Content, shapeId, slide) is { } written ? _names.Write(altText.Write(written, element.Content), element.Content) : null;

    protected abstract OpenXmlElement? Build(ObjectElement element, TContent content, uint shapeId, SlidePart slide);
}

internal sealed class ChartWriter(PptxDeckChartMapper charts, OoxmlAltTextMapper altText)
    : PptxObjectWriter<PulseOffice.Charts.ChartDefinition>(altText)
{
    protected override OpenXmlElement? Build(ObjectElement element, PulseOffice.Charts.ChartDefinition chart, uint shapeId, SlidePart slide) =>
        charts.Build(element, chart, shapeId, slide);
}

internal sealed class TableWriter(PptxDeckTableMapper tables, OoxmlAltTextMapper altText)
    : PptxObjectWriter<PulseOffice.Document.Embedding.DataTableObject>(altText)
{
    protected override OpenXmlElement? Build(ObjectElement element, PulseOffice.Document.Embedding.DataTableObject table, uint shapeId, SlidePart slide) =>
        tables.Build(element, table, shapeId);
}

internal sealed class PackageWriter(PptxDeckShapeMapper shapes, OoxmlAltTextMapper altText)
    : PptxObjectWriter<PulseOffice.Document.Embedding.PackagedObject>(altText)
{
    protected override OpenXmlElement? Build(ObjectElement element, PulseOffice.Document.Embedding.PackagedObject packaged, uint shapeId, SlidePart slide) =>
        shapes.BuildOle(element, packaged, shapeId, slide);
}

internal sealed class DrawingWriter(PptxDeckShapeMapper shapes, OoxmlAltTextMapper altText)
    : PptxObjectWriter<PulseOffice.Document.Embedding.EmbeddedObject>(altText)
{
    protected override OpenXmlElement? Build(ObjectElement element, PulseOffice.Document.Embedding.EmbeddedObject content, uint shapeId, SlidePart slide) =>
        shapes.Build(element, shapeId, slide);
}
