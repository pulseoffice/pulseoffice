using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class DeepAuditRoundTwentyTwoExchangeTests
{
    private static CellRangeContent Grid(int rows, int columns)
    {
        var row = Enumerable.Repeat(new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "x" }, columns).ToArray();
        return new CellRangeContent
        {
            SheetName = "Foglio",
            RowCount = rows,
            ColumnCount = columns,
            Rows = [.. Enumerable.Range(0, rows).Select(_ => (IReadOnlyList<ExchangeCell>)row)],
        };
    }

    [Theory]
    [InlineData(1, 2_000_000)]
    [InlineData(4_000_000, 1)]
    public void UnaTabellaDaUnIntervalloEnormeHaUnaDimensioneNaturaleValidaEntroIlTetto(int rows, int columns)
    {
        var table = new CellGridConversions().Table(Grid(rows, columns), "t");

        Assert.InRange(table.NaturalSize.Width.Value, 1, ExchangeContentGuard.MaximumExtent);
        Assert.InRange(table.NaturalSize.Height.Value, 1, ExchangeContentGuard.MaximumExtent);
    }
}
