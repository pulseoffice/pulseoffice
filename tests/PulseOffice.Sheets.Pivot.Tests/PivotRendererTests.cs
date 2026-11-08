using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class PivotRendererTests
{
    private sealed class Table(string[] headers, object[][] rows) : IPivotSource
    {
        public IReadOnlyList<string> Headers => headers;

        public int RowCount => rows.Length;

        public CellValue ValueAt(int row, int column) => rows[row][column] switch
        {
            double number => CellValue.Number(number),
            int number => CellValue.Number(number),
            var text => CellValue.Text(text.ToString()!),
        };
    }

    private static readonly IPivotEngine Engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));
    private static readonly IPivotRenderer Renderer = new PivotRenderer();
    private static readonly PivotLabels Labels = new("Totale generale", "Totale {0}");

    private static Table Sales() => new(
        ["Regione", "Prodotto", "Importo"],
        [
            ["Nord", "Vite", 100],
            ["Nord", "Dado", 30],
            ["Sud", "Vite", 20],
        ]);

    private static PivotDefinition Definition(CellAddress destination, params PivotField[] fields) => new()
    {
        Id = "p1",
        Name = "Prova",
        SourceSheet = "Dati",
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 2)),
        DestinationSheet = "Pivot",
        Destination = destination,
        Fields = fields,
    };

    private static PivotField Field(string name, int index, PivotArea area, int order = 0) => new()
    {
        SourceName = name,
        SourceColumnIndex = index,
        Area = area,
        AreaOrder = order,
    };

    private static (Worksheet Sheet, CellRange Written) Render(
        CellAddress destination, params PivotField[] fields)
    {
        var definition = Definition(destination, fields);
        var table = Engine.Build(definition, Sales());

        var sheet = new Worksheet("Pivot");
        var written = Renderer.Render(table, definition, sheet, new StyleCatalog(), Labels);

        return (sheet, written);
    }

    [Fact]
    public void LeIntestazioniDeiCampiRigaStannoInCima()
    {
        var (sheet, _) = Render(
            CellAddress.First, Field("Regione", 0, PivotArea.Row), Field("Importo", 2, PivotArea.Value));

        Assert.Equal("Regione", sheet.GetValue(CellAddress.First).AsText());
        Assert.Equal("Importo", sheet.GetValue(new CellAddress(0, 1)).AsText());
    }

    [Fact]
    public void LeVociEIValoriFinisconoSottoLeIntestazioni()
    {
        var (sheet, _) = Render(
            CellAddress.First, Field("Regione", 0, PivotArea.Row), Field("Importo", 2, PivotArea.Value));

        Assert.Equal("Nord", sheet.GetValue(new CellAddress(1, 0)).AsText());
        Assert.Equal(130, sheet.GetValue(new CellAddress(1, 1)).AsNumber());
        Assert.Equal("Totale generale", sheet.GetValue(new CellAddress(3, 0)).AsText());
    }

    [Fact]
    public void LaTabellaSiScriveDoveDiceLaDestinazione()
    {
        var (sheet, written) = Render(
            new CellAddress(5, 3), Field("Regione", 0, PivotArea.Row), Field("Importo", 2, PivotArea.Value));

        Assert.Equal("Regione", sheet.GetValue(new CellAddress(5, 3)).AsText());
        Assert.Equal(new CellAddress(5, 3), written.Start);
        Assert.Equal(new CellAddress(8, 4), written.End);
    }

    [Fact]
    public void ISottototaliSiChiamanoConIlNomeDellaVoce()
    {
        var (sheet, _) = Render(
            CellAddress.First,
            Field("Regione", 0, PivotArea.Row, order: 0),
            Field("Prodotto", 1, PivotArea.Row, order: 1),
            Field("Importo", 2, PivotArea.Value));

        var labels = Enumerable.Range(0, 12)
            .Select(row => sheet.GetValue(new CellAddress(row, 0)).AsText())
            .ToList();

        Assert.Contains("Totale Nord", labels);
    }

    [Fact]
    public void LeVociDiSecondoLivelloSonoRientrate()
    {
        var definition = Definition(
            CellAddress.First,
            Field("Regione", 0, PivotArea.Row, order: 0),
            Field("Prodotto", 1, PivotArea.Row, order: 1),
            Field("Importo", 2, PivotArea.Value));

        var sheet = new Worksheet("Pivot");
        var styles = new StyleCatalog();

        Renderer.Render(Engine.Build(definition, Sales()), definition, sheet, styles, Labels);

        var indents = Enumerable.Range(0, 10)
            .Select(row => styles.Get(sheet[new CellAddress(row, 0)].StyleId).Indent)
            .ToList();

        Assert.Contains(1, indents);
    }

    [Fact]
    public void LeColonneRiportanoLeVociDelCampoColonna()
    {
        var (sheet, _) = Render(
            CellAddress.First,
            Field("Regione", 0, PivotArea.Row),
            Field("Prodotto", 1, PivotArea.Column),
            Field("Importo", 2, PivotArea.Value));

        Assert.Equal("Dado", sheet.GetValue(new CellAddress(0, 1)).AsText());
        Assert.Equal("Vite", sheet.GetValue(new CellAddress(0, 2)).AsText());
        Assert.Equal("Totale generale", sheet.GetValue(new CellAddress(0, 3)).AsText());
    }
}
