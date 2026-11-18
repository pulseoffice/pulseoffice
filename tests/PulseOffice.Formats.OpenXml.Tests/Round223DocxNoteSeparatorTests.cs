using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round223DocxNoteSeparatorTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument RoundTrip(NoteSeparator separator)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Notes.Add(new Note("fn1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("fnp", "text")])));
        ((ParagraphBlock)document.Body[0]).Marks = [new NoteMark(0, "fn1")];
        document.Notes.FootnoteSeparator = separator;
        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    [Fact]
    public void A_custom_footnote_separator_crosses_docx_as_a_bordered_separator_paragraph()
    {
        var separator = new NoteSeparator { RelativeLength = 0.5, Thickness = 1.5, SpaceBefore = 4, SpaceAfter = 6 };

        var read = RoundTrip(separator).Notes.FootnoteSeparator;

        Assert.Equal(0.5, read.RelativeLength, 2);
        Assert.Equal(1.5, read.Thickness, 3);
        Assert.Equal(4, read.SpaceBefore, 3);
        Assert.Equal(6, read.SpaceAfter, 3);
    }

    [Fact]
    public void The_default_separator_stays_the_word_separator_mark()
    {
        Assert.Equal(new NoteSeparator(), RoundTrip(new NoteSeparator()).Notes.FootnoteSeparator);
    }
}
