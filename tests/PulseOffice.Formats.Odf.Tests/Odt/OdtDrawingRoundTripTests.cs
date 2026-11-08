using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtDrawingRoundTripTests
{
    private static AnchoredObject Placed(string id, EmbeddedObject content, ObjectAnchor anchor) =>
        new(id, anchor, new EmbeddedContent(content), new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4)))
        {
            Placement = new ObjectPlacement(Twip.FromCentimeters(1), Twip.FromCentimeters(2)),
            Name = "Oggetto " + id,
        };

    private static TextDocument Document()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var paragraph = new ParagraphBlock("p1", "Testo con oggetti") { Marks = [new ObjectMark(5, "o2")] };
        document.Body.Replace([paragraph]);

        document.Objects.Add(Placed("o1", new ChartDefinition
        {
            Id = "ch1", Type = ChartType.Column, SourceSheet = "", SourceRange = "", ChartTitle = "Vendite",
            InlineData = new ChartData(["Gen", "Feb"], [new ChartSeries("Nord", [10, 20])]),
        }, new ParagraphAnchor("p1")));
        document.Objects.Add(Placed("o2", new FormulaObject { Id = "f1", Expression = "sqrt {a^2 + b^2}" }, new AsCharacterAnchor("p1")));
        document.Objects.Add(Placed("o3", new ShapeObject { Id = "s1", Shape = ShapeKind.Ellipse }, new PageAnchor(1)));
        document.Objects.Add(Placed("o4", new FontworkObject { Id = "fw1", Text = "Benvenuti" }, new ParagraphAnchor("p1")));
        document.Objects.Add(Placed("o5", new VectorGraphicObject
        {
            Id = "v1",
            Asset = new VectorAsset { Id = "a1", Name = "Stella", SourceKind = VectorSourceKind.InlineSvg, SvgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><path d=\"M0 0 L10 10\"/></svg>" },
            RecolorHex = "#FF0000",
        }, new ParagraphAnchor("p1")));
        document.Objects.Add(Placed("o6", new FormControlObject { Id = "c1", ControlKind = FormControlKind.CheckBox, Properties = new FormControlCatalog().Create(FormControlKind.CheckBox, "Spunta") }, new ParagraphAnchor("p1")));
        return document;
    }

    [Fact]
    public void GliOggettiDisegnatiAttraversanoLOdt()
    {
        var document = Document();
        Assert.DoesNotContain(new Odf.Odt.OdtWriter().CheckFidelity(document).Losses, loss => loss.Code == "ODT001");

        var read = OdtRoundTripTests.RoundTrip(document);
        EmbeddedObject Content(string id) => Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All, item => item.Id == id).Content).Value;

        var chart = Assert.IsType<ChartDefinition>(Content("o1"));
        Assert.Equal("Vendite", chart.ChartTitle);
        Assert.Equal(20, chart.InlineData!.Series[0].Values[1]);
        Assert.Equal("sqrt {a^2 + b^2}", Assert.IsType<FormulaObject>(Content("o2")).Expression);
        Assert.Equal(ShapeKind.Ellipse, Assert.IsType<ShapeObject>(Content("o3")).Shape);
        Assert.Equal("Benvenuti", Assert.IsType<FontworkObject>(Content("o4")).Text);
        var vector = Assert.IsType<VectorGraphicObject>(Content("o5"));
        Assert.Equal("#FF0000", vector.RecolorHex);
        Assert.Contains("<path", vector.Asset.SvgContent, StringComparison.Ordinal);
        Assert.Equal(FormControlKind.CheckBox, Assert.IsType<FormControlObject>(Content("o6")).ControlKind);

        var formula = Assert.Single(read.Objects.All, item => item.Id == "o2");
        Assert.IsType<AsCharacterAnchor>(formula.Anchor);
        Assert.Contains(read.Body.Blocks.OfType<ParagraphBlock>().Single().Marks, mark => mark is ObjectMark { ObjectId: "o2", Offset: 5 });
        var shape = Assert.Single(read.Objects.All, item => item.Id == "o3");
        Assert.IsType<PageAnchor>(shape.Anchor);
        Assert.InRange(shape.Size.Width.Value - Twip.FromCentimeters(6).Value, -2, 2);
        Assert.InRange(shape.Placement.X.Value - Twip.FromCentimeters(1).Value, -2, 2);
        Assert.Equal("Oggetto o3", shape.Name);
    }

    [Fact]
    public void LaRigaDellaFirmaAttraversaLOdtComeNelRiferimento()
    {
        var document = Document();
        document.Objects.Add(Placed("o7", new SignatureLineObject
        {
            Id = "sig1", SignerName = "Ada Lovelace", SignerTitle = "Contessa", SignerEmail = "ada@example.org",
            Instructions = "Firmare qui", ShowDate = false, AllowComments = true,
            SignedBy = "Ada", SignedOn = new DateTimeOffset(2026, 10, 13, 9, 30, 0, TimeSpan.Zero),
        }, new ParagraphAnchor("p1")));

        Assert.DoesNotContain(new Odf.Odt.OdtWriter().CheckFidelity(document).Losses, loss => loss.Code == "ODT001");
        var read = OdtRoundTripTests.RoundTrip(document);

        var placed = Assert.Single(read.Objects.All, item => item.Id == "o7");
        var line = Assert.IsType<SignatureLineObject>(Assert.IsType<EmbeddedContent>(placed.Content).Value);
        Assert.Equal("sig1", line.Id);
        Assert.Equal("Ada Lovelace", line.SignerName);
        Assert.Equal("Contessa", line.SignerTitle);
        Assert.Equal("ada@example.org", line.SignerEmail);
        Assert.Equal("Firmare qui", line.Instructions);
        Assert.False(line.ShowDate);
        Assert.True(line.AllowComments);
        Assert.Equal("Ada", line.SignedBy);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 9, 30, 0, TimeSpan.Zero), line.SignedOn);
        Assert.InRange(placed.Size.Width.Value - Twip.FromCentimeters(6).Value, -2, 2);
    }
}
