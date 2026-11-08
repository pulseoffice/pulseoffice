using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class StandardFilterTests
{
    private readonly StandardFilter _filter = new(new SheetFilterService());

    private static Worksheet Sheet()
    {
        var sheet = new Worksheet("Foglio1");
        object[][] rows =
        [
            ["Regione", "Importo"],
            ["Nord", 100.0],
            ["Sud", 20.0],
            ["Nord", 5.0],
            ["Est", 50.0],
        ];

        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                sheet.SetValue(new CellAddress(row, column), rows[row][column] switch
                {
                    double number => CellValue.Number(number),
                    var text => CellValue.Text(text.ToString()!),
                });

        return sheet;
    }

    private static CellRange Data => new(new CellAddress(0, 0), new CellAddress(4, 1));

    [Fact]
    public void AndKeepsRowsMatchingBoth()
    {
        var hidden = _filter.RowsToHide(Sheet(), Data,
        [
            new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Equal, "Nord"),
            new StandardFilterRow(FilterJoin.And, 1, FilterCondition.Greater, "10"),
        ]);

        Assert.Equal([2, 3, 4], hidden.Order());
    }

    [Fact]
    public void OrKeepsRowsMatchingEither()
    {
        var hidden = _filter.RowsToHide(Sheet(), Data,
        [
            new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Equal, "Sud"),
            new StandardFilterRow(FilterJoin.Or, 0, FilterCondition.Equal, "Est"),
        ]);

        Assert.Equal([1, 3], hidden.Order());
    }

    [Fact]
    public void ConditionsFoldFromLeftLikeCalc()
    {
        var hidden = _filter.RowsToHide(Sheet(), Data,
        [
            new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Equal, "Sud"),
            new StandardFilterRow(FilterJoin.Or, 0, FilterCondition.Equal, "Nord"),
            new StandardFilterRow(FilterJoin.And, 1, FilterCondition.LessOrEqual, "20"),
        ]);

        Assert.Equal([1, 4], hidden.Order());
    }

    [Theory]
    [InlineData(FilterCondition.NotContains, "or", "<>*or*")]
    [InlineData(FilterCondition.EndsWith, "d", "*d")]
    [InlineData(FilterCondition.GreaterOrEqual, "5", ">=5")]
    public void CriteriaSyntax(FilterCondition condition, string value, string expected) =>
        Assert.Equal(expected, _filter.Criteria(condition, value));

    [Fact]
    public void NoConditionsHidesNothing() => Assert.Empty(_filter.RowsToHide(Sheet(), Data, []));
}
