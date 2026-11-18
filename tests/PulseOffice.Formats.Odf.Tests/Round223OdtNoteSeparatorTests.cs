using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round223OdtNoteSeparatorTests
{
    private TextDocument RoundTrip(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }

    [Fact]
    public void The_footnote_separator_crosses_odt()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var separator = new NoteSeparator { RelativeLength = 0.5, Thickness = 1.5, SpaceBefore = 4, SpaceAfter = 6 };
        document.Notes.FootnoteSeparator = separator;

        Assert.Equal(separator, RoundTrip(document).Notes.FootnoteSeparator);
    }

    [Fact]
    public void The_default_separator_reads_back_as_the_default()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();

        Assert.Equal(new NoteSeparator(), RoundTrip(document).Notes.FootnoteSeparator);
    }
}
