using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DimensionLineAndSceneTests
{
    private const string SceneMarkup =
        "<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
        + "draw:style-name=\"gr7\" dr3d:projection=\"perspective\"><dr3d:light dr3d:enabled=\"true\"/><dr3d:sphere draw:style-name=\"gr7\"/></dr3d:scene>";

    private const string SceneStyle =
        "<style:style xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
        + "style:name=\"gr7\" style:family=\"graphic\"><style:graphic-properties draw:fill-color=\"#729fcf\"/></style:style>";

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private ShapeObject Dimension => new()
    {
        Id = "quota",
        Shape = ShapeKind.Line,
        Style = ShapeStyle.Default with { Fill = ShapeFill.None, Line = ShapeLine.Default with { ColorHex = "#FF3333", StartArrow = ArrowEnd.Arrow, EndArrow = ArrowEnd.Arrow } },
        Dimension = new DimensionLine
        {
            Start = new ShapePoint(0, 1),
            End = new ShapePoint(1, 1),
            LineDistance = Twip.FromCentimeters(0.4),
            GuideOverhang = Twip.FromCentimeters(0.1),
            Unit = "mm",
            ShowUnit = true,
            DecimalPlaces = 1,
            Placing = DimensionPlacing.Above,
        },
    };

    [Fact]
    public void La_geometria_della_quota_mette_le_linee_guida_e_la_misura()
    {
        var geometry = new DimensionLineGeometry();
        var line = Dimension.Dimension!;
        var frame = geometry.Frame(new ShapePoint(0, 20), new ShapePoint(100, 20), line);
        Assert.Equal(100, frame.Width, 3);
        Assert.Equal(line.LineDistance.ToPoints() + line.GuideOverhang.ToPoints(), frame.Height, 1);
        var layout = geometry.Layout(line with { Start = frame.Start, End = frame.End }, frame.Width, frame.Height);
        Assert.Equal(100, layout.MeasuredLength, 3);
        Assert.Equal(0, layout.ReadableAngleDegrees, 3);
        Assert.True(layout.LineStart.Y < frame.Start.Y * frame.Height);
        Assert.Equal("35.3 mm", geometry.Label(line, 100, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Testo", geometry.Label(line with { Label = "Testo" }, 100, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(3, geometry.Outline(line, 100, 20).Figures.Count);
        Assert.Equal(2, geometry.Heads(line, 100, 20, ArrowEnd.Arrow, ArrowEnd.Arrow, 6).Figures.Count);
    }

    [Theory]
    [InlineData("furlong", DimensionLine.AutomaticUnit)]
    [InlineData("", DimensionLine.AutomaticUnit)]
    [InlineData("MM", DimensionLine.AutomaticUnit)]
    [InlineData("km", "km")]
    public void An_unknown_dimension_unit_read_from_ooxml_falls_back_to_automatic(string unit, string expected)
    {
        var lines = new DrawingDimensionLines();
        var properties = new D.ShapeProperties();
        lines.Write(properties, Dimension);
        var element = properties.Descendants<DocumentFormat.OpenXml.OpenXmlUnknownElement>().Single(node => node.LocalName == "dimension");
        element.SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute("unit", string.Empty, unit));

        var restored = lines.Restore(Dimension with { Dimension = null }, properties);

        Assert.Equal(expected, restored.Dimension!.Unit);
    }

    [Fact]
    public void La_quota_attraversa_pptx_e_odp()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(Dimension), pptx).Succeeded);
        pptx.Position = 0;
        using (var package = PresentationDocument.Open(pptx, false))
        {
            var shape = package.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<DocumentFormat.OpenXml.Presentation.Shape>()).Single();
            Assert.NotNull(shape.ShapeProperties!.GetFirstChild<D.CustomGeometry>());
            Assert.Contains("mm", shape.TextBody!.InnerText, StringComparison.Ordinal);
        }
        pptx.Position = 0;
        var fromPptx = Single(new PptxReader(_themes, _masters).Read(pptx).Value!);
        AssertDimension(fromPptx);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(fromPptx), odp).Succeeded);
        odp.Position = 0;
        AssertDimension(Single(new OdpReader(_themes, _masters).Read(odp).Value!));
    }

    [Fact]
    public void La_quota_attraversa_xlsx_e_ods()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "quota",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(0.5)),
            Content = Dimension,
        });
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        xlsx.Position = 0;
        var fromXlsx = new XlsxReader().Read(xlsx).Value!;
        AssertDimension(Assert.IsType<ShapeObject>(Assert.Single(fromXlsx.Sheets[0].Objects).Content));

        using var ods = new MemoryStream();
        new OdsWriter().Write(fromXlsx, ods);
        ods.Position = 0;
        var fromOds = new OdsReader().Read(ods);
        Assert.DoesNotContain(fromOds.Diagnostics, diagnostic => diagnostic.Code == "ODF190");
        AssertDimension(Assert.IsType<ShapeObject>(Assert.Single(fromOds.Value!.Sheets[0].Objects).Content));
    }

    [Fact]
    public void La_quota_attraversa_docx_e_odt()
    {
        var document = new TextDocument();
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "Testo");
        document.Body.Replace([paragraph]);
        document.Objects.Add(new AnchoredObject("obj1", new ParagraphAnchor(paragraph.Id), new EmbeddedContent(Dimension), new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(0.5))));

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        docx.Position = 0;
        var fromDocx = _composer.Reader(DocxFlavour.Document).Read(docx).Value!;
        AssertDimension(Shape(fromDocx));

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(fromDocx, odt).Succeeded);
        odt.Position = 0;
        AssertDimension(Shape(new OdtReader().Read(odt).Value!));
    }

    [Fact]
    public void La_scena_3d_attraversa_odp_e_pptx_con_un_segnaposto()
    {
        var scene = new Scene3DObject { Id = "scena", Markup = SceneMarkup, Styles = [SceneStyle] };

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(scene), odp).Succeeded);
        odp.Position = 0;
        var fromOdp = SingleScene(new OdpReader(_themes, _masters).Read(odp).Value!);
        AssertScene(fromOdp);

        using var pptx = new MemoryStream();
        var written = new PptxWriter().Write(Deck(fromOdp), pptx);
        Assert.True(written.Succeeded);
        Assert.Contains(new PptxWriter().CheckFidelity(Deck(fromOdp)).Losses, diagnostic => diagnostic.Code == "ODP102");
        pptx.Position = 0;
        using (var package = PresentationDocument.Open(pptx, false))
        {
            var placeholder = package.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<DocumentFormat.OpenXml.Presentation.Shape>()).Single();
            Assert.Equal("3D", placeholder.TextBody!.InnerText);
        }
        pptx.Position = 0;
        var fromPptx = SingleScene(new PptxReader(_themes, _masters).Read(pptx).Value!);
        AssertScene(fromPptx);

        using var again = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(fromPptx), again).Succeeded);
        again.Position = 0;
        AssertScene(SingleScene(new OdpReader(_themes, _masters).Read(again).Value!));
    }

    [Fact]
    public void La_proprieta_ereditata_da_uno_stile_con_nome_resta_nella_scena()
    {
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(new Scene3DObject { Id = "scena", Markup = SceneMarkup, Styles = [SceneStyle] }), odp).Succeeded);
        InheritFromNamedStyle(odp);
        odp.Position = 0;
        var fromOdp = SingleScene(new OdpReader(_themes, _masters).Read(odp).Value!);
        AssertInherited(fromOdp);

        using var again = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(fromOdp), again).Succeeded);
        again.Position = 0;
        AssertInherited(SingleScene(new OdpReader(_themes, _masters).Read(again).Value!));

        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(fromOdp), pptx).Succeeded);
        pptx.Position = 0;
        using var back = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(SingleScene(new PptxReader(_themes, _masters).Read(pptx).Value!)), back).Succeeded);
        back.Position = 0;
        AssertInherited(SingleScene(new OdpReader(_themes, _masters).Read(back).Value!));
    }

    private void AssertInherited(Scene3DObject scene)
    {
        var style = System.Xml.Linq.XElement.Parse(Assert.Single(scene.Styles));
        Assert.Contains("#123456", style.ToString(), StringComparison.Ordinal);
        Assert.Contains("#729fcf", style.ToString(), StringComparison.Ordinal);
        Assert.Null(style.Attribute((System.Xml.Linq.XNamespace)"urn:oasis:names:tc:opendocument:xmlns:style:1.0" + "parent-style-name"));
    }

    private void InheritFromNamedStyle(MemoryStream odp)
    {
        System.Xml.Linq.XNamespace office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        System.Xml.Linq.XNamespace styleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        System.Xml.Linq.XNamespace draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        System.Xml.Linq.XNamespace dr3d = "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0";
        using var archive = new System.IO.Compression.ZipArchive(odp, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true);
        var content = Load(archive, "content.xml");
        var used = (string)content.Descendants(dr3d + "sphere").Single().Attribute(draw + "style-name")!;
        var automatic = content.Descendants(styleNs + "style").Single(style => (string?)style.Attribute(styleNs + "name") == used);
        automatic.SetAttributeValue(styleNs + "parent-style-name", "Scena");
        Save(archive, "content.xml", content);
        var styles = Load(archive, "styles.xml");
        styles.Root!.Element(office + "styles")!.Add(new System.Xml.Linq.XElement(styleNs + "style",
            new System.Xml.Linq.XAttribute(styleNs + "name", "Scena"), new System.Xml.Linq.XAttribute(styleNs + "family", "graphic"),
            new System.Xml.Linq.XElement(styleNs + "graphic-properties", new System.Xml.Linq.XAttribute(draw + "stroke-color", "#123456"))));
        Save(archive, "styles.xml", styles);
    }

    private System.Xml.Linq.XDocument Load(System.IO.Compression.ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        return System.Xml.Linq.XDocument.Load(stream);
    }

    private void Save(System.IO.Compression.ZipArchive archive, string name, System.Xml.Linq.XDocument document)
    {
        archive.GetEntry(name)!.Delete();
        using var stream = archive.CreateEntry(name).Open();
        document.Save(stream);
    }

    [Fact]
    public void Una_scena_estranea_non_entra_nel_file()
    {
        var hostile = new Scene3DObject { Id = "scena", Markup = "<office:script xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>" };
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(hostile), odp).Succeeded);
        odp.Position = 0;
        using var archive = new System.IO.Compression.ZipArchive(odp);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        Assert.DoesNotContain("office:script", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"><draw:image xlink:href=\"http://attacker.example/beacon.png\"/></dr3d:scene>", "")]
    [InlineData("<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\" xml:base=\"http://attacker.example/\"><dr3d:sphere/></dr3d:scene>", "")]
    [InlineData(SceneMarkup, "<style:style xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" style:name=\"gr7\" style:family=\"graphic\"><office:event-listeners/></style:style>")]
    public void Riferimenti_esterni_nella_scena_o_nei_suoi_stili_non_entrano_nel_file(string markup, string style)
    {
        var hostile = new Scene3DObject { Id = "scena", Markup = markup, Styles = style.Length > 0 ? [style] : [] };
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(hostile), odp).Succeeded);
        odp.Position = 0;
        using var archive = new System.IO.Compression.ZipArchive(odp);
        var content = new StreamReader(archive.GetEntry("content.xml")!.Open()).ReadToEnd();
        Assert.DoesNotContain("attacker.example", content, StringComparison.Ordinal);
        Assert.DoesNotContain("event-listeners", content, StringComparison.Ordinal);
    }

    private void AssertDimension(ShapeObject shape)
    {
        var dimension = Assert.IsType<DimensionLine>(shape.Dimension);
        Assert.Equal(ShapeKind.Line, shape.Shape);
        Assert.Null(shape.Text);
        Assert.Equal("mm", dimension.Unit);
        Assert.True(dimension.ShowUnit);
        Assert.Equal(1, dimension.DecimalPlaces);
        Assert.InRange(dimension.LineDistance.Value, Twip.FromCentimeters(0.4).Value - 2, Twip.FromCentimeters(0.4).Value + 2);
        Assert.Equal(ArrowEnd.Arrow, shape.Style.Line.EndArrow);
        Assert.False(shape.Style.Fill.IsVisible);
        Assert.Equal(1, dimension.End.X, 2);
    }

    private void AssertScene(Scene3DObject scene)
    {
        var node = System.Xml.Linq.XElement.Parse(scene.Markup);
        Assert.Equal("scene", node.Name.LocalName);
        Assert.Equal("sphere", node.Elements().Last().Name.LocalName);
        var style = System.Xml.Linq.XElement.Parse(Assert.Single(scene.Styles));
        Assert.Contains("#729fcf", style.ToString(), StringComparison.Ordinal);
        XNamespaceAware(node, style);
    }

    private void XNamespaceAware(System.Xml.Linq.XElement node, System.Xml.Linq.XElement style)
    {
        System.Xml.Linq.XNamespace draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        System.Xml.Linq.XNamespace styleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        Assert.Equal((string?)style.Attribute(styleNs + "name"), (string?)node.Elements().Last().Attribute(draw + "style-name"));
    }

    private Presentation Deck(PulseOffice.Document.Embedding.EmbeddedObject content)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "oggetto", Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(4), Twip.FromCentimeters(0.5)), Content = content });
        deck.AddExisting(slide);
        return deck;
    }

    private ShapeObject Single(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();

    private Scene3DObject SingleScene(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<Scene3DObject>().Single();

    private ShapeObject Shape(TextDocument document) =>
        Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(document.Objects.All.Single().Content).Value);
}
