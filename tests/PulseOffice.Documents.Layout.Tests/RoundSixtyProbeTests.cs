using System.Diagnostics;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

[Collection(nameof(IncrementalTests))]
public sealed class RoundSixtyProbeTests(ITestOutputHelper output)
{
    private static async Task<T> Bounded<T>(Func<T> action)
    {
        var task = Task.Run(action);
        var done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.True(done == task, "layout did not finish");
        return await task;
    }

    private static TableCell Cell(IEnumerable<string> texts) => new(new BlockContainer([.. texts.Select((text, i) => new ParagraphBlock($"c{Guid.NewGuid():N}", text))]));

    [Fact]
    public async Task TremilaParagrafiLegatiAlSuccessivoNonDiventanoQuadratici()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        for (var i = 0; i < 3000; i++) f.Add("alfa beta gamma delta", p => p with { KeepWithNext = true });
        var watch = Stopwatch.StartNew();
        var layout = await Bounded(f.Layout);
        output.WriteLine($"{layout.Pages.Count} pages in {watch.ElapsedMilliseconds} ms");
        Assert.True(layout.Pages.Count > 10);
        Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task UnaNotaPiuAltaDellaPaginaSiSpezzaETermina()
    {
        var f = new LayoutFixture(width: 200, height: 100);
        var block = f.Add("testo");
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([.. Enumerable.Range(0, 200).Select(i => new ParagraphBlock($"np{i}", "nota lunga"))])));
        block.Marks = [new NoteMark(block.Length, "n1")];
        f.Add("dopo");
        var layout = await Bounded(f.Layout);
        Assert.Contains(f.Lines(layout), line => line.ParagraphId == block.Id);
    }

    [Fact]
    public async Task QuarantacinqueColonneConSpazioPiuLargoDellaPaginaTerminano()
    {
        var f = new LayoutFixture(width: 200, height: 100);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.Columns = new SectionColumns(45, Twip.FromPoints(400));
        for (var i = 0; i < 20; i++) f.Add("uno due tre");
        var content = new BlockContainer([.. Enumerable.Range(0, 30).Select(i => new ParagraphBlock($"s{i}", "quattro cinque"))]);
        f.Document.Body.Add(new SectionBlock("sec", "S", content) { Columns = new SectionColumns(45, Twip.FromPoints(-50)) });
        var layout = await Bounded(f.Layout);
        Assert.All(f.Lines(layout), line => Assert.True(double.IsFinite(line.Left) && double.IsFinite(line.Top)));
    }

    [Fact]
    public async Task UnaCellaConDuemilaParagrafiSiSpezzaSuPiuPagine()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var header = new TableRow([Cell([.. Enumerable.Range(0, 30).Select(_ => "testata")])]);
        var tall = new TableRow([Cell([.. Enumerable.Range(0, 2000).Select(_ => "riga")])]);
        f.Document.Body.Add(new TableBlock("t", [header, tall], [Twip.FromPoints(150)]) { HeaderRows = 1 });
        f.Add("dopo");
        var watch = Stopwatch.StartNew();
        var layout = await Bounded(f.Layout);
        output.WriteLine($"{layout.Pages.Count} pages in {watch.ElapsedMilliseconds} ms");
        Assert.Contains(f.LineTexts(layout), text => text == "dopo");
    }

    [Fact]
    public async Task UnParagrafoTenutoInsiemePiuAltoDellaPaginaTermina()
    {
        var f = new LayoutFixture(width: 60, height: 60);
        f.Add("a");
        f.Add(string.Join(' ', Enumerable.Range(0, 400).Select(_ => "xx")), p => p with { KeepTogether = true, KeepWithNext = true, Orphans = 50, Widows = 50 });
        f.Add("b", p => p with { KeepWithNext = true, KeepTogether = true });
        var layout = await Bounded(f.Layout);
        Assert.Contains(f.LineTexts(layout), text => text == "b");
    }

    [Fact]
    public void UnaRigaDiAltezzaEsattaNegativaNonFaRisalireIlTestoSuccessivo()
    {
        var f = new LayoutFixture(width: 200, height: 300);
        var before = f.Add("prima");
        f.Document.Body.Add(new TableBlock("t", [new TableRow([Cell(["cella"])]) { Height = Twip.FromPoints(-500), MinimumHeight = false }], [Twip.FromPoints(100)]));
        var after = f.Add("dopo");

        var layout = f.Layout();

        var table = layout.Pages[0].Tables.Single();
        Assert.True(table.Bounds.Height >= 0, $"table height {table.Bounds.Height}");
        var first = f.Lines(layout).Single(line => line.ParagraphId == before.Id);
        var last = f.Lines(layout).Single(line => line.ParagraphId == after.Id);
        Assert.True(last.Top >= first.Top + first.Line.Height - 0.001, $"after at {last.Top}");
    }

    [Fact]
    public void UnaInterlineaNegativaEnormeNonFaRisalireLeRighe()
    {
        var f = new LayoutFixture(width: 60, height: 300);
        var block = f.Add("uno due tre quattro cinque sei sette otto");
        var after = f.Add("dopo");
        f.Settings.Spacing[block.Id] = new PulseOffice.Documents.Layout.Lines.LeadingSpacing(-1000);

        var layout = f.Layout();

        var lines = f.Lines(layout);
        Assert.All(lines, line => Assert.True(line.Line.Height >= 0, $"height {line.Line.Height}"));
        Assert.All(lines, line => Assert.InRange(line.Top, 0, 300));
        Assert.True(lines.Single(line => line.ParagraphId == after.Id).Top >= lines.Where(line => line.ParagraphId == block.Id).Max(line => line.Top));
    }

    [Fact]
    public void UnaFiligranaConAngoloNonNumericoRestaFinita()
    {
        var f = new LayoutFixture(width: 200, height: 300);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.Watermark = new PageWatermark("BOZZA") { Angle = double.NaN, Transparency = double.NaN };
        f.Add("testo");

        var mark = f.Layout().Pages[0].Backdrop.OfType<PulseOffice.Documents.Layout.Tree.WatermarkBox>().Single();

        Assert.True(double.IsFinite(mark.Run.Font.Size) && double.IsFinite(mark.X) && double.IsFinite(mark.Baseline) && double.IsFinite(mark.Angle));
    }

    [Fact]
    public void UnOggettoConRotazioneNonNumericaContinuaAScostareIlTesto()
    {
        var f = new LayoutFixture(width: 200, height: 300);
        var block = f.Add(string.Join(' ', Enumerable.Range(0, 60).Select(_ => "parola")));
        f.Document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("o", new PulseOffice.Documents.Objects.CharacterAnchor(block.Id), new PulseOffice.Documents.Objects.TextBoxContent(new BlockContainer([new ParagraphBlock("fp", "x")])), new DocumentSize(Twip.FromPoints(80), Twip.FromPoints(80)))
        {
            Wrap = PulseOffice.Documents.Objects.WrapMode.Parallel,
            RotationDegrees = double.NaN,
            WrapContour = true,
        });

        var layout = f.Layout();

        var item = layout.Pages[0].Objects.Single(box => box.ObjectId == "o");
        Assert.True(double.IsFinite(item.RotatedBounds.Width) && double.IsFinite(item.RotatedBounds.Height));
        var beside = f.Lines(layout).Where(line => line.Top < item.Bounds.Bottom && line.Top + line.Line.Height > item.Bounds.Y);
        Assert.All(beside, line => Assert.False(line.Left < item.Bounds.Right && line.Left + line.Line.Runs.Sum(run => run.Width) > item.Bounds.X + 0.001));
    }

    [Fact]
    public async Task UnaTabellaAnnidataPiuAltaDellaPaginaTermina()
    {
        var f = new LayoutFixture(width: 200, height: 100);
        var inner = new TableBlock("in", [new TableRow([Cell([.. Enumerable.Range(0, 100).Select(_ => "dentro")])]) { Height = Twip.FromPoints(-40) }], [Twip.FromPoints(100)]);
        var outer = new TableRow([new TableCell(new BlockContainer([inner]))]);
        f.Document.Body.Add(new TableBlock("out", [outer], [Twip.FromPoints(150)]));
        f.Add("dopo");
        var layout = await Bounded(f.Layout);
        Assert.Contains(f.LineTexts(layout), text => text == "dopo");
    }

    [Fact]
    public async Task UnaSezioneBilanciataLungaNonDiventaQuadratica()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        var content = new BlockContainer([.. Enumerable.Range(0, 4000).Select(i => new ParagraphBlock($"s{i}", "alfa beta gamma delta epsilon"))]);
        f.Document.Body.Add(new SectionBlock("sec", "S", content) { Columns = new SectionColumns(3, Twip.FromPoints(10)) });
        var watch = Stopwatch.StartNew();
        var layout = await Bounded(f.Layout);
        output.WriteLine($"{layout.Pages.Count} pages in {watch.ElapsedMilliseconds} ms");
        Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(-50)]
    public void LeColonneConUnoSpazioAssurdoRestanoNellaPaginaESenzaSovrapporsi(double gap)
    {
        var f = new LayoutFixture(width: 200, height: 100);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.Columns = new SectionColumns(4, Twip.FromPoints(gap));
        for (var i = 0; i < 40; i++) f.Add("uno due tre");

        var layout = f.Layout();

        var lefts = layout.Pages[0].Lines.Select(line => line.Left).Distinct().Order().ToList();
        Assert.Equal(4, lefts.Count);
        Assert.All(f.Lines(layout), line => Assert.InRange(line.Left, 0, 200 - 1));
        var columns = layout.Pages[0].Columns;
        for (var c = 1; c < columns.Count; c++) Assert.True(columns[c].Bounds.X >= columns[c - 1].Bounds.Right - 0.001);
        Assert.True(columns[^1].Bounds.Right <= 200 + 0.001);
    }

    [Fact]
    public void UnMilioneDiColonneDalModelloSiFermaAlMassimo()
    {
        var f = new LayoutFixture(width: 200, height: 100);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.Columns = new SectionColumns(1_000_000, Twip.Zero);
        f.Add("uno");

        var layout = f.Layout();

        Assert.InRange(layout.Pages[0].Columns.Count, 1, SectionColumns.LargestCount);
    }

    [Fact]
    public async Task UnParagrafoDiCentomilaParoleSuMillePagineNonEQuadratico()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        f.Add(string.Join(' ', Enumerable.Range(0, 100_000).Select(i => "parola")), p => p with { Borders = BoxBorders.Uniform(new BorderLine(1, 0xFF000000), 2) });
        var watch = Stopwatch.StartNew();
        var layout = await Bounded(f.Layout);
        output.WriteLine($"{layout.Pages.Count} pages in {watch.ElapsedMilliseconds} ms");
        Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void UnaRigaInPiuAPaginaDieciNonReimpaginaTutto()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        for (var i = 0; i < 12000; i++) f.Add("alfa beta gamma delta epsilon zeta eta theta iota kappa lambda mu");
        f.Layout();
        f.Engine.Invalidate();
        var watchFull = Stopwatch.StartNew();
        var layout = f.Layout();
        watchFull.Stop();
        var times = new List<double>();
        for (var run = 0; run < 5; run++)
        {
            var block = (ParagraphBlock)f.Document.Body[200 + run];
            block.Content = block.Content.WithRuns([new PulseOffice.Text.Rich.TextRun(block.Content.Text + " nu xi omicron pi rho sigma tau upsilon phi chi psi omega alfa beta gamma")]);
            var watch = Stopwatch.StartNew();
            layout = f.Engine.Relayout(f.Document, layout);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        output.WriteLine($"{layout.Pages.Count} pages, full {watchFull.ElapsedMilliseconds} ms, relayout median {times[2]:F1} ms");
        Assert.True(times[2] < watchFull.ElapsedMilliseconds / 5.0, $"{times[2]} vs {watchFull.ElapsedMilliseconds}");
    }
}
