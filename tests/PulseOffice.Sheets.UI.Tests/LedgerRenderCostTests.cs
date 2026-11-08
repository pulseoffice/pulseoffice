using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Sheets.Values;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LedgerPerformanceCollection
{
    public const string Name = "Ledger performance";
}

[Collection(LedgerPerformanceCollection.Name)]
public sealed class LedgerRenderCostTests
{
    private const int UsedRows = 1_000_000;

    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private static void Record(string line) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-ledger-render.txt"), $"{DateTimeOffset.Now:O} {line}\n");

    private sealed class Scene
    {
        public Scene()
        {
            var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
            Engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
            var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
            var status = new PulseOffice.UI.Controls.Services.StatusBarService(
                TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
            var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
                System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)));
            Grid = new SheetGridView(Engine, presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), new SheetSelection());
            Writer = new LedgerCellWriter(Workbook, Engine, History, status, texts, new PulseOffice.Shell.DocumentWorkspace(), Grid,
                null, null, null, null);
        }

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public UndoStack History { get; } = new();

        public CalculationEngine Engine { get; }

        public SheetGridView Grid { get; }

        public LedgerCellWriter Writer { get; }

        public Worksheet Sheet => Workbook.Sheets[0];
    }

    private static TimeSpan Frames(SheetGridView grid, RenderTargetBitmap bitmap, int count)
    {
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < count; i++) bitmap.Render(grid);
        return clock.Elapsed / count;
    }

    [Fact]
    public Task A_million_used_rows_paint_scroll_and_type_in_bounded_time() => Session.Dispatch(() =>
    {
        var scene = new Scene();
        for (var row = 0; row < UsedRows; row++) scene.Sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        for (var row = 0; row < UsedRows; row += 1000) scene.Sheet.SetValue(new CellAddress(row, 1), CellValue.Text("riga"));

        var grid = scene.Grid;
        grid.Measure(new Size(1200, 800));
        grid.Arrange(new Rect(0, 0, 1200, 800));
        using var bitmap = new RenderTargetBitmap(new PixelSize(1200, 800));
        bitmap.Render(grid);

        var top = Frames(grid, bitmap, 10);
        grid.EnsureVisible(new CellAddress(UsedRows - 10, 0));
        var bottom = Frames(grid, bitmap, 10);

        var clock = Stopwatch.StartNew();
        const int scrolls = 40;
        for (var i = 0; i < scrolls; i++)
        {
            grid.EnsureVisible(new CellAddress(UsedRows / 2 + i * 37, 0));
            bitmap.Render(grid);
        }
        var scroll = clock.Elapsed / scrolls;

        clock.Restart();
        const int keys = 20;
        for (var i = 0; i < keys; i++)
        {
            scene.Writer.Write((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), new CellAddress(UsedRows / 2, 2), 0);
            bitmap.Render(grid);
        }
        var typing = clock.Elapsed / keys;

        Record($"rows={UsedRows} frame top={top.TotalMilliseconds:0.0} bottom={bottom.TotalMilliseconds:0.0} scroll={scroll.TotalMilliseconds:0.0} typing={typing.TotalMilliseconds:0.0}");

        Assert.Equal(20, scene.Sheet.GetValue(new CellAddress(UsedRows / 2, 2)).AsNumber());
        Assert.True(bottom.TotalMilliseconds < 4 * Math.Max(top.TotalMilliseconds, 10), $"top {top}, bottom {bottom}");
        Assert.True(scroll < TimeSpan.FromMilliseconds(150), $"scroll frame {scroll.TotalMilliseconds:0} ms");
        Assert.True(typing < TimeSpan.FromMilliseconds(250), $"typing {typing.TotalMilliseconds:0} ms per key");
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Frozen_panes_over_a_million_rows_paint_and_scroll_like_the_plain_grid() => Session.Dispatch(() =>
    {
        var scene = new Scene();
        for (var row = 0; row < UsedRows; row++) scene.Sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        scene.Sheet.Frozen = new FrozenPanes(3, 2);

        var grid = scene.Grid;
        grid.Measure(new Size(1200, 800));
        grid.Arrange(new Rect(0, 0, 1200, 800));
        grid.Refresh();
        using var bitmap = new RenderTargetBitmap(new PixelSize(1200, 800));
        bitmap.Render(grid);

        var top = Frames(grid, bitmap, 10);
        grid.EnsureVisible(new CellAddress(UsedRows - 10, 0));
        var bottom = Frames(grid, bitmap, 10);
        var clock = Stopwatch.StartNew();
        const int scrolls = 40;
        for (var i = 0; i < scrolls; i++)
        {
            grid.EnsureVisible(new CellAddress(UsedRows / 2 + i * 37, 5));
            bitmap.Render(grid);
        }
        var scroll = clock.Elapsed / scrolls;
        Record($"rows={UsedRows} frozen=3x2 frame top={top.TotalMilliseconds:0.0} bottom={bottom.TotalMilliseconds:0.0} scroll={scroll.TotalMilliseconds:0.0}");

        Assert.True(bottom.TotalMilliseconds < 4 * Math.Max(top.TotalMilliseconds, 10), $"top {top}, bottom {bottom}");
        Assert.True(scroll < TimeSpan.FromMilliseconds(150), $"scroll frame {scroll.TotalMilliseconds:0} ms");
    }, TestContext.Current.CancellationToken);
}
