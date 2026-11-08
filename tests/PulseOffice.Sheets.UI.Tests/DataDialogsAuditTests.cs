using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.DataDialogs;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class DataDialogsAuditTests
{
    [Fact]
    public void LeCelleDiInputDelleOperazioniMultipleSonoDelFoglioAttivo()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Secondo");
        var engine = new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        var previewer = new EngineMultipleOperationsPreviewer(new MultipleOperations(),
            new UI.Dialogs.SheetReferenceReader(new SheetRangeReferences()),
            new SketchCells(new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English)),
            engine, 1, new CellRange(new CellAddress(1, 2), new CellAddress(3, 3)));

        var request = previewer.Request(new MultipleOperationsChoice("B1", "A1", "A2"));

        Assert.Equal(1, request!.RowInput!.Value.SheetIndex);
        Assert.Equal(1, request.ColumnInput!.Value.SheetIndex);
    }
}
