using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Charts;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class DeepAuditRound28ChartTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider().GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private sealed class CountingEditor : IChartEditor
    {
        public int Opened { get; private set; }

        public Task<ChartDefinition?> EditAsync(ChartDefinition chart, ChartEditSource source, CancellationToken ct = default)
        {
            Opened++;
            return Task.FromResult<ChartDefinition?>(chart with { ChartTitle = "changed" });
        }
    }

    [Fact]
    public async Task IlDialogoDelGraficoNonSiApreSuUnFoglioProtetto()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var placed = new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(0, 3),
            Content = new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = sheet.Name, SourceRange = "A1:B3" },
        };
        sheet.Objects.Add(placed);
        sheet.Protection = new SheetProtection { IsEnabled = true };
        var history = new UndoStack();
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = Engines.Create(workbook),
            Selection = () => new CellRange(new CellAddress(0, 3), new CellAddress(0, 3)),
            WriteSelection = _ => { },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => 1,
            WriteZoom = _ => { },
            ReadSelectedObject = () => placed,
            ReadSelectedObjects = () => [placed],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        var editor = new CountingEditor();
        var commands = new LedgerChartCommands(Fake<ISheetFormattingService>(), context, Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            Fake<ISheetImageInsertion>(), Fake<IChartPicker>(), editor,
            Fake<Charts.IChartSettingsMapper>(), Fake<Engine.Operations.IChartDataExtractor>(), Fake<Dialogs.ISheetReferenceSources>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.chart.edit").Execute(null!, CancellationToken.None);

        Assert.Equal(0, editor.Opened);
        Assert.Same(placed, sheet.Objects[0]);
        Assert.False(history.CanUndo);
    }
}
