using PulseOffice.Charts;
using PulseOffice.Document.Forms;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtObjectPlacesTests
{
    private readonly byte[] _png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    [Fact]
    public void UnGruppoCheContieneUnImmagineSopravviveAlGiroInOdt()
    {
        var document = new TextDocument();
        document.Body.Add(new ParagraphBlock("p1", "testo"));
        var group = new ShapeGroupObject
        {
            Id = "g1",
            Children = [new GroupChild(new RasterImageObject { Id = "img1", Data = _png, MediaType = "image/png" }, 0, 0, 1, 1)],
        };
        document.Objects.Add(new AnchoredObject("obj1", new CharacterAnchor("p1"), new EmbeddedContent(group),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3))));

        var copy = RoundTrip(document);

        var read = Assert.IsType<ShapeGroupObject>(Assert.IsType<EmbeddedContent>(Assert.Single(copy.Objects.All).Content).Value);
        var image = Assert.IsType<RasterImageObject>(Assert.Single(read.Children).Content);
        Assert.Equal(_png, image.Data.ToArray());
    }

    [Fact]
    public void UnGraficoNelPieDiPaginaSopravviveAlGiroInOdt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "testo"));
        var page = document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        page.Footer.Enabled = true;
        page.Footer.Default.Add(new ParagraphBlock("f1", string.Empty, DefaultStyleNames.Footer));
        var chart = new ChartDefinition
        {
            Id = "c1", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
            InlineData = new ChartData(["2024", "2025"], [new ChartSeries("Vendite", [1, 2])]),
        };
        document.Objects.Add(new AnchoredObject("obj1", new AsCharacterAnchor("f1"), new EmbeddedContent(chart),
            new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(3))));

        var copy = RoundTrip(document);

        var item = Assert.Single(copy.Objects.All);
        Assert.IsType<ChartDefinition>(Assert.IsType<EmbeddedContent>(item.Content).Value);
    }

    [Fact]
    public void UnGruppoDiControlliDelModuloSopravviveAlGiroInOdt()
    {
        var document = new TextDocument();
        document.Body.Add(new ParagraphBlock("p1", "modulo"));
        FormControlObject Box(string id) => new() { Id = id, ControlKind = FormControlKind.CheckBox, Properties = new FormControlProperties { Name = id } };
        var group = new ShapeGroupObject { Id = "g1", Children = [new GroupChild(Box("a"), 0, 0, 0.5, 1), new GroupChild(Box("b"), 0.5, 0, 0.5, 1)] };
        document.Objects.Add(new AnchoredObject("obj1", new ParagraphAnchor("p1"), new EmbeddedContent(group),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1))));

        var copy = RoundTrip(document);

        var read = Assert.IsType<ShapeGroupObject>(Assert.IsType<EmbeddedContent>(Assert.Single(copy.Objects.All).Content).Value);
        Assert.All(read.Children, child => Assert.IsType<FormControlObject>(child.Content));
        Assert.Equal(2, read.Children.Count);
    }

    [Fact]
    public void UnaTabellaEsternoSenzaSpaziaturaNelleCelleNonNeRiceveUna()
    {
        const string flat = """
            <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
              <office:automatic-styles><style:style style:name="A2" style:family="table-cell"><style:table-cell-properties fo:padding="0.1cm" fo:border="none"/></style:style></office:automatic-styles>
              <office:body><office:text>
                <table:table table:name="T1"><table:table-column/><table:table-row><table:table-cell><text:p>a</text:p></table:table-cell></table:table-row></table:table>
                <table:table table:name="T2"><table:table-column/><table:table-row><table:table-cell table:style-name="A2"><text:p>b</text:p></table:table-cell></table:table-row></table:table>
              </office:text></office:body>
            </office:document>
            """;
        var read = new OdtReader().Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(flat)));

        Assert.True(read.Succeeded);
        var tables = read.Value!.Body.Blocks.OfType<TableBlock>().ToList();
        Assert.Equal(0, tables[0].Direct.CellPadding);
        Assert.Equal(0, tables[0].Direct.VerticalCellPadding);
        Assert.Equal(2.83, tables[1].Direct.VerticalCellPadding!.Value, 2);
    }
}
