using System.Diagnostics;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

[Collection(nameof(IncrementalTests))]
public sealed class ManyObjectsTimingTests(ITestOutputHelper output)
{
    private string Shape(DocumentLayout layout) => string.Join('|', layout.Pages.Select(page =>
        page.Number + ":" + string.Join(',', page.Lines.Select(line => $"{line.ParagraphId}@{line.Left:F1},{line.Top:F1}")) + ";" + string.Join(',', page.Objects.Select(item => $"{item.Bounds.X:F1},{item.Bounds.Y:F1}"))));

    private string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(index => "parola" + index % 7));

    [Fact]
    public void CinquecentoOggettiConContornoSiImpaginanoESiReimpaginanoInTempi()
    {
        var f = new LayoutFixture(width: 420, height: 600);
        var modes = new[] { WrapMode.Parallel, WrapMode.Optimal, WrapMode.Left, WrapMode.Right, WrapMode.TopAndBottom };
        for (var index = 0; index < 500; index++)
        {
            var block = f.Add(Words(80));
            f.Document.Objects.Add(new AnchoredObject($"o{index}", new ParagraphAnchor(block.Id),
                new EmbeddedContent(new PulseOffice.Drawing.Shapes.ShapeObject { Id = $"s{index}", Shape = index % 2 == 0 ? PulseOffice.Drawing.Shapes.ShapeKind.Ellipse : PulseOffice.Drawing.Shapes.ShapeKind.Rectangle }),
                new DocumentSize(Twip.FromPoints(80), Twip.FromPoints(60)))
            {
                Placement = new ObjectPlacement(Twip.FromPoints(index % 3 * 150), Twip.FromPoints(10)),
                Wrap = modes[index % modes.Length],
                WrapContour = index % 2 == 0,
                ZOrder = index,
            });
        }

        var watch = Stopwatch.StartNew();
        var layout = f.Layout();
        var full = watch.Elapsed.TotalMilliseconds;
        Assert.True(layout.Pages.Count > 50, $"pages: {layout.Pages.Count}");
        Assert.Equal(500, layout.Pages.Sum(page => page.Objects.Count));

        var times = new List<double>();
        for (var run = 0; run < 5; run++)
        {
            var paragraph = (ParagraphBlock)f.Document.Body[400 + run];
            paragraph.Content = paragraph.Content.WithRuns([new TextRun(Words(70) + " modificata " + run)]);
            watch.Restart();
            layout = f.Engine.Relayout(f.Document, layout);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        var median = times[times.Count / 2];

        var objectTimes = new List<double>();
        for (var run = 0; run < 5; run++)
        {
            var item = f.Document.Objects.Find($"o{420 + run}")!;
            item.Wrap = run % 2 == 0 ? WrapMode.None : WrapMode.Through;
            item.Placement = item.Placement with { X = Twip.FromPoints(run * 20) };
            watch.Restart();
            layout = f.Engine.Relayout(f.Document, layout);
            objectTimes.Add(watch.Elapsed.TotalMilliseconds);
        }
        objectTimes.Sort();
        var objectMedian = objectTimes[objectTimes.Count / 2];
        Assert.Equal(Shape(f.Layout()), Shape(layout));

        output.WriteLine($"500 objects on {layout.Pages.Count} pages: full {full:F1} ms, relayout median {median:F1} ms, object change median {objectMedian:F1} ms");
        Assert.True(full < 15000, $"layout took {full:F1} ms");
        Assert.True(median < 3000, $"relayout took {median:F1} ms");
        Assert.True(objectMedian < full / 2, $"object relayout took {objectMedian:F1} ms against {full:F1} ms");
    }
}
