using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class PivotLookupAndBlanksTests
{
    private static readonly IPivotEngine Engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));

    private static PivotField Field(string name, int index, PivotArea area, int order = 0,
        PivotAggregation aggregation = PivotAggregation.Sum) => new()
    {
        SourceName = name, SourceColumnIndex = index, Area = area, AreaOrder = order, Aggregation = aggregation,
    };

    private static Workbook Data(params object?[][] rows)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Name = "Dati";
        string[] headers = ["Regione", "Prodotto", "Importo"];
        for (var c = 0; c < headers.Length; c++)
            workbook.Sheets[0].SetValue(new CellAddress(0, c), CellValue.Text(headers[c]));

        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                workbook.Sheets[0].SetValue(new CellAddress(r + 1, c), rows[r][c] switch
                {
                    null => CellValue.Empty,
                    int number => CellValue.Number(number),
                    var text => CellValue.Text(text.ToString()!),
                });

        workbook.AddSheet("Pivot");
        return workbook;
    }

    private static PivotDefinition Definition(int lastRow, params PivotField[] fields) => new()
    {
        Id = "p1", Name = "P", SourceSheet = "Dati",
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(lastRow, 2)),
        DestinationSheet = "Pivot", Destination = CellAddress.First, Fields = fields,
    };

    [Fact]
    public void LaMediaIgnoraCelleVuoteETesto()
    {
        var workbook = Data(["Nord", "Vite", 10], ["Nord", "Dado", null], ["Nord", "Bullone", "5"]);
        var definition = Definition(3, Field("Regione", 0, PivotArea.Row),
            Field("Importo", 2, PivotArea.Value, aggregation: PivotAggregation.Average));

        var table = Engine.Build(definition, new RangePivotSource(workbook.Sheets[0], definition.SourceRange));

        Assert.Equal(10, table.Value(0, 0).AsNumber());
    }

    [Fact]
    public void GetPivotDataUsaINomiDeiCampiAncheInColonna()
    {
        var workbook = Data(["Nord", "Vite", 100], ["Nord", "Dado", 30], ["Sud", "Vite", 20], ["Sud", "Dado", 70]);
        workbook.Pivots.Add(Definition(4, Field("Regione", 0, PivotArea.Row), Field("Prodotto", 1, PivotArea.Column),
            Field("Importo", 2, PivotArea.Value)));
        var lookup = new PivotDataLookup(workbook, Engine, new PivotLabels("Totale", "{0}"));
        var target = new CellAddress(2, 2);

        Assert.Equal(70, lookup.Lookup(1, target, "Importo", [("Prodotto", "Dado"), ("Regione", "Sud")])?.AsNumber());
        Assert.Equal(120, lookup.Lookup(1, target, "Importo", [("Prodotto", "Vite")])?.AsNumber());
        Assert.Equal(130, lookup.Lookup(1, target, "Importo", [("Regione", "Nord")])?.AsNumber());
        Assert.Equal(220, lookup.Lookup(1, target, "Importo", [])?.AsNumber());
        Assert.Null(lookup.Lookup(1, target, "Importo", [("Mese", "Nord")]));
    }

    [Fact]
    public void GetPivotDataTrovaIlCampoInternoSenzaQuelloEsternoEIlNomeDellaMisura()
    {
        var workbook = Data(["Nord", "Vite", 100], ["Nord", "Dado", 30], ["Sud", "Vite", 20], ["Sud", "Dado", 70]);
        workbook.Pivots.Add(Definition(4, Field("Regione", 0, PivotArea.Row), Field("Prodotto", 1, PivotArea.Row, order: 1),
            Field("Importo", 2, PivotArea.Value)));
        var lookup = new PivotDataLookup(workbook, Engine, new PivotLabels("Totale", "{0}"));
        var target = new CellAddress(2, 2);

        Assert.Equal(120, lookup.Lookup(1, target, "Importo", [("prodotto", "vite")])?.AsNumber());
        Assert.Equal(70, lookup.Lookup(1, target, "Sum - Importo", [("Prodotto", "Dado"), ("Regione", "Sud")])?.AsNumber());
        Assert.Null(lookup.Lookup(1, target, "Importo", [("Prodotto", "Chiodo")]));
    }

    [Fact]
    public void GetPivotDataSuUnSubtotaleNascostoDaRiferimentoSalvoCheLaRigaSiaUnica()
    {
        var workbook = Data(["Nord", "Vite", 100], ["Nord", "Dado", 30], ["Sud", "Vite", 20]);
        workbook.Pivots.Add(Definition(3, Field("Regione", 0, PivotArea.Row) with { ShowSubtotals = false },
            Field("Prodotto", 1, PivotArea.Row, order: 1), Field("Importo", 2, PivotArea.Value)));
        var lookup = new PivotDataLookup(workbook, Engine, new PivotLabels("Totale", "{0}"));
        var target = new CellAddress(2, 2);

        Assert.Null(lookup.Lookup(1, target, "Importo", [("Regione", "Nord")]));
        Assert.Equal(20, lookup.Lookup(1, target, "Importo", [("Regione", "Sud")])?.AsNumber());
        Assert.Equal(30, lookup.Lookup(1, target, "Importo", [("Regione", "Nord"), ("Prodotto", "Dado")])?.AsNumber());
    }
}
