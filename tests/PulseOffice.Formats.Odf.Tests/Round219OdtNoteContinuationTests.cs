using System.IO.Compression;
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

    [Fact]
    public void Footnotes_not_at_the_page_bottom_are_declared_on_reading()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        Assert.DoesNotContain(new OdtReader().Read(stream).Diagnostics, diagnostic => diagnostic.Code == "ODT290");

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("styles.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("styles.xml").Open());
            writer.Write(xml.Replace("text:footnotes-position=\"page\"", "text:footnotes-position=\"document\"", StringComparison.Ordinal));
        }
        stream.Position = 0;

        Assert.Contains(new OdtReader().Read(stream).Diagnostics, diagnostic => diagnostic.Code == "ODT290");
    }
}
