using System.Globalization;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class SharedTableTests
{
    private sealed class Grid(double?[,] values, int row, int column) : ITableFormulaContext
    {
        public int Row => row;

        public int Column => column;

        public int RowCount => values.GetLength(0);

        public int ColumnCount => values.GetLength(1);

        public double? Value(int r, int c) => r >= 0 && c >= 0 && r < RowCount && c < ColumnCount ? values[r, c] : null;
    }

    private readonly TableFormulaParser _parser = new();

    [Fact]
    public void LaSelezioneSiAllargaFinoAComprendereLeCelleUnite()
    {
        var range = new TableSpans().Enclose(new TableCellRange(0, 0, 0, 0), [new CellSpan(0, 0, 2, 1), new CellSpan(1, 0, 1, 3)]);

        Assert.Equal(new TableCellRange(0, 0, 1, 2), range);
    }

    [Fact]
    public void LeLarghezzeSiDistribuisconoEDSiRidimensionanoSenzaCambiareIlTotale()
    {
        var widths = new ColumnWidths();

        Assert.Equal([100, 150, 150], widths.Distribute([100, 100, 200], 1, 2));
        var resized = widths.Resize([100, 100, 200], 0, 160, 20);
        Assert.Equal([160, 40, 200], resized);
        Assert.Equal([20, 180, 200], widths.Resize([100, 100, 200], 0, 0, 20));
        Assert.Equal(400, widths.Fit([100, 100, 200], 0, 2, [300, 300, 300], 20, 400).Sum(), 6);
    }

    [Fact]
    public void LOrdinamentoNumericoMetteINumeriPrimaEDILTestoDopo()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "10" }, new[] { "9" }, new[] { "mela" }, new[] { "" }, new[] { "1,5" } };

        var order = new TableSorter().Order(rows, [new TableSortKey(0, Kind: TableSortKind.Numeric)], CultureInfo.GetCultureInfo("it-IT"));

        Assert.Equal([4, 1, 0, 2, 3], order);
    }

    [Fact]
    public void LOrdinamentoAlfabeticoDecrescenteEStabile()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "b", "1" }, new[] { "a", "2" }, new[] { "b", "3" } };

        Assert.Equal([0, 2, 1], new TableSorter().Order(rows, [new TableSortKey(0, Descending: true)], CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("=SUM(A1:A2)", 3)]
    [InlineData("=sum <A1:A2>", 3)]
    [InlineData("ooow:<A1>+<A2>*2", 5)]
    [InlineData("=AVERAGE(A1,A2,B1)", 2)]
    [InlineData("=mean <A1>|<A2>|<B1>", 2)]
    [InlineData("=SUM(ABOVE)", 3)]
    [InlineData("=MAX(A1:B2)-MIN(A1:B2)", 2)]
    [InlineData("=COUNT(A1:B2)", 3)]
    [InlineData("=PRODUCT(A1:A2)*(1+1)", 4)]
    [InlineData("=-A2/4", -0.5)]
    public void LeFormuleDelleCelleSiValutano(string formula, double expected)
    {
        var grid = new Grid(new double?[,] { { 1, 3 }, { 2, null }, { null, null } }, 2, 0);

        Assert.Equal(expected, _parser.Parse(formula).Evaluate(grid), 6);
    }

    [Fact]
    public void LeFormuleSiScrivonoNeiDueDialetti()
    {
        var grid = new Grid(new double?[3, 2], 2, 0);
        var node = _parser.Parse("=SUM(ABOVE)+A1*(B1-B2)");

        Assert.Equal("SUM(ABOVE)+A1*(B1-B2)", new CanonicalFormulaSyntax().Print(node, grid));
        Assert.Equal("sum <A1:A2>+<A1>*(<B1>-<B2>)", new WriterFormulaSyntax().Print(node, grid));
        Assert.Equal("SUM(A1:A2)+A1*(B1-B2)", new CanonicalFormulaSyntax().Print(_parser.Parse("ooow:" + new WriterFormulaSyntax().Print(node, grid)), grid));
    }

    [Fact]
    public void UnaFormulaSbagliataNonSiAccetta()
    {
        Assert.False(_parser.TryParse("=SUM(A1", out _));
        Assert.False(_parser.TryParse("=FOO(A1)", out _));
    }
}
