using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DimensionLineResizeRoundTripTests
{
    private const double Width = 113.4;

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly IDimensionLineGeometry _geometry = new DimensionLineGeometry();

    public static TheoryData<double> Heights => new() { 3, 0.5, 1.6 };

    private (ShapeObject Shape, DocumentSize Size) Resized(double factor)
    {
        var line = new DimensionLine
        {
            Start = default,
            End = default,
            LineDistance = Twip.FromCentimeters(0.6),
            GuideOverhang = Twip.FromCentimeters(0.2),
            GuideDistance = Twip.FromCentimeters(0.1),
            Unit = "mm",
            ShowUnit = true,
        };
        var frame = _geometry.Frame(new ShapePoint(0, 0), new ShapePoint(Width, 0), line);
        var shape = new ShapeObject
        {
            Id = "quota",
            Shape = ShapeKind.Line,
            Style = ShapeStyle.Default with { Fill = ShapeFill.None, Line = ShapeLine.Default with { StartArrow = ArrowEnd.Arrow, EndArrow = ArrowEnd.Arrow } },
            Dimension = line with { Start = frame.Start, End = frame.End },
        };
        return (shape, new DocumentSize(Twip.FromPoints(frame.Width), Twip.FromPoints(frame.Height * factor)));
    }

    private void AssertSame(ShapeObject expected, DocumentSize expectedSize, ShapeObject actual, DocumentSize actualSize)
    {
        Assert.InRange(actualSize.Width.Value, expectedSize.Width.Value - 3, expectedSize.Width.Value + 3);
        Assert.InRange(actualSize.Height.Value, expectedSize.Height.Value - 3, expectedSize.Height.Value + 3);
        var before = _geometry.Layout(expected.Dimension!, expectedSize.Width.ToPoints(), expectedSize.Height.ToPoints());
        var after = _geometry.Layout(actual.Dimension!, actualSize.Width.ToPoints(), actualSize.Height.ToPoints());
        Assert.Equal(before.MeasuredLength, after.MeasuredLength, 1);
        foreach (var (left, right) in before.Points.Zip(after.Points))
        {
            Assert.Equal(left.X, right.X, 0.2);
            Assert.Equal(left.Y, right.Y, 0.2);
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void A_resized_dimension_line_keeps_size_and_distances_through_odp_and_pptx(double factor)
    {
        var (shape, size) = Resized(factor);
        var deck = Deck(shape, size);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        odp.Position = 0;
        var fromOdp = Single(new OdpReader(_themes, _masters).Read(odp).Value!);
        AssertSame(shape, size, (ShapeObject)fromOdp.Content, new DocumentSize(fromOdp.Bounds.Width, fromOdp.Bounds.Height));

        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, pptx).Succeeded);
        pptx.Position = 0;
        var fromPptx = Single(new PptxReader(_themes, _masters).Read(pptx).Value!);
        AssertSame(shape, size, (ShapeObject)fromPptx.Content, new DocumentSize(fromPptx.Bounds.Width, fromPptx.Bounds.Height));
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void A_resized_dimension_line_keeps_size_and_distances_through_ods_and_xlsx(double factor)
    {
        var (shape, size) = Resized(factor);
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject { Id = "quota", Anchor = new CellAddress(1, 1), Size = size, Content = shape });

        using var ods = new MemoryStream();
        new OdsWriter().Write(workbook, ods);
        ods.Position = 0;
        var fromOds = Assert.Single(new OdsReader().Read(ods).Value!.Sheets[0].Objects);
        AssertSame(shape, size, (ShapeObject)fromOds.Content, fromOds.Size);

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        xlsx.Position = 0;
        var fromXlsx = Assert.Single(new XlsxReader().Read(xlsx).Value!.Sheets[0].Objects);
        AssertSame(shape, size, (ShapeObject)fromXlsx.Content, fromXlsx.Size);
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void A_resized_dimension_line_keeps_size_and_distances_through_odt_and_docx(double factor)
    {
        var (shape, size) = Resized(factor);
        var document = new TextDocument();
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "Testo");
        document.Body.Replace([paragraph]);
        document.Objects.Add(new AnchoredObject("obj1", new ParagraphAnchor(paragraph.Id), new EmbeddedContent(shape), size));

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        odt.Position = 0;
        var fromOdt = Assert.Single(new OdtReader().Read(odt).Value!.Objects.All);
        AssertSame(shape, size, (ShapeObject)((EmbeddedContent)fromOdt.Content).Value, fromOdt.Size);

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        docx.Position = 0;
        var fromDocx = Assert.Single(_composer.Reader(DocxFlavour.Document).Read(docx).Value!.Objects.All);
        AssertSame(shape, size, (ShapeObject)((EmbeddedContent)fromDocx.Content).Value, fromDocx.Size);
    }

    public static TheoryData<double, bool, bool> Turns => new()
    {
        { 30, false, false },
        { 0, true, false },
        { 0, false, true },
        { 125, true, false },
    };

    private IReadOnlyList<ShapePoint> Drawn(AnchoredObject item) =>
        Drawn((ShapeObject)((EmbeddedContent)item.Content).Value, item.Size, item.RotationDegrees, item.FlipHorizontal, item.FlipVertical);

    private IReadOnlyList<ShapePoint> Drawn(ShapeObject shape, DocumentSize size, double elementDegrees = 0, bool flipHorizontal = false, bool flipVertical = false)
    {
        shape = shape with { FlipHorizontal = shape.FlipHorizontal != flipHorizontal, FlipVertical = shape.FlipVertical != flipVertical };
        var width = size.Width.ToPoints();
        var height = size.Height.ToPoints();
        var radians = (shape.RotationDegrees + elementDegrees) * Math.PI / 180;
        var (cx, cy) = (width / 2, height / 2);
        var points = _geometry.Layout(shape.Dimension!, width, height).Points
            .Select(point => new ShapePoint(shape.FlipHorizontal ? width - point.X : point.X, shape.FlipVertical ? height - point.Y : point.Y))
            .Select(point => new ShapePoint(
                cx + (point.X - cx) * Math.Cos(radians) - (point.Y - cy) * Math.Sin(radians),
                cy + (point.X - cx) * Math.Sin(radians) + (point.Y - cy) * Math.Cos(radians)))
            .ToList();
        var (mx, my) = (points.Average(point => point.X), points.Average(point => point.Y));
        return [.. points.Select(point => new ShapePoint(point.X - mx, point.Y - my))];
    }

    private void AssertDrawnAlike(IReadOnlyList<ShapePoint> expected, IReadOnlyList<ShapePoint> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (left, right) in expected.Zip(actual))
        {
            Assert.Equal(left.X, right.X, 0.3);
            Assert.Equal(left.Y, right.Y, 0.3);
        }
    }

    private ShapeObject Turned(ShapeObject shape, double degrees, bool flipHorizontal, bool flipVertical) =>
        shape with { RotationDegrees = degrees, FlipHorizontal = flipHorizontal, FlipVertical = flipVertical };

    [Theory]
    [MemberData(nameof(Turns))]
    public void A_turned_dimension_line_is_drawn_alike_after_odp_and_pptx(double degrees, bool flipHorizontal, bool flipVertical)
    {
        var (plain, size) = Resized(1);
        var shape = Turned(plain, degrees, flipHorizontal, flipVertical);
        var expected = Drawn(shape, size);
        foreach (var deck in new[] { Deck(shape, size), Rotated(Deck(Turned(plain, 0, flipHorizontal, flipVertical), size), degrees) })
        {
            using var odp = new MemoryStream();
            Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
            odp.Position = 0;
            var fromOdp = Single(new OdpReader(_themes, _masters).Read(odp).Value!);
            AssertDrawnAlike(expected, Drawn((ShapeObject)fromOdp.Content, new DocumentSize(fromOdp.Bounds.Width, fromOdp.Bounds.Height), fromOdp.RotationDegrees));

            using var pptx = new MemoryStream();
            Assert.True(new PptxWriter().Write(deck, pptx).Succeeded);
            pptx.Position = 0;
            var fromPptx = Single(new PptxReader(_themes, _masters).Read(pptx).Value!);
            AssertDrawnAlike(expected, Drawn((ShapeObject)fromPptx.Content, new DocumentSize(fromPptx.Bounds.Width, fromPptx.Bounds.Height), fromPptx.RotationDegrees));
        }
    }

    [Theory]
    [MemberData(nameof(Turns))]
    public void A_turned_dimension_line_is_drawn_alike_after_ods_and_xlsx(double degrees, bool flipHorizontal, bool flipVertical)
    {
        var (plain, size) = Resized(1);
        var shape = Turned(plain, degrees, flipHorizontal, flipVertical);
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject { Id = "quota", Anchor = new CellAddress(4, 4), Size = size, Content = shape });

        using var ods = new MemoryStream();
        new OdsWriter().Write(workbook, ods);
        ods.Position = 0;
        var fromOds = Assert.Single(new OdsReader().Read(ods).Value!.Sheets[0].Objects);
        AssertDrawnAlike(Drawn(shape, size), Drawn((ShapeObject)fromOds.Content, fromOds.Size));

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        xlsx.Position = 0;
        var fromXlsx = Assert.Single(new XlsxReader().Read(xlsx).Value!.Sheets[0].Objects);
        AssertDrawnAlike(Drawn(shape, size), Drawn((ShapeObject)fromXlsx.Content, fromXlsx.Size));
    }

    [Theory]
    [MemberData(nameof(Turns))]
    public void A_turned_dimension_line_is_drawn_alike_after_odt_and_docx(double degrees, bool flipHorizontal, bool flipVertical)
    {
        var (plain, size) = Resized(1);
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "Testo");
        var onShape = new AnchoredObject("obj1", new ParagraphAnchor(paragraph.Id), new EmbeddedContent(Turned(plain, degrees, flipHorizontal, flipVertical)), size);
        var onItem = new AnchoredObject("obj1", new ParagraphAnchor(paragraph.Id), new EmbeddedContent(plain), size)
        {
            RotationDegrees = degrees,
            FlipHorizontal = flipHorizontal,
            FlipVertical = flipVertical,
        };
        foreach (var item in new[] { onShape, onItem })
        {
            var expected = Drawn(item);
            var document = new TextDocument();
            document.Body.Replace([paragraph]);
            document.Objects.Add(item);

            using var odt = new MemoryStream();
            Assert.True(new OdtWriter().Write(document, odt).Succeeded);
            odt.Position = 0;
            AssertDrawnAlike(expected, Drawn(Assert.Single(new OdtReader().Read(odt).Value!.Objects.All)));

            using var docx = new MemoryStream();
            Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
            docx.Position = 0;
            AssertDrawnAlike(expected, Drawn(Assert.Single(_composer.Reader(DocxFlavour.Document).Read(docx).Value!.Objects.All)));
        }
    }

    private Presentation Rotated(Presentation deck, double degrees)
    {
        var slide = deck.Slides[0];
        var element = slide.Elements.OfType<ObjectElement>().Single();
        slide.Replace(element with { RotationDegrees = degrees });
        return deck;
    }

    private Presentation Deck(ShapeObject content, DocumentSize size)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "oggetto", Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), size.Width, size.Height), Content = content });
        deck.AddExisting(slide);
        return deck;
    }

    private ObjectElement Single(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Single(element => element.Content is ShapeObject);
}
