using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ObjectRoundTripTests
{
    private const string Folder = "Fixtures/Objects";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ObjectSample _sample = new();
    private readonly FrameChains _chains = new();

    public static TheoryData<string> Routes() => new() { "odt", "docx", "odt-lo", "docx-lo", "odt-lo-docx", "docx-lo-odt" };

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Fixture(string name, ITextDocumentReader reader)
    {
        using var stream = File.OpenRead(Path.Combine(Folder, name));
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Route(string route) => route switch
    {
        "odt" => Through(_sample.Build(), new OdtWriter(), new OdtReader()),
        "docx" => Through(_sample.Build(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
        "odt-lo" => Fixture("objects.lo.odt", new OdtReader()),
        "docx-lo" => Fixture("objects.lo.docx", _composer.Reader(DocxFlavour.Document)),
        "odt-lo-docx" => Fixture("objects.odt.lo.docx", _composer.Reader(DocxFlavour.Document)),
        _ => Fixture("objects.docx.lo.odt", new OdtReader()),
    };

    private bool ThroughReferenceDocx(string route) => route is "docx-lo" or "odt-lo-docx" or "docx-lo-odt";

    private AnchoredObject Named(TextDocument document, string name) =>
        document.Objects.All.Single(item => item.Name == name);

    private AnchoredObject Picture(TextDocument document, string paragraphText) =>
        document.Objects.All.Single(item => item.Content is EmbeddedContent { Value: RasterImageObject }
            && document.Body.Blocks.OfType<ParagraphBlock>().Any(paragraph => paragraph.Text.StartsWith(paragraphText, StringComparison.Ordinal) && item.Anchor.Accept(new AnchorParagraph()) == paragraph.Id));

    [Fact]
    public void IFileDiPartenzaSonoQuelliDelCampione()
    {
        var folder = Environment.GetEnvironmentVariable("PULSE_OBJECT_FIXTURES");
        if (string.IsNullOrEmpty(folder)) return;
        using (var odt = File.Create(Path.Combine(folder, "objects.odt"))) Assert.True(new OdtWriter().Write(_sample.Build(), odt).Succeeded);
        using (var docx = File.Create(Path.Combine(folder, "objects.docx"))) Assert.True(_composer.Writer(DocxFlavour.Document).Write(_sample.Build(), docx).Succeeded);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void IlContornoLoScorrimentoELaSpecchiaturaAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var item = Picture(document, "parola");

        Assert.True(item.WrapContour);
        Assert.Equal(WrapMode.Right, item.Wrap);
        Assert.True(item.FlipHorizontal);
        Assert.Equal(6, item.WrapSpacing.Left.ToPoints(), 0);
        var picture = (RasterImageObject)((EmbeddedContent)item.Content).Value;
        Assert.Contains("Un triangolo", picture.Description, StringComparison.Ordinal);
        if (ThroughReferenceDocx(route)) return;
        var contour = item.Contour!.Points;
        Assert.Equal(3, contour.Count);
        Assert.All(ObjectSample.Triangle.Points.Zip(contour), pair =>
        {
            Assert.Equal(pair.First.X, pair.Second.X, 2);
            Assert.Equal(pair.First.Y, pair.Second.Y, 2);
        });
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LoScorrimentoDelSoloPrimoParagrafoVaInOdtESiDichiaraInDocx(string route)
    {
        var document = Route(route);
        var item = Picture(document, "Solo");

        Assert.Equal(route != "odt-lo-docx", item.FlipVertical);
        Assert.Equal(route is "odt" or "docx" or "odt-lo", item.WrapFirstParagraph);
    }

    [Fact]
    public void IlSoloPrimoParagrafoNonSiDichiaraPiuInDocx()
    {
        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(_sample.Build()).Losses, loss => loss.Code == "DOCX020");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LaDidascaliaNumerataAttraversaIFormati(string route)
    {
        var document = Route(route);
        var caption = document.Body.Blocks.OfType<ParagraphBlock>().Single(paragraph => paragraph.Text.Contains("il grafico", StringComparison.Ordinal));

        var field = caption.Content.Runs.Single(run => run.Field == TextFieldKind.Sequence);
        Assert.Equal(route is "odt-lo-docx" or "docx-lo" ? "Figura" : "Figure", field.FieldArgument);
        Assert.Equal("1", field.Text);
        Assert.Equal("Figura 1: il grafico", caption.Text);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LeCorniciCollegateAttraversanoIFormati(string route)
    {
        var document = Route(route);
        if (route == "docx-lo-odt")
        {
            Assert.Contains(document.Objects.All, item => item.Name == "Prima");
            return;
        }
        var heads = document.Objects.All.Where(item => item.Content is TextBoxContent { NextFrameName: not null }).ToList();

        var head = Assert.Single(heads);
        var chain = _chains.Chain(document, head);
        Assert.Equal(2, chain.Count);
        Assert.StartsWith("parola", ((TextBoxContent)head.Content).Content.Blocks.OfType<ParagraphBlock>().First().Text, StringComparison.Ordinal);
        Assert.All(((TextBoxContent)chain[1].Content).Content.Blocks.OfType<ParagraphBlock>(), paragraph => Assert.Empty(paragraph.Text));
    }
}
