using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TableFormulaLimitsTests
{
    private sealed class SmallTable : ITableFormulaContext
    {
        public int Visits { get; private set; }

        public int Row => 2;

        public int Column => 0;

        public int RowCount => 3;

        public int ColumnCount => 2;

        public double? Value(int row, int column)
        {
            Visits++;
            return row < 0 || row >= RowCount || column < 0 || column >= ColumnCount ? null : row + 1;
        }
    }

    private readonly TableFormulaParser _parser = new();

    [Fact]
    public void UnIntervalloEnormeLetteDaUnFileSiLimitaAllaTabella()
    {
        var table = new SmallTable();

        var value = _parser.Parse("sum <A1:XFD2000000000>").Evaluate(table);

        Assert.Equal(1 + 1 + 2 + 2 + 3 + 3, value);
        Assert.True(table.Visits <= 6);
    }

    [Fact]
    public void UnaColonnaConTroppeLettereNonEUnRiferimento()
    {
        Assert.False(_parser.TryParse("<ZZZZZZZZZZ1>", out _));
    }

    [Fact]
    public void UnaFormulaAnnidataOltreMisuraSiRifiutaSenzaEsaurireLoStack()
    {
        var formula = new string('(', 200_000) + "1" + new string(')', 200_000);

        Assert.False(_parser.TryParse(formula, out _));
        Assert.False(_parser.TryParse(new string('-', 200_000) + "1", out _));
    }
}
