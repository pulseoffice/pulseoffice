using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class PivotEngineTests
{
    private sealed class Table(string[] headers, object[][] rows) : IPivotSource
    {
        public IReadOnlyList<string> Headers => headers;

        public int RowCount => rows.Length;

        public CellValue ValueAt(int row, int column) => rows[row][column] switch
        {
            null => CellValue.Empty,
            double number => CellValue.Number(number),
            int number => CellValue.Number(number),
            bool flag => CellValue.Boolean(flag),
            var text => CellValue.Text(text.ToString()!),
        };
    }

    private static readonly IPivotEngine Engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));

    private static Table Sales() => new(
        ["Regione", "Prodotto", "Importo"],
        [
            ["Nord", "Vite", 100],
            ["Nord", "Vite", 50],
            ["Nord", "Dado", 30],
            ["Sud", "Vite", 20],
            ["Sud", "Dado", 70],
        ]);

    private static PivotField Field(string name, int index, PivotArea area, int order = 0) => new()
    {
        SourceName = name,
        SourceColumnIndex = index,
        Area = area,
        AreaOrder = order,
    };

    private static PivotDefinition Definition(params PivotField[] fields) => new()
    {
        Id = "p1",
        Name = "Prova",
        SourceSheet = "Dati",
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(5, 2)),
        DestinationSheet = "Pivot",
        Destination = CellAddress.First,
        Fields = fields,
    };

    private static double Cell(PivotTable table, int row, int column) => table.Value(row, column).AsNumber();

    [Fact]
    public void SenzaCampiValoreNonC_EAlcunaTabella()
    {
        var table = Engine.Build(Definition(Field("Regione", 0, PivotArea.Row)), Sales());

        Assert.True(table.IsEmpty);
    }

    [Fact]
    public void UnCampoRigaEUnValoreDannoUnaSommaPerVoce()
    {
        var table = Engine.Build(
            Definition(Field("Regione", 0, PivotArea.Row), Field("Importo", 2, PivotArea.Value)),
            Sales());

        Assert.Equal(["Nord", "Sud", ""], table.Rows.Select(row => row.Label));
        Assert.Equal(180, Cell(table, 0, 0));
        Assert.Equal(90, Cell(table, 1, 0));
        Assert.Equal(270, Cell(table, 2, 0));
    }

    [Fact]
    public void IlTotaleGeneraleSiPuoTogliere()
    {
        var definition = Definition(
            Field("Regione", 0, PivotArea.Row), Field("Importo", 2, PivotArea.Value)) with
        {
            ShowGrandTotalRows = false,
        };

        var table = Engine.Build(definition, Sales());

        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void RigheEColonneIncrocianoIDati()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Prodotto", 1, PivotArea.Column),
                Field("Importo", 2, PivotArea.Value)),
            Sales());

        Assert.Equal(["Dado", "Vite", ""], table.Columns.Select(column => column.Node.Label));

        Assert.Equal(30, Cell(table, 0, 0));
        Assert.Equal(150, Cell(table, 0, 1));
        Assert.Equal(180, Cell(table, 0, 2));
    }

    [Fact]
    public void LaMediaNonSiCalcolaSommandoILSottototali()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Importo", 2, PivotArea.Value) with { Aggregation = PivotAggregation.Average }),
            Sales());

        Assert.Equal(60, Cell(table, 0, 0), 3);
        Assert.Equal(45, Cell(table, 1, 0), 3);
        Assert.Equal(54, Cell(table, 2, 0), 3);
    }

    [Fact]
    public void DueLivelliDiRigheProduconoISottototali()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row, order: 0),
                Field("Prodotto", 1, PivotArea.Row, order: 1),
                Field("Importo", 2, PivotArea.Value)),
            Sales());

        var kinds = table.Rows.Select(row => row.Kind).ToList();

        Assert.Contains(PivotNodeKind.Subtotal, kinds);

        var subtotal = table.Rows
            .Select((row, index) => (row, index))
            .First(entry => entry.row.Kind == PivotNodeKind.Subtotal && entry.row.Path[0] == "Nord");

        Assert.Equal(180, Cell(table, subtotal.index, 0));
    }

    [Fact]
    public void UnValoreEsclusoNonEntraNelConto()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row) with { ExcludedValues = ["Sud"] },
                Field("Importo", 2, PivotArea.Value)),
            Sales());

        Assert.Equal(["Nord", ""], table.Rows.Select(row => row.Label));
        Assert.Equal(180, Cell(table, 1, 0));
    }

    [Fact]
    public void IlConteggioDistintoContaLeVociNonLeRighe()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Prodotto", 1, PivotArea.Value) with { Aggregation = PivotAggregation.DistinctCount }),
            Sales());

        Assert.Equal(2, Cell(table, 0, 0));
    }

    [Fact]
    public void LaPercentualeSulTotaleSiCalcolaSulleVociNonSuiTotali()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.PercentOfTotal }),
            Sales());

        Assert.Equal(180.0 / 270, Cell(table, 0, 0), 4);
        Assert.Equal(90.0 / 270, Cell(table, 1, 0), 4);
        Assert.Equal(1, Cell(table, 2, 0), 4);
    }

    [Fact]
    public void IlTotaleProgressivoSomma()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.RunningTotal }),
            Sales());

        Assert.Equal(180, Cell(table, 0, 0));
        Assert.Equal(270, Cell(table, 1, 0));
    }

    [Fact]
    public void LOrdineDiscendenteRibaltaLeVoci()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row) with { SortDescending = true },
                Field("Importo", 2, PivotArea.Value)),
            Sales());

        Assert.Equal("Sud", table.Rows[0].Label);
    }

    [Fact]
    public void SenzaCampiRigaRestaSoloIlTotale()
    {
        var table = Engine.Build(Definition(Field("Importo", 2, PivotArea.Value)), Sales());

        Assert.Single(table.Rows);
        Assert.Equal(270, Cell(table, 0, 0));
    }

    [Fact]
    public void DueCampiValoreDannoDueColonnePerVoce()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Importo", 2, PivotArea.Value, order: 0),
                Field("Importo", 2, PivotArea.Value, order: 1) with { Aggregation = PivotAggregation.Count }),
            Sales());

        Assert.Equal(2, table.Columns.Count);
        Assert.Equal(180, Cell(table, 0, 0));
        Assert.Equal(3, Cell(table, 0, 1));
    }

    [Fact]
    public void ConDueCampiRigaLaPercentualeSulTotaleContaSoloLeVoci()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Prodotto", 1, PivotArea.Row, 1),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.PercentOfTotal }),
            Sales());

        var leaves = Enumerable.Range(0, table.Rows.Count)
            .Where(r => table.Rows[r].Kind == PivotNodeKind.Item && table.Rows[r].Path.Count == 2)
            .Sum(r => Cell(table, r, 0));

        Assert.Equal(1, leaves, 4);
    }

    [Fact]
    public void LaPercentualeSullaRigaSenzaCampiColonnaNonEUnErrore()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.PercentOfRow }),
            Sales());

        Assert.Equal(1, Cell(table, 0, 0), 4);
    }

    [Fact]
    public void LaPercentualeSullaRigaSiCalcolaSuiValoriOriginali()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Prodotto", 1, PivotArea.Column),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.PercentOfRow }),
            Sales());

        Assert.Equal(30.0 / 180, Cell(table, 0, 0), 6);
        Assert.Equal(150.0 / 180, Cell(table, 0, 1), 6);
    }

    [Fact]
    public void LaPercentualeSulTotaleUsaIlTotaleGeneraleDiTutteLeColonne()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Prodotto", 1, PivotArea.Column),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.PercentOfTotal }),
            Sales());

        Assert.Equal(30.0 / 270, Cell(table, 0, 0), 6);
    }

    [Fact]
    public void LaPercentualeSullaColonnaUsaIlTotaleDellaColonna()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Prodotto", 1, PivotArea.Column),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.PercentOfColumn }),
            Sales());

        Assert.Equal(30.0 / 100, Cell(table, 0, 0), 6);
    }

    [Fact]
    public void IlRangoLasciaVuotiITotali()
    {
        var table = Engine.Build(
            Definition(
                Field("Regione", 0, PivotArea.Row),
                Field("Importo", 2, PivotArea.Value) with { DisplayMode = ValueDisplayMode.RankDescending }),
            Sales());

        Assert.Equal(1, Cell(table, 0, 0));
        Assert.True(table.Value(table.Rows.Count - 1, 0).IsEmpty);
    }

    [Fact]
    public void GliIntervalliNumericiSiOrdinanoPerInizio()
    {
        var source = new Table(["Valore", "Importo"], [[5, 1], [105, 1], [15, 1]]);
        var table = Engine.Build(
            Definition(
                Field("Valore", 0, PivotArea.Row) with { NumericGroupInterval = 10 },
                Field("Importo", 1, PivotArea.Value)),
            source);

        Assert.Equal(["0-10", "10-20", "100-110"], table.Rows.Take(3).Select(row => row.Label));
    }

    [Fact]
    public void UnValoreSulBordoCadeNellIntervalloGiusto()
    {
        var grouping = new PivotGrouping();
        var field = Field("Valore", 0, PivotArea.Row) with { NumericGroupInterval = 0.1 };

        Assert.Equal("0.3-0.4", grouping.KeyOf(CellValue.Number(0.3), field));
        Assert.Equal("0.0001-0.0002", grouping.KeyOf(CellValue.Number(0.00015),
            field with { NumericGroupInterval = 0.0001 }));
    }
}
