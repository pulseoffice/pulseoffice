using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.Sheets.UI.Dialogs;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class MultipleOperationsPreviewerTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    [Fact]
    public void Sul_secondo_foglio_la_cella_di_ingresso_resta_sul_secondo_foglio()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Seconda");
        var engine = Engines.Create(workbook);
        var first = workbook.Sheets[0];
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(99));
        engine.SetValue(new SheetCell(1, new CellAddress(0, 0)), CellValue.Number(1));
        engine.SetFormula(new SheetCell(1, new CellAddress(0, 1)), "=A1*10");
        foreach (var (row, value) in new[] { (3, 2.0), (4, 5.0) })
            engine.SetValue(new SheetCell(1, new CellAddress(row, 0)), CellValue.Number(value));

        var target = new CellRange(new CellAddress(3, 0), new CellAddress(4, 1));
        var operations = new MultipleOperations();
        var previewer = new EngineMultipleOperationsPreviewer(
            operations, new SheetReferenceReader(new SheetRangeReferences()), new SketchCells(new TextCatalog(UiLanguage.Italian)), engine, 1, target);

        var request = previewer.Request(new MultipleOperationsChoice("B1", string.Empty, "A1"));

        Assert.Equal(1, request!.ColumnInput!.Value.SheetIndex);
        var values = operations.Compute(engine, 1, request).Values;
        Assert.Equal(20, values[new CellAddress(3, 1)].AsNumber());
        Assert.Equal(50, values[new CellAddress(4, 1)].AsNumber());
        Assert.Equal(99, first[new CellAddress(0, 0)].Value.AsNumber());
    }
}
