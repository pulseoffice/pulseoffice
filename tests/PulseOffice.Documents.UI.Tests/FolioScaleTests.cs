using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FolioPerformanceCollection
{
    public const string Name = "Folio performance";
}

[Collection(FolioPerformanceCollection.Name)]
public sealed class FolioScaleTests
{
    private const string Sentence = "Il documento lungo serve a misurare la memoria e la dimensione del file quando le pagine sono molte, con parole ripetute. ";

    private static Action<TextDocument> Paragraphs(int count) => document =>
    {
        var blocks = new List<Block>(count);
        for (var i = 0; i < count; i++) blocks.Add(new ParagraphBlock(document.NextId("p"), string.Concat(Enumerable.Repeat(Sentence, 3))));
        document.Body.Replace(blocks);
    };

    private static void Record(string line) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-folio-scale.txt"), $"{DateTimeOffset.Now:O} {line}\n");

    [Fact]
    public Task TwoThousandParagraphsBuildAndExportInBoundedTime() => FolioScene.RunAsync(() =>
    {
        var clock = Stopwatch.StartNew();
        var scene = new FolioScene(Paragraphs(2000));
        var built = clock.Elapsed;
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(printable, stream, new PdfExportOptions("Lungo"));
        var total = clock.Elapsed;
        Record($"paragraphs=2000 pages={printable.PageCount} build={built.TotalMilliseconds:0} total={total.TotalMilliseconds:0}");

        Assert.True(result.Succeeded);
        Assert.True(printable.PageCount > 100, $"{printable.PageCount} pages");
        Assert.True(built < TimeSpan.FromSeconds(30), $"build {built}");
        Assert.True(total < TimeSpan.FromSeconds(90), $"build and export {total}");
        return Task.CompletedTask;
    });

    [Fact]
    public Task SceneTimeGrowsLinearlyWithParagraphs() => FolioScene.RunAsync(() =>
    {
        _ = new FolioScene(Paragraphs(20));
        var small = Measure(250);
        var large = Measure(1000);
        Record($"scene 250={small.TotalMilliseconds:0} 1000={large.TotalMilliseconds:0}");

        Assert.True(large.TotalMilliseconds < 12 * Math.Max(small.TotalMilliseconds, 50), $"250 paragraphs {small}, 1000 paragraphs {large}");
        return Task.CompletedTask;
    });

    [Fact]
    public Task TypingInAFiveHundredPageDocumentStaysResponsive() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(Paragraphs(6000));
        Assert.True(scene.Editor.Layout.Pages.Count >= 500, $"{scene.Editor.Layout.Pages.Count} pages");
        scene.Type("a");
        var clock = Stopwatch.StartNew();
        const int keys = 20;
        for (var i = 0; i < keys; i++) scene.Type("x");
        var perKey = clock.Elapsed / keys;
        Record($"pages={scene.Editor.Layout.Pages.Count} typing per key={perKey.TotalMilliseconds:0.0}");

        Assert.StartsWith("ax", scene.Paragraphs[0], StringComparison.Ordinal);
        Assert.True(perKey < TimeSpan.FromMilliseconds(250), $"{perKey.TotalMilliseconds:0} ms per key");
        return Task.CompletedTask;
    });

    [Fact]
    public Task PageStatusOfACaretStaysCheapInAFiveHundredPageDocument() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(Paragraphs(6000));
        var pages = scene.Editor.Layout.Pages;
        Assert.True(pages.Count >= 500, $"{pages.Count} pages");
        var jump = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Editing.IFolioPageJump>();
        var last = pages[^1].Lines[^1];
        var at = new PulseOffice.Documents.Editing.DocumentPosition(last.ParagraphId, last.Line.Start);
        var first = new PulseOffice.Documents.Editing.DocumentPosition(pages[0].Lines[0].ParagraphId, 0);
        Assert.Equal(pages.Count, jump.PageOf(scene.Editor, at));
        Assert.Equal(1, jump.PageOf(scene.Editor, first));

        const int moves = 200;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < moves; i++) _ = jump.PageOf(scene.Editor, i % 2 == 0 ? at : first);
        var perMove = clock.Elapsed / moves;
        Record($"pages={pages.Count} page status per caret move={perMove.TotalMilliseconds:0.000}");

        Assert.True(perMove < TimeSpan.FromMilliseconds(2), $"{perMove.TotalMilliseconds:0.000} ms per caret move");
        return Task.CompletedTask;
    });

    [Fact]
    public Task RecoverySnapshotOfAFiveHundredPageDocumentIsBounded() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene(Paragraphs(6000));
        var provider = scene.Services.GetServices<PulseOffice.Shell.IDocumentSnapshotProvider>().Single(candidate => candidate.Module == PulseOffice.Foundation.Modules.ModuleKind.Documents);
        var open = new PulseOffice.Shell.OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Documents, "Lungo", scene.View, scene.History) { Model = scene.Document };
        using (var warm = new MemoryStream()) await provider.Capture(open).WriteAsync(warm);

        var clock = Stopwatch.StartNew();
        var capture = provider.Capture(open);
        var captured = clock.Elapsed;
        clock.Restart();
        using var stream = new MemoryStream();
        await capture.WriteAsync(stream);
        var serialized = clock.Elapsed;
        Record($"pages={scene.Editor.Layout.Pages.Count} recovery capture={captured.TotalMilliseconds:0.0} serialization={serialized.TotalMilliseconds:0} bytes={stream.Length}");

        Assert.True(captured < TimeSpan.FromMilliseconds(60), $"capture {captured.TotalMilliseconds:0.0} ms");
        Assert.True(serialized < TimeSpan.FromMilliseconds(1500), $"serialization {serialized.TotalMilliseconds:0} ms");
        Assert.True(stream.Length < 6_000_000, $"{stream.Length} bytes");
    });

    private static TimeSpan Measure(int count)
    {
        var clock = Stopwatch.StartNew();
        _ = new FolioScene(Paragraphs(count));
        return clock.Elapsed;
    }
}
