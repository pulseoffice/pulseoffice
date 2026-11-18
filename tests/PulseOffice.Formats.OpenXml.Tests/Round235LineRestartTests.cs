using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round235LineRestartTests
{
    [Fact]
    public void A_restart_on_the_first_paragraph_becomes_the_starting_line_number()
    {
        var composer = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory());
        var document = new TextDocument(new DefaultStyleSheetFactory().Create()) { LineNumbering = new LineNumbering(1) };
        var first = new ParagraphBlock("p1", "first", DefaultStyleNames.Standard);
        first.Content = first.Content with { LineNumberRestart = 5 };
        document.Body.Add(first);
        document.Body.Add(new ParagraphBlock("p2", "second", DefaultStyleNames.Standard));
        var writer = composer.Writer(DocxFlavour.Document);

        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX248");
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        var read = composer.Reader(DocxFlavour.Document).Read(new MemoryStream(stream.ToArray())).Value!;

        Assert.Equal(5, read.LineNumbering?.Start);
        Assert.False(read.LineNumbering?.RestartEachSection);
    }
}
