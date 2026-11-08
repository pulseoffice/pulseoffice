using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class ColorAutoFilterEngineTests
{
    [Fact]
    public void IlFiltroPerColoreNascondeLeRigheDiAltroColore()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Colori");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Sfondo"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));
        sheet.SetStyle(new CellAddress(1, 0), workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#E8F2A1" }));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Number(2));
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(2, 0));
        var filter = new ColumnFilter(0) { Color = new ColorFilterCriterion(ColorFilterTarget.Background, "#e8f2a1") };

        var hidden = new SheetFilterService().RowsToHide(sheet, range, [filter], true, workbook.Styles);

        Assert.Equal([2], hidden);
    }
}
