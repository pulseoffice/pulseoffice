using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public sealed class PivotCalculatedFieldTests
{
    private sealed class Table : IPivotSource
    {
        private readonly object[][] _rows =
        [
            ["Nord", 100.0, 60.0],
            ["Nord", 50.0, 20.0],
            ["Sud", 80.0, 80.0],
        ];

        public IReadOnlyList<string> Headers { get; } = ["Zona", "Ricavi", "Costi fissi"];

        public int RowCount => _rows.Length;

        public CellValue ValueAt(int row, int column) => _rows[row][column] is double number ? CellValue.Number(number) : CellValue.Text((string)_rows[row][column]);
    }

    private static readonly IPivotEngine Engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));

    private static PivotDefinition Definition(string formula) => new()
    {
        Id = "p",
        Name = "P",
        SourceSheet = "Dati",
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 2)),
        DestinationSheet = "Pivot",
        Destination = CellAddress.First,
        Fields =
        [
            new PivotField { SourceName = "Zona", SourceColumnIndex = 0, Area = PivotArea.Row },
            new PivotField { SourceName = "Margine", SourceColumnIndex = -1, Formula = formula, Area = PivotArea.Value },
        ],
    };

    [Fact]
    public void IlCampoCalcolatoApplicaLaFormulaAiTotali()
    {
        var table = Engine.Build(Definition("=Ricavi-'Costi fissi'"), new Table());

        Assert.Equal(70, table.Value(0, 0).AsNumber());
        Assert.Equal(0, table.Value(1, 0).AsNumber());
        Assert.Equal(70, table.Value(2, 0).AsNumber());
    }

    [Fact]
    public void LaDivisionePerZeroDaErrore()
    {
        var table = Engine.Build(Definition("=Ricavi/('Costi fissi'-80)"), new Table());

        Assert.Equal(ValueKind.Error, table.Value(1, 0).Kind);
    }

    [Fact]
    public void UnaFormulaSenzaCampiNonECompilata()
    {
        var formulas = new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser());

        Assert.Empty(formulas.Compile("=1+2", new Table().Headers).Columns);
        Assert.False(formulas.Compile("=Ricavi+(", new Table().Headers).IsValid);
    }

    [Fact]
    public void UnTraboccoDaNumeroEUnaDivisionePerZeroDaDiv0()
    {
        var formulas = new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser());
        var headers = new[] { "Ricavi" };

        Assert.Equal(CellError.Number, formulas.Evaluate(formulas.Compile("=Ricavi^1000", headers), [10]).Error);
        Assert.Equal(CellError.DivideByZero, formulas.Evaluate(formulas.Compile("=-Ricavi/0", headers), [10]).Error);
    }

    [Fact]
    public void IConfrontiValgonoUnoOZero()
    {
        var formulas = new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser());
        var headers = new[] { "Ricavi", "Costi" };

        Assert.Equal(1, formulas.Evaluate(formulas.Compile("=Ricavi>Costi", headers), [10, 4]).AsNumber());
        Assert.Equal(0, formulas.Evaluate(formulas.Compile("=Ricavi=Costi", headers), [10, 4]).AsNumber());
    }
}
