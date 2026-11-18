using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round210TableReferenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private (Workbook Workbook, CalculationEngine Engine) Sales()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("A1"), CellValue.Text("Name"));
        engine.SetValue(C("B1"), CellValue.Text("Amount"));
        engine.SetValue(C("A2"), CellValue.Text("n"));
        engine.SetValue(C("B2"), CellValue.Number(10));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(C("A1").Address, C("B2").Address) });
        return (workbook, engine);
    }

    [Fact]
    public void This_row_outside_the_data_rows_gives_value_error()
    {
        var (_, engine) = Sales();
        engine.SetFormula(C("D5"), "=Sales[@Amount]");

        Assert.Equal(CellError.Value, engine.GetValue(C("D5")).Error);
    }

    [Fact]
    public void A_header_rename_is_followed_in_manual_calculation_mode()
    {
        var (workbook, engine) = Sales();
        engine.SetFormula(C("D1"), "=SUM(Sales[Amount])");
        engine.AutomaticCalculation = false;

        engine.SetValue(C("B1"), CellValue.Text("Price"));

        Assert.Equal("=SUM(Sales[Price])", workbook.Sheets[0][C("D1").Address].Formula);
    }

    [Fact]
    public void Renames_reach_conditional_formats_validations_and_defined_names()
    {
        var (workbook, engine) = Sales();
        var sheet = workbook.Sheets[0];
        workbook.DefineName(new DefinedName("Amounts", "Sales[Amount]"));
        sheet.ConditionalRules.Add(new Formatting.ConditionalRule { Range = new CellRange(C("C1").Address, C("C2").Address), Formula = "=C1>MAX(Sales[Amount])" });
        sheet.Validations.Add(new DataValidation { Range = CellRange.Single(C("D1").Address), Kind = ValidationKind.List, FirstFormula = "Sales[Amount]" });

        engine.SetValue(C("B1"), CellValue.Text("Price"));
        Assert.True(new Operations.TableRenaming().Rename(engine, "Sales", "Orders"));

        Assert.Equal("Orders[Price]", workbook.DefinedNames.Single().Expression);
        Assert.Equal("=C1>MAX(Orders[Price])", Assert.Single(sheet.ConditionalRules).Formula);
        Assert.Equal("Orders[Price]", Assert.Single(sheet.Validations).FirstFormula);
    }
}
