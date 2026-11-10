using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerGridRound19Tests
{
    private sealed class Parts
    {
        public Parts()
        {
            var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
            Engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
            var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
            var status = new PulseOffice.UI.Controls.Services.StatusBarService(
                TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
            var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
                System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)));
            var selection = new SheetSelection();
            Grid = new SheetGridView(Engine, presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), selection);
            var writer = new LedgerCellWriter(Workbook, Engine, History, status, texts, new PulseOffice.Shell.DocumentWorkspace(), Grid,
                null, null, null, null);
            Operations = new LedgerCellOperations(Workbook, Engine, Grid, selection, History, status, texts, writer, new PulseOffice.Sheets.UI.Outline.OutlineGutter());
        }

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public UndoStack History { get; } = new();

        public CalculationEngine Engine { get; }

        public SheetGridView Grid { get; }

        public LedgerCellOperations Operations { get; }

        public Worksheet Sheet => Workbook.Sheets[0];
    }

    private static CellRange Columns(int first, int last) =>
        new(new CellAddress(0, first), new CellAddress(CellAddress.MaxRows - 1, last));

    [Fact]
    public void Filling_a_whole_column_sideways_works_only_on_the_used_rows()
    {
        var parts = new Parts();
        parts.Sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        parts.Sheet.SetValue(new CellAddress(4, 1), CellValue.Text("x"));

        var clock = Stopwatch.StartNew();
        parts.Operations.ApplyFill(new SheetGridView.FillRequest(Columns(0, 0), Columns(0, 2)));
        clock.Stop();

        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(500), clock.Elapsed.ToString());
        Assert.Equal(1, parts.Sheet.GetValue(new CellAddress(0, 1)).AsNumber());
        Assert.Equal(1, parts.Sheet.GetValue(new CellAddress(0, 2)).AsNumber());
        Assert.True(parts.Sheet.GetValue(new CellAddress(4, 1)).IsEmpty);
        Assert.True(parts.History.CanUndo);
    }

    [Fact]
    public void Filling_whole_rows_downwards_works_only_on_the_used_columns()
    {
        var parts = new Parts();
        parts.Sheet.SetValue(new CellAddress(0, 3), CellValue.Number(7));
        var row = new CellRange(new CellAddress(0, 0), new CellAddress(0, CellAddress.MaxColumns - 1));
        var rows = new CellRange(new CellAddress(0, 0), new CellAddress(3, CellAddress.MaxColumns - 1));

        var clock = Stopwatch.StartNew();
        parts.Operations.ApplyFill(new SheetGridView.FillRequest(row, rows));
        clock.Stop();

        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(500), clock.Elapsed.ToString());
        Assert.Equal(7, parts.Sheet.GetValue(new CellAddress(3, 3)).AsNumber());
    }

    [Fact]
    public void Filling_an_empty_sheet_whole_column_does_nothing()
    {
        var parts = new Parts();

        parts.Operations.ApplyFill(new SheetGridView.FillRequest(Columns(0, 0), Columns(0, 3)));

        Assert.False(parts.History.CanUndo);
    }

    [Fact]
    public void The_row_and_column_highlight_follows_the_theme_text_colour()
    {
        var parts = new Parts();
        var viewport = new PulseOffice.Sheets.UI.GridParts.GridViewport(parts.Engine,
            PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact), 0, () => new Avalonia.Size(800, 600), new PulseOffice.Sheets.UI.Outline.OutlineGutter());
        var painter = new PulseOffice.Sheets.UI.GridParts.Painting.GridOverlayPainter(
            viewport, new SheetSelection(), new PulseOffice.Sheets.UI.GridParts.GridDisplayOptions(), null!);

        var tint = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(painter.CrossTint(Avalonia.Media.Brushes.White));

        Assert.Equal(Avalonia.Media.Colors.White, tint.Color);
        Assert.InRange(tint.Opacity, 0.01, 0.2);
    }
}
