using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round219OdtNoteContinuationTests
{
    [Fact]
    public void Note_continuation_notices_cross_odt()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Notes.FootnoteSettings = document.Notes.FootnoteSettings with { ContinuationForward = "Continued", ContinuationBackward = "Continuation" };

        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        Assert.Equal("Continued", read.Notes.FootnoteSettings.ContinuationForward);
        Assert.Equal("Continuation", read.Notes.FootnoteSettings.ContinuationBackward);
        Assert.Null(read.Notes.EndnoteSettings.ContinuationForward);
    }
}
