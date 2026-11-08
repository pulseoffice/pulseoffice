using System.Diagnostics;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfLargeExportTests
{
    private sealed class Book(int pages) : IPrintableDocument
    {
        private const string Line = "Il documento lungo misura memoria e dimensione del file quando le pagine sono molte.";

        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas)
        {
            for (var row = 0; row < 48; row++)
                canvas.DrawGlyphs(new PrintGlyphRun(Line, 56, 60 + row * 15, new PrintFont("Helvetica", 11), PrintColor.Black, [.. Line.Select(_ => 5.6)]));
            var pixels = new byte[64 * 64 * 3];
            Array.Fill(pixels, (byte)120);
            canvas.DrawImage(new PageImage(64, 64, pixels), new DocumentRect(Twip.FromPoints(500), Twip.FromPoints(20), Twip.FromPoints(40), Twip.FromPoints(40)));
        }
    }

    [Fact]
    public void FiveHundredPagesStayLinearAndSmall()
    {
        var book = new Book(500);
        GC.Collect();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        using var stream = new MemoryStream();

        var result = new PdfExporter(new PulseOffice.Formats.Pdf.Fonts.StandardPdfFonts(new PdfFontMetrics())).Write(book, stream, new PdfExportOptions("Libro"));

        clock.Stop();
        var churn = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.True(result.Succeeded);
        Assert.True(stream.Length / 500 < 4_000, $"{stream.Length / 500} bytes per page");
        Assert.True(churn / 500 < 3_500_000, $"{churn / 500} bytes allocated per page");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), clock.Elapsed.ToString());
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-large-export.txt"),
            $"pdf pages=500 bytes={stream.Length} ms={clock.ElapsedMilliseconds} allocated={churn}\n");
    }
}
