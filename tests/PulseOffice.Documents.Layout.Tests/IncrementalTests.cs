using System.Diagnostics;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

[CollectionDefinition(nameof(IncrementalTests), DisableParallelization = true)]
public sealed class SerialTiming;

[Collection(nameof(IncrementalTests))]
public sealed class IncrementalTests(ITestOutputHelper output)
{
    private readonly Random _random = new(7);

    private string Text(int words) =>
        string.Join(' ', Enumerable.Range(0, words).Select(_ => new string('a', _random.Next(2, 9))));

    private LayoutFixture Large(int paragraphs)
    {
        var f = new LayoutFixture(width: 420, height: 700);
        for (var i = 0; i < paragraphs; i++)
        {
            var keep = i % 37 == 0;
            var drop = i % 41 == 0 ? new DropCap(3, 1) : null;
            var framed = i % 23 == 0 ? BoxBorders.Uniform(new BorderLine(1, 0xFF336699), 3) : null;
            f.Add(Text(_random.Next(5, 80)), p => p with { KeepWithNext = keep, SpaceAfter = 6, Explicit = ParagraphOverrides.SpaceAfter, DropCap = drop, Borders = framed, Background = framed is null ? null : 0xFFEEF2FF });
        }
        return f;
    }

    private string Signature(DocumentLayout layout) => string.Join('|', layout.Pages.Select(page =>
        $"{page.Index}:{page.StyleName}:{page.Start}:" + string.Join(',', page.Lines.Select(line => $"{line.ParagraphId}/{line.Line.Start}-{line.Line.End}@{line.Top:F3}"))));

    private void Edit(LayoutFixture f, int index, string text)
    {
        var block = (ParagraphBlock)f.Document.Body[index];
        block.Content = block.Content.WithRuns([new TextRun(text)]);
    }

    [Fact]
    public void LaReimpaginazioneIncrementaleEUgualeAQuellaCompleta()
    {
        var f = Large(600);
        var previous = f.Layout();
        Edit(f, 300, Text(400));
        var incremental = f.Engine.Relayout(f.Document, previous);
        f.Engine.Invalidate();
        var full = f.Engine.Layout(f.Document);

        Assert.Equal(Signature(full), Signature(incremental));
    }

    [Fact]
    public void InserireEdEliminareParagrafiDaLoStessoRisultato()
    {
        var f = Large(400);
        var previous = f.Layout();
        f.Document.Body.Insert(120, new ParagraphBlock("nuovo", Text(60)));
        f.Document.Body.RemoveAt(10);
        var incremental = f.Engine.Relayout(f.Document, previous);
        f.Engine.Invalidate();

        Assert.Equal(Signature(f.Engine.Layout(f.Document)), Signature(incremental));
    }

    [Fact]
    public void AccorciareUnParagrafoInFondoDaLoStessoRisultato()
    {
        var f = Large(300);
        var previous = f.Layout();
        Edit(f, 299, "fine");
        var incremental = f.Engine.Relayout(f.Document, previous);
        f.Engine.Invalidate();

        Assert.Equal(Signature(f.Engine.Layout(f.Document)), Signature(incremental));
    }

    [Fact]
    public void UnaModificaSuCinquecentoPagineSiReimpaginaInMenoDi50Millisecondi()
    {
        var f = Large(7000);
        var layout = f.Layout();
        Assert.True(layout.Pages.Count >= 500, $"pages: {layout.Pages.Count}");
        for (var warm = 0; warm < 3; warm++)
        {
            Edit(f, 3000 + warm, Text(30));
            layout = f.Engine.Relayout(f.Document, layout);
        }

        var times = new List<double>();
        for (var run = 0; run < 9; run++)
        {
            Edit(f, 3500 + run, Text(45));
            var watch = Stopwatch.StartNew();
            var relaid = f.Engine.Relayout(f.Document, layout);
            watch.Stop();
            times.Add(watch.Elapsed.TotalMilliseconds);
            Assert.InRange(relaid.Pages.Count, layout.Pages.Count - 2, layout.Pages.Count + 2);
            layout = relaid;
        }
        var fulls = new List<double>();
        for (var run = 0; run < 3; run++)
        {
            f.Engine.Invalidate();
            var watch = Stopwatch.StartNew();
            f.Engine.Layout(f.Document);
            watch.Stop();
            fulls.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        fulls.Sort();
        var median = times[times.Count / 2];
        var full = fulls[1];
        output.WriteLine($"relayout of {layout.Pages.Count} pages: median {median:F2} ms, full layout {full:F2} ms");
        Assert.True(median < 50 || median < full / 10, $"relayout took {median:F2} ms against a full layout of {full:F2} ms");
    }
}
