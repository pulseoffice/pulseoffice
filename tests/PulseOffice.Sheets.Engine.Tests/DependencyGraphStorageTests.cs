using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class DependencyGraphStorageTests
{
    private static SheetCell Cell(int row, int column = 0) => new(0, new CellAddress(row, column));

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(40)]
    public void I_dipendenti_restano_esatti_sopra_e_sotto_la_soglia_compatta(int count)
    {
        var graph = new DependencyGraph();
        var source = Cell(0);
        var formulas = Enumerable.Range(1, count).Select(row => Cell(row, 1)).ToList();
        foreach (var formula in formulas) graph.SetPrecedents(formula, [source, source, Cell(0, 2)], []);

        Assert.Equal(formulas.ToHashSet(), graph.DirectDependents(source));

        foreach (var formula in formulas.Where((_, index) => index % 2 == 0)) graph.ClearPrecedents(formula);
        Assert.Equal(formulas.Where((_, index) => index % 2 == 1).ToHashSet(), graph.DirectDependents(source));
        Assert.Empty(graph.DirectPrecedents(formulas[0]));
        if (count > 1) Assert.Equal(new HashSet<SheetCell> { source, Cell(0, 2) }, graph.DirectPrecedents(formulas[1]));
    }
}
