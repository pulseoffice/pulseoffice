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
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round284FillOptionsTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

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
            var grid = Grid = new SheetGridView(engine, presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), selection);
            var writer = new LedgerCellWriter(Workbook, engine, History, status, texts, new PulseOffice.Shell.DocumentWorkspace(), grid,
                null, null, null, null);
            Operations = new LedgerCellOperations(Workbook, engine, grid, selection, History, status, texts, writer, new PulseOffice.Sheets.UI.Outline.OutlineGutter());
            Options = new LedgerFillOptions(grid, Operations, texts, new PulseOffice.UI.Icons.CompositeIconCatalog([new PulseOffice.UI.Icons.IconCatalog(), new PulseOffice.UI.Icons.CellIcons()]));
        }

        public SheetGridView Grid { get; }

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public UndoStack History { get; } = new();

        public LedgerCellOperations Operations { get; }

        public LedgerFillOptions Options { get; }

        public Worksheet Sheet => Workbook.Sheets[0];

        public double[] Column(int rows) => [.. Enumerable.Range(0, rows).Select(row => Sheet.GetValue(new CellAddress(row, 0)).AsNumber())];
    }

    [Fact]
    public Task The_fill_options_button_follows_zoom_and_shows_an_icon() => Session.Dispatch(() =>
    {
        var parts = new Parts();
        parts.Sheet.SetValue(CellAddress.First, CellValue.Number(1));

        parts.Operations.ApplyFill(new SheetGridView.FillRequest(
            new CellRange(CellAddress.First, CellAddress.First), new CellRange(CellAddress.First, new CellAddress(3, 0))));
        var before = parts.Options.Placement;
        parts.Grid.Zoom = 2;

        Assert.NotNull(before);
        Assert.NotEqual(before, parts.Options.Placement);
        Assert.Equal(parts.Grid.CellBounds(new CellAddress(3, 0)), parts.Options.Placement);
        Assert.IsType<PulseOffice.UI.Controls.IconView>(parts.Options.Button.Content);
        parts.Options.Hide();
        Assert.Null(parts.Options.Placement);
    }, TestContext.Current.CancellationToken);
}
