using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class DeepAuditRound27SparklineTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider().GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    [Fact]
    public async Task LeSparklineDiUnFoglioProtettoNonSiCancellano()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Sparklines.Add(new SparklineGroup
        {
            Id = "g",
            Sparklines = [new Sparkline(new CellAddress(0, 3), new CellRange(new CellAddress(0, 0), new CellAddress(0, 2)))],
        });
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
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        var commands = new LedgerSparklineCommands(context, Fake<Engine.Operations.ISheetSnapshots>(),
            Fake<PulseOffice.UI.Controls.Services.IDialogService>(), Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            Fake<Dialogs.ISheetReferenceSources>(), Fake<Dialogs.ISheetReferenceReader>(), null!,
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.sparkline.delete")
            .Execute(null!, CancellationToken.None);

        Assert.Equal(1, sheet.Sparklines.Count);
        Assert.False(history.CanUndo);
    }
}
