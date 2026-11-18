using PulseOffice.Testing;
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
        _ = BuildAndExport(20);
        var small = BuildAndExport(200);
        var large = BuildAndExport(2000);
        Record($"paragraphs=2000 pages={large.Pages} total={large.Time.TotalMilliseconds:0} paragraphs=200 total={small.Time.TotalMilliseconds:0}");

        Assert.True(large.Pages > 100, $"{large.Pages} pages");
        new GrowthCheck(TimeSpan.FromMilliseconds(50), 4).Bounded(small.Time, large.Time, 10, $"200 paragraphs {small.Time}, 2000 paragraphs {large.Time}");
        return Task.CompletedTask;
    });

    private static (int Pages, TimeSpan Time) BuildAndExport(int paragraphs)
    {
        var clock = Stopwatch.StartNew();
        var scene = new FolioScene(Paragraphs(paragraphs));
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(printable, stream, new PdfExportOptions("Lungo"));
        Assert.True(result.Succeeded);
        return (printable.PageCount, clock.Elapsed);
    }

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
        var small = TypingPerKey(600);
        var large = TypingPerKey(6000);
        Record($"typing per key 600 paragraphs={small.TotalMilliseconds:0.0} 6000 paragraphs={large.TotalMilliseconds:0.0}");

        new GrowthCheck(TimeSpan.FromMilliseconds(5), 4).Bounded(small, large, 1, $"{small.TotalMilliseconds:0.0} ms per key at 600 paragraphs, {large.TotalMilliseconds:0.0} ms at 6000");
        return Task.CompletedTask;
    });

    private static TimeSpan TypingPerKey(int paragraphs)
    {
        var scene = new FolioScene(Paragraphs(paragraphs));
        if (paragraphs >= 6000) Assert.True(scene.Editor.Layout.Pages.Count >= 500, $"{scene.Editor.Layout.Pages.Count} pages");
        scene.Type("a");
        const int keys = 20;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < keys; i++) scene.Type("x");
        var perKey = clock.Elapsed / keys;
        Assert.StartsWith("ax", scene.Paragraphs[0], StringComparison.Ordinal);
        return perKey;
    }

    [Fact]
    public Task PageStatusOfACaretStaysCheapInAFiveHundredPageDocument() => FolioScene.RunAsync(() =>
    {
        var small = PageStatusMoves(600);
        var large = PageStatusMoves(6000);
        Record($"page status for 200 caret moves 600 paragraphs={small.TotalMilliseconds:0.000} 6000 paragraphs={large.TotalMilliseconds:0.000}");

        new GrowthCheck().Bounded(small, large, 1, $"200 caret moves: {small.TotalMilliseconds:0.000} ms at 600 paragraphs, {large.TotalMilliseconds:0.000} ms at 6000");
        return Task.CompletedTask;
    });

    private static TimeSpan PageStatusMoves(int paragraphs)
    {
        var scene = new FolioScene(Paragraphs(paragraphs));
        var pages = scene.Editor.Layout.Pages;
        if (paragraphs >= 6000) Assert.True(pages.Count >= 500, $"{pages.Count} pages");
        var jump = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Editing.IFolioPageJump>();
        var last = pages[^1].Lines[^1];
        var at = new PulseOffice.Documents.Editing.DocumentPosition(last.ParagraphId, last.Line.Start);
        var first = new PulseOffice.Documents.Editing.DocumentPosition(pages[0].Lines[0].ParagraphId, 0);
        Assert.Equal(pages.Count, jump.PageOf(scene.Editor, at));
        Assert.Equal(1, jump.PageOf(scene.Editor, first));

        const int moves = 200;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < moves; i++) _ = jump.PageOf(scene.Editor, i % 2 == 0 ? at : first);
        return clock.Elapsed;
    }

    [Fact]
    public Task RecoverySnapshotOfAFiveHundredPageDocumentIsBounded() => FolioScene.RunAsync(async () =>
    {
        var small = await Recovery(600);
        var large = await Recovery(6000);
        Record($"recovery 600 paragraphs capture={small.Captured.TotalMilliseconds:0.0} serialization={small.Serialized.TotalMilliseconds:0}; 6000 paragraphs capture={large.Captured.TotalMilliseconds:0.0} serialization={large.Serialized.TotalMilliseconds:0} bytes={large.Bytes}");

        var growth = new GrowthCheck(TimeSpan.FromMilliseconds(5), 4);
        growth.Bounded(small.Captured, large.Captured, 10, $"capture {small.Captured.TotalMilliseconds:0.0} ms at 600, {large.Captured.TotalMilliseconds:0.0} ms at 6000");
        growth.Bounded(small.Serialized, large.Serialized, 10, $"serialization {small.Serialized.TotalMilliseconds:0} ms at 600, {large.Serialized.TotalMilliseconds:0} ms at 6000");
        Assert.True(large.Bytes < 6_000_000, $"{large.Bytes} bytes");
    });

    private static async Task<(TimeSpan Captured, TimeSpan Serialized, long Bytes)> Recovery(int paragraphs)
    {
        var scene = new FolioScene(Paragraphs(paragraphs));
        var provider = scene.Services.GetServices<PulseOffice.Shell.IDocumentSnapshotProvider>().Single(candidate => candidate.Module == PulseOffice.Foundation.Modules.ModuleKind.Documents);
        var open = new PulseOffice.Shell.OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Documents, "Lungo", scene.View, scene.History) { Model = scene.Document };
        using (var warm = new MemoryStream()) await provider.Capture(open).WriteAsync(warm);

        var clock = Stopwatch.StartNew();
        var capture = provider.Capture(open);
        var captured = clock.Elapsed;
        clock.Restart();
        using var stream = new MemoryStream();
        await capture.WriteAsync(stream);
        return (captured, clock.Elapsed, stream.Length);
    }

    private static TimeSpan Measure(int count)
    {
        var clock = Stopwatch.StartNew();
        _ = new FolioScene(Paragraphs(count));
        return clock.Elapsed;
    }
}
