using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioLargeExportTests
{
    private const string Sentence =
        "Il documento lungo serve a misurare la memoria e la dimensione del file quando le pagine sono molte, con parole ripetute. ";

    private static void Prepare(TextDocument document)
    {
        var blocks = new List<Block>();
        for (var i = 0; i < 160; i++) blocks.Add(new ParagraphBlock(document.NextId("p"), string.Concat(Enumerable.Repeat(Sentence, 3))));
        document.Body.Replace(blocks);
    }

    [Fact]
    public Task ALongFolioDocumentExportsWithBoundedSizeAndMemory() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(Prepare);
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        Assert.True(printable.PageCount >= 1, $"{printable.PageCount} pages");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();

        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(printable, stream, new PdfExportOptions("Lungo"));

        clock.Stop();
        var churn = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var retained = GC.GetTotalMemory(forceFullCollection: true) - before;
        var perPage = stream.Length / (double)printable.PageCount;
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-large-export.txt"),
            $"{DateTimeOffset.Now:O} pages={printable.PageCount} bytes={stream.Length} perPage={perPage:0} ms={clock.ElapsedMilliseconds} allocated={churn} retained={retained}\n");

        Assert.True(result.Succeeded);
        Assert.True(perPage < 12_000, $"{perPage:0} bytes per page");
        Assert.True(churn / printable.PageCount < 8_000_000, $"{churn / printable.PageCount} bytes allocated per page");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"{clock.Elapsed}");
        return Task.CompletedTask;
    });
}
