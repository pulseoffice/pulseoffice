using PulseOffice.Documents;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ToolParityTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Sample()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.LineNumbering = new LineNumbering(3) { RestartEachPage = true, Distance = 20 };
        document.Marks = new MarkProtection(true, true);
        var runs = new[]
        {
            new PulseOffice.Text.Rich.TextRun("Nascosto"),
            new PulseOffice.Text.Rich.TextRun("\u200B") { Field = PulseOffice.Text.Rich.TextFieldKind.HiddenParagraph, FieldArgument = "1 == 1", Hidden = true },
        };
        document.Body.Add(new PulseOffice.Documents.Blocks.ParagraphBlock(document.NextId("p"), new PulseOffice.Text.Rich.TextParagraph("Nascosto\u200B") { Runs = runs }));
        var rubyRuns = new[] { new PulseOffice.Text.Rich.TextRun("漢字") { Ruby = "かんじ" }, new PulseOffice.Text.Rich.TextRun(" fine") };
        document.Body.Add(new PulseOffice.Documents.Blocks.ParagraphBlock(document.NextId("p"), new PulseOffice.Text.Rich.TextParagraph("漢字 fine") { Runs = rubyRuns }));
        var group = new PulseOffice.Drawing.Shapes.ShapeGroupObject
        {
            Id = "g1",
            Children =
            [
                new PulseOffice.Drawing.Shapes.GroupChild(new PulseOffice.Drawing.Shapes.ShapeObject { Id = "r1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle }, 0, 0, 0.4, 0.5),
                new PulseOffice.Drawing.Shapes.GroupChild(new PulseOffice.Drawing.Shapes.ShapeObject { Id = "r2", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse }, 0.6, 0.5, 0.4, 0.5),
            ],
        };
        var first = document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Id;
        document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("g1", new PulseOffice.Documents.Objects.ParagraphAnchor(first),
            new PulseOffice.Documents.Objects.EmbeddedContent(group), new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromPoints(200), PulseOffice.Foundation.Units.Twip.FromPoints(100))));
        return document;
    }

    private static TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    [Fact]
    public void NumerazioneDelleRigheEProtezioniAttraversanoOdtEDocx()
    {
        var odt = Through(Sample(), new OdtWriter(), new OdtReader());
        Assert.Equal(Sample().LineNumbering, odt.LineNumbering);
        AssertHidden(odt);
        AssertGroup(odt);
        AssertRuby(odt);
        Assert.Equal(new MarkProtection(true, true), odt.Marks);

        var docx = Through(Sample(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));
        AssertRuby(docx);
        AssertHidden(docx);
        Assert.Equal(3, docx.LineNumbering?.Increment);
        Assert.True(docx.LineNumbering?.RestartEachPage);
        Assert.Equal(20, docx.LineNumbering?.Distance ?? 0, 0);
        var losses = _composer.Writer(DocxFlavour.Document).CheckFidelity(Sample()).Losses.Select(loss => loss.Code).ToList();
        Assert.Contains("DOCX033", losses);
        Assert.DoesNotContain("DOCX034", losses);
        Assert.DoesNotContain("DOCX032", losses);
        var right = Sample();
        right.LineNumbering = right.LineNumbering! with { Side = LineNumberSide.Right };
        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(right).Losses, loss => loss.Code == "DOCX032");
    }

    [Fact]
    public void IFileDiPartenzaDegliStrumentiSonoQuelliDelCampione()
    {
        if (Environment.GetEnvironmentVariable("PULSE_TOOL_FIXTURES") is not { Length: > 0 } folder) return;
        using var file = File.Create(Path.Combine(folder, "tools.odt"));
        Assert.True(new OdtWriter().Write(Sample(), file).Succeeded);
    }

    [Fact]
    public void UnAltraSuiteConservaNumerazioneEProtezioni()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Tools", "tools.lo.odt"));
        var read = new OdtReader().Read(stream).Value!;
        Assert.Equal(3, read.LineNumbering?.Increment);
        Assert.True(read.LineNumbering?.RestartEachPage);
        Assert.Equal(new MarkProtection(true, true), read.Marks);
        AssertHidden(read);
        AssertGroup(read);
        AssertRuby(read);
    }

    private static void AssertRuby(TextDocument document)
    {
        var run = document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Single(candidate => candidate.Ruby is not null);
        Assert.Equal("漢字", run.Text);
        Assert.Equal("かんじ", run.Ruby);
    }

    private static void AssertGroup(TextDocument document)
    {
        var group = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(((PulseOffice.Documents.Objects.EmbeddedContent)Assert.Single(document.Objects.All).Content).Value);
        Assert.Equal(2, group.Children.Count);
        Assert.Equal(200, Assert.Single(document.Objects.All).Size.Width.ToPoints(), 0);
        Assert.Equal(0.6, group.Children.Max(child => child.X), 2);
    }

    private static void AssertHidden(TextDocument document)
    {
        var run = document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs)
            .Single(candidate => candidate.Field == PulseOffice.Text.Rich.TextFieldKind.HiddenParagraph);
        Assert.Equal("1 == 1", run.FieldArgument);
        Assert.True(run.Hidden);
    }
}
