using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round280FillOptionsTests
{
    private sealed class Parts
    {
        public Parts()
        {
            var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
            var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
            var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
            var status = new PulseOffice.UI.Controls.Services.StatusBarService(
                TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
            var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
                System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)));
            var selection = new SheetSelection();
            var grid = new SheetGridView(engine, presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), selection);
            var writer = new LedgerCellWriter(Workbook, engine, History, status, texts, new PulseOffice.Shell.DocumentWorkspace(), grid,
                null, null, null, null);
            Operations = new LedgerCellOperations(Workbook, engine, grid, selection, History, status, texts, writer, new PulseOffice.Sheets.UI.Outline.OutlineGutter());
        }

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public UndoStack History { get; } = new();

        public LedgerCellOperations Operations { get; }

        public Worksheet Sheet => Workbook.Sheets[0];

        public double[] Column(int rows) => [.. Enumerable.Range(0, rows).Select(row => Sheet.GetValue(new CellAddress(row, 0)).AsNumber())];
    }

    private static readonly CellRange Seed = new(CellAddress.First, new CellAddress(1, 0));
    private static readonly CellRange Target = new(CellAddress.First, new CellAddress(3, 0));

    [Fact]
    public void A_fill_option_redoes_the_last_fill_in_place_as_one_step()
    {
        var parts = new Parts();
        parts.Sheet.SetValue(CellAddress.First, CellValue.Number(1));
        parts.Sheet.SetValue(new CellAddress(1, 0), CellValue.Number(2));
        SheetGridView.FillRequest? shown = null;
        parts.Operations.Filled += (_, request) => shown = request;

        parts.Operations.ApplyFill(new SheetGridView.FillRequest(Seed, Target));
        Assert.Equal([1d, 2d, 3d, 4d], parts.Column(4));
        Assert.NotNull(shown);

        Assert.True(parts.Operations.RefillAs(FillMode.Copy));
        Assert.Equal([1d, 2d, 1d, 2d], parts.Column(4));

        parts.History.Undo();
        Assert.True(parts.Sheet.GetValue(new CellAddress(2, 0)).IsEmpty);
        Assert.False(parts.Operations.RefillAs(FillMode.Series));
    }

    [Fact]
    public void Holding_the_toggle_key_fills_a_single_number_as_a_series_and_copies_its_format()
    {
        var parts = new Parts();
        parts.Sheet.SetValue(CellAddress.First, CellValue.Number(5));
        var percent = parts.Workbook.Styles.Intern(CellStyle.Default with { NumberFormatId = 10 });
        parts.Sheet.SetStyle(CellAddress.First, percent);

        parts.Operations.ApplyFill(new SheetGridView.FillRequest(new CellRange(CellAddress.First, CellAddress.First), new CellRange(CellAddress.First, new CellAddress(2, 0))) { Toggled = true });

        Assert.Equal([5d, 6d, 7d], parts.Column(3));
        Assert.Equal(percent, parts.Sheet.StyleIdAt(new CellAddress(2, 0)));
    }
}
