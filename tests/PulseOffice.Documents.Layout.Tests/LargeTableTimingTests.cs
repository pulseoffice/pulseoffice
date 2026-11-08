using System.Diagnostics;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

[Collection(nameof(IncrementalTests))]
public sealed class LargeTableTimingTests(ITestOutputHelper output)
{
    private TableCell Cell(LayoutFixture f, string text) =>
        new(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(text) { Runs = [new TextRun(text)] })]));

    [Fact]
    public void UnaTabellaDiTremilaRigheSiImpaginaESiReimpaginaInTempi()
    {
        var f = new LayoutFixture(width: 420, height: 700);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 2, BorderWidth = 0.5, BorderColor = 0xFF000000, BandBackground = 0xFFEEEEEE } });
        f.Add("prima");
        var rows = Enumerable.Range(0, 3000).Select(r => new TableRow([Cell(f, $"riga {r}"), Cell(f, (r * 3).ToString(System.Globalization.CultureInfo.InvariantCulture)), Cell(f, "alfa beta gamma delta"), Cell(f, "x")]));
        var table = new TableBlock("big", rows, [Twip.FromPoints(100), Twip.FromPoints(80), Twip.FromPoints(160), Twip.FromPoints(60)]) { StyleName = "T", HeaderRows = 1 };
        f.Document.Body.Add(table);

        var watch = Stopwatch.StartNew();
        var layout = f.Layout();
        var full = watch.Elapsed.TotalMilliseconds;
        Assert.True(layout.Pages.Count > 50, $"pages: {layout.Pages.Count}");

        var times = new List<double>();
        for (var run = 0; run < 5; run++)
        {
            var paragraph = (ParagraphBlock)table.Cell(1500 + run, 2).Content[0];
            paragraph.Content = paragraph.Content.WithRuns([new TextRun("modificata " + run)]);
            watch.Restart();
            layout = f.Engine.Relayout(f.Document, layout);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        var median = times[times.Count / 2];
        output.WriteLine($"table of 3000 rows on {layout.Pages.Count} pages: full {full:F1} ms, relayout median {median:F1} ms");
        Assert.True(median < 1500, $"relayout took {median:F1} ms");
        Assert.All(layout.Pages.Skip(1), page => Assert.Equal(0, page.Tables.SelectMany(box => box.Cells).Min(cell => cell.Row)));
    }
}
