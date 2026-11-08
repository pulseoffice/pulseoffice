using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class PivotPreviewHeaderTests
{
    [Fact]
    public void Senza_campi_di_colonna_l_anteprima_intitola_i_valori_col_nome_del_campo_come_la_tabella_creata()
    {
        var sheet = new Worksheet("Dati");
        string[] headers = ["Categoria", "Gen", "Mar"];
        object[][] rows = [["Affitto", 800, 800], ["Cibo", 300, 320]];
        for (var c = 0; c < headers.Length; c++) sheet.SetValue(new CellAddress(0, c), CellValue.Text(headers[c]));
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < headers.Length; c++)
                sheet.SetValue(new CellAddress(r + 1, c), rows[r][c] is string text ? CellValue.Text(text) : CellValue.Number(Convert.ToDouble(rows[r][c], System.Globalization.CultureInfo.InvariantCulture)));
        var source = new RangePivotSource(sheet, new CellRange(new(0, 0), new(2, 2)));
        var engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));
        var previewer = new SourcePivotPreviewer(engine, source, new PivotLabels("Totale generale", "Totale {0}"));
        PivotField[] fields =
        [
            new() { SourceName = "Categoria", SourceColumnIndex = 0, Area = PivotArea.Row },
            new() { SourceName = "Gen", SourceColumnIndex = 1, Area = PivotArea.Value, AreaOrder = 0 },
            new() { SourceName = "Mar", SourceColumnIndex = 2, Area = PivotArea.Value, AreaOrder = 1 },
        ];

        var preview = previewer.Preview(fields, 7, 5);

        Assert.Equal(["", "Gen", "Mar"], preview[0]);
    }
}
