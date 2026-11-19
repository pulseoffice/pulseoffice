using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round256LambdaHelperTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Evaluate(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        Assert.True(CellAddress.TryParseA1("A1", out var address));
        var cell = new SheetCell(0, address);
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell);
    }

    [Fact]
    public void Map_pads_a_shorter_array_with_not_available() =>
        Assert.Equal(CellError.NotAvailable, Evaluate("=INDEX(MAP({1|2|3};{1|2};LAMBDA(a;b;a+b));3;1)").Error);

    [Fact]
    public void Map_broadcasts_a_single_value_over_the_other_array() =>
        Assert.Equal(13, Evaluate("=INDEX(MAP({1|2|3};10;LAMBDA(a;b;a+b));3;1)").AsNumber());

    [Fact]
    public void Makearray_propagates_an_error_in_its_size() =>
        Assert.Equal(CellError.DivideByZero, Evaluate("=MAKEARRAY(1/0;2;LAMBDA(r;c;1))").Error);
}
