using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerPanelSourcesTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    [Fact]
    public void Il_navigatore_ignora_un_identificativo_con_foglio_negativo()
    {
        var (context, selected) = Context();
        var navigator = new LedgerNavigatorSource(context, null!, null!);

        navigator.Go("objects", "-1:oggetto");
        navigator.Go("comments", "-1:A1");

        Assert.Empty(selected);
    }

    [Fact]
    public void La_revisione_ignora_un_commento_con_foglio_negativo()
    {
        var (context, _) = Context();
        var history = context.History!;
        var review = new LedgerReviewSource(context, null!, new PulseOffice.Foundation.Review.AuthorPalette(), null!);

        review.Edit("-1:A1", "testo");
        review.Delete(["-1:A1"]);
        review.Go("-1:A1");

        Assert.False(history.CanUndo);
    }

    private static (ActiveSheetContext Context, List<int> Selected) Context()
    {
        var workbook = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        var selected = new List<int>();
        var zoom = 1.0;
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = new UndoStack(),
            Engine = Engines.Create(workbook),
            Selection = () => CellRange.Single(CellAddress.First),
            WriteSelection = _ => { },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => zoom,
            WriteZoom = value => zoom = value,
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = selected.Add,
            WriteSelectedObject = _ => { },
        });
        return (context, selected);
    }
}
