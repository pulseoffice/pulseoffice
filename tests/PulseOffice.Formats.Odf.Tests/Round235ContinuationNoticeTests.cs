using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round235ContinuationNoticeTests
{
    [Fact]
    public void Continuation_notices_have_their_own_paragraph_style_that_round_trips()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        Assert.Equal(DefaultStyleNames.Footnote, document.Styles.Paragraph.Find(DefaultStyleNames.FootnoteContinuation)?.ParentName);
        document.Notes.FootnoteSettings = document.Notes.FootnoteSettings with { ContinuationForward = "Continued", ContinuationBackward = "From before" };
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);

        var read = new OdtReader().Read(new MemoryStream(stream.ToArray())).Value!;

        Assert.NotNull(read.Styles.Paragraph.Find(DefaultStyleNames.FootnoteContinuation));
        Assert.Equal("Continued", read.Notes.FootnoteSettings.ContinuationForward);
        Assert.Equal("From before", read.Notes.FootnoteSettings.ContinuationBackward);
    }
}
