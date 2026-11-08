using PulseOffice.Document.Embedding;
using PulseOffice.Exchange;
using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class CellGridConversionsTests
{
    private readonly CellGridConversions _conversions = new();

    [Fact]
    public void Le_colonne_di_soli_numeri_diventano_numeriche_e_si_allineano_a_destra()
    {
        var grid = new CellRangeContent
        {
            SheetName = "Bilancio",
            RowCount = 3,
            ColumnCount = 2,
            Rows =
            [
                [Text("Voce"), Text("2025")],
                [Text("Ricavi"), Number(120000)],
                [Text("Costi"), ExchangeCell.Empty],
            ],
        };

        var table = _conversions.Table(grid, "t1");

        Assert.Equal([DataValueKind.Text, DataValueKind.Number], table.Snapshot.Columns.Select(column => column.Kind));
    }

    [Fact]
    public void A_numeric_column_stores_the_number_not_the_displayed_text()
    {
        var grid = new CellRangeContent
        {
            SheetName = "Sheet",
            RowCount = 2,
            ColumnCount = 2,
            Rows =
            [
                [Text("Item"), Text("Amount")],
                [Text("Rent"), new ExchangeCell { Kind = ExchangeCellKind.Number, Number = 1234.5, Text = "1.234,50 €" }],
            ],
        };

        var table = _conversions.Table(grid, "t1");

        Assert.Equal(1234.5, table.Snapshot.Rows[0][1]);
        Assert.Equal("Rent", table.Snapshot.Rows[0][0]);
    }

    private static ExchangeCell Text(string text) => new() { Kind = ExchangeCellKind.Text, Text = text };

    private static ExchangeCell Number(double value) => new() { Kind = ExchangeCellKind.Number, Number = value, Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
}
