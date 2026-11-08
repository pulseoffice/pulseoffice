using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class NestedRevisionFormatTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ChangeRegions _regions = new(new StoryCatalog(new BlockTree()));

    private static TextDocument Nested(string kept)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var paragraph = new ParagraphBlock("p1", "Uno " + kept);
        paragraph.State = paragraph.State with
        {
            Marks = [new ChangeMark(4, "ct1", MarkEdge.Start), new ChangeMark(4, "ct2", MarkEdge.Point), new ChangeMark(4 + kept.Length, "ct1", MarkEdge.End)],
        };
        document.Body.Replace([paragraph]);
        document.Changes.Add(new Insertion("ct1", "Anna", new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero)));
        document.Changes.Add(new Deletion("ct2", "Bruno", new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero), new BlockContainer([new ParagraphBlock("dp1", "due ")])) { WithinInsertion = "ct1" });
        return document;
    }

    private MemoryStream Docx(TextDocument document)
    {
        var buffer = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, buffer).Succeeded);
        buffer.Position = 0;
        return buffer;
    }

    private TextDocument ReadDocx(MemoryStream buffer)
    {
        buffer.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(buffer);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private TextDocument ThroughOdt(TextDocument document)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, buffer).Succeeded);
        buffer.Position = 0;
        var read = new OdtReader().Read(buffer);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private void AssertNested(TextDocument document, string kept)
    {
        var paragraph = Assert.Single(document.Body.Blocks.OfType<ParagraphBlock>());
        Assert.Equal("Uno " + kept, paragraph.Text);
        var map = _regions.Map(document);
        var insertion = Assert.Single(map.Regions, region => region.Change is Insertion);
        Assert.Equal("Anna", insertion.Change.Author);
        Assert.Equal((4, 4 + kept.Length), (insertion.Start.Offset, insertion.End.Offset));
        var deletion = Assert.IsType<Deletion>(Assert.Single(map.Regions, region => region.Change is Deletion).Change);
        Assert.Equal("Bruno", deletion.Author);
        Assert.Equal(insertion.Change.Id, deletion.WithinInsertion);
        Assert.Equal("due ", ((ParagraphBlock)deletion.Removed[0]).Text);
    }

    [Theory]
    [InlineData("tre")]
    [InlineData("")]
    public void DocxWritesTheDeletionInsideTheInsertion(string kept)
    {
        using var buffer = Docx(Nested(kept));
        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(buffer, false);
        var inserted = Assert.Single(package.MainDocumentPart!.Document!.Body!.Descendants<W.InsertedRun>());
        Assert.Equal("Anna", inserted.Author?.Value);
        var deleted = Assert.Single(inserted.Elements<W.DeletedRun>());
        Assert.Equal("Bruno", deleted.Author?.Value);
        Assert.Equal("due ", string.Concat(deleted.Descendants<W.DeletedText>().Select(text => text.Text)));
        Assert.Equal(kept, string.Concat(inserted.Descendants<W.Text>().Select(text => text.Text)));
    }

    [Theory]
    [InlineData("tre")]
    [InlineData("")]
    public void DocxRoundTripKeepsTheNesting(string kept)
    {
        using var buffer = Docx(Nested(kept));
        AssertNested(ReadDocx(buffer), kept);
    }

    [Theory]
    [InlineData("tre")]
    [InlineData("")]
    public void OdtRoundTripKeepsTheNesting(string kept) => AssertNested(ThroughOdt(Nested(kept)), kept);

    [Fact]
    public void OdtThenDocxKeepsTheNesting()
    {
        using var buffer = Docx(ThroughOdt(Nested("tre")));
        AssertNested(ReadDocx(buffer), "tre");
    }
}
