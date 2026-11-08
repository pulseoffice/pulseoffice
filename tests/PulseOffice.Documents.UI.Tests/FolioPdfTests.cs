using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioPdfTests
{
    private static void Prepare(TextDocument document)
    {
        var link = new TextParagraph("Vai al sito o al capitolo") { Runs = [new TextRun("Vai al "), new TextRun("sito") { Hyperlink = "https://www.example.org" }, new TextRun(" o al "), new TextRun("capitolo") { Hyperlink = "#segno" }] };
        var linked = new ParagraphBlock(document.NextId("p"), link);
        var target = new ParagraphBlock(document.NextId("p"), "Capitolo secondo", DefaultStyleNames.HeadingLevel(1)) { Marks = [new BookmarkMark(0, "segno", MarkEdge.Point)] };
        var commented = new ParagraphBlock(document.NextId("p"), "Testo commentato");
        document.Comments.Add(new Comment("c1", "Anna", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), new BlockContainer([new ParagraphBlock(document.NextId("p"), "Da rivedere")])));
        commented.Marks = [new CommentMark(0, "c1", MarkEdge.Start), new CommentMark(5, "c1", MarkEdge.End)];
        document.Body.Replace(
        [
            new ParagraphBlock(document.NextId("p"), "Capitolo primo", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock(document.NextId("p"), "Sezione", DefaultStyleNames.HeadingLevel(2)),
            linked,
            target,
            commented,
        ]);
    }

    [Fact]
    public Task IlPdfDiFolioHaSegnalibriCollegamentiENote() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(Prepare);
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        using var stream = new MemoryStream();

        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(printable, stream, new PdfExportOptions("Prova", Compress: false));
        var text = System.Text.Encoding.Latin1.GetString(stream.ToArray());

        Assert.True(result.Succeeded);
        Assert.Contains("/Title (Capitolo primo)", text, StringComparison.Ordinal);
        Assert.Contains("/Title (Sezione) /Parent", text, StringComparison.Ordinal);
        Assert.Contains("/URI (https://www.example.org)", text, StringComparison.Ordinal);
        Assert.Contains("/Dest (segno)", text, StringComparison.Ordinal);
        Assert.Contains("(segno) [", text, StringComparison.Ordinal);
        Assert.Contains("/Subtype /Text", text, StringComparison.Ordinal);
        Assert.Contains("/T (Anna)", text, StringComparison.Ordinal);
        return Task.CompletedTask;
    });
}
