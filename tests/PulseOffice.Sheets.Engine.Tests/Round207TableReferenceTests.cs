using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round207TableReferenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private (Workbook Workbook, CalculationEngine Engine) Sales(bool totals = true)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("A1"), CellValue.Text("Name"));
        engine.SetValue(C("B1"), CellValue.Text("Amount"));
        for (var row = 2; row <= 4; row++)
        {
            engine.SetValue(C("A" + row), CellValue.Text("n" + row));
            engine.SetValue(C("B" + row), CellValue.Number((row - 1) * 10));
        }
        var last = totals ? "B5" : "B4";
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(C("A1").Address, C(last).Address), HasTotalsRow = totals });
        if (totals) engine.SetFormula(C("B5"), "=SUBTOTAL(109;Sales[Amount])");
        return (workbook, engine);
    }

    [Fact]
    public void A_column_reference_stays_structured_and_sums_the_data_rows()
    {
        var (workbook, engine) = Sales();
        engine.SetFormula(C("D1"), "=SUM(Sales[Amount])");

        Assert.Equal(60, engine.GetValue(C("D1")).AsNumber());
        Assert.Equal(60, engine.GetValue(C("B5")).AsNumber());
        Assert.Equal("=SUM(Sales[Amount])", workbook.Sheets[0][C("D1").Address].Formula);
    }

    [Fact]
    public void This_row_headers_totals_and_all_resolve_to_their_parts_of_the_table()
    {
        var (_, engine) = Sales();
        engine.SetFormula(C("C3"), "=Sales[@Amount]*2");
        engine.SetFormula(C("D1"), "=Sales[@Amount]");
        engine.SetFormula(C("E1"), "=Sales[[#Headers],[Name]]");
        engine.SetFormula(C("F1"), "=Sales[[#Totals],[Amount]]");
        engine.SetFormula(C("G1"), "=ROWS(Sales[#All])");
        engine.SetFormula(C("H1"), "=COLUMNS(Sales[[Name]:[Amount]])");

        Assert.Equal(40, engine.GetValue(C("C3")).AsNumber());
        Assert.Equal(CellError.Value, engine.GetValue(C("D1")).Error);
        Assert.Equal("Name", engine.GetValue(C("E1")).AsText());
        Assert.Equal(60, engine.GetValue(C("F1")).AsNumber());
        Assert.Equal(5, engine.GetValue(C("G1")).AsNumber());
        Assert.Equal(2, engine.GetValue(C("H1")).AsNumber());
    }

    [Fact]
    public void The_canonical_text_uses_the_file_form_of_structured_references()
    {
        var printed = new IdentityRewriter().Rewrite("=Sales[@Amount]+SUM(Sales[[#Headers],[#Data],[Name]:[Amount]])+Sales[]");

        Assert.Equal("=Sales[[#This Row],[Amount]]+SUM(Sales[[#Headers],[#Data],[Name]:[Amount]])+Sales[#Data]", printed);
    }

    [Fact]
    public void Renaming_a_header_renames_the_column_in_the_formulas()
    {
        var (workbook, engine) = Sales();
        engine.SetFormula(C("D1"), "=SUM(Sales[Amount])");

        engine.SetValue(C("B1"), CellValue.Text("Price"));

        Assert.Equal("=SUM(Sales[Price])", workbook.Sheets[0][C("D1").Address].Formula);
        Assert.Equal("=SUBTOTAL(109;Sales[Price])", workbook.Sheets[0][C("B5").Address].Formula);
        Assert.Equal(60, engine.GetValue(C("D1")).AsNumber());
    }

    [Fact]
    public void Renaming_the_table_renames_it_in_the_formulas()
    {
        var (workbook, engine) = Sales();
        engine.SetFormula(C("D1"), "=SUM(Sales[Amount])");

        Assert.True(new TableRenaming().Rename(engine, "Sales", "Orders"));

        Assert.Equal("=SUM(Orders[Amount])", workbook.Sheets[0][C("D1").Address].Formula);
        Assert.NotNull(workbook.DatabaseRanges.Find("Orders"));
        Assert.Equal(60, engine.GetValue(C("D1")).AsNumber());
    }

    [Fact]
    public void Rows_inserted_into_the_table_are_read_by_the_same_formula()
    {
        var (workbook, engine) = Sales(totals: false);
        engine.SetFormula(C("D1"), "=SUM(Sales[Amount])");

        new SheetStructure().InsertRows(engine, 0, 2, 1);
        engine.SetValue(C("B3"), CellValue.Number(5));

        Assert.Equal("=SUM(Sales[Amount])", workbook.Sheets[0][C("D1").Address].Formula);
        Assert.Equal(65, engine.GetValue(C("D1")).AsNumber());
    }

    [Fact]
    public void Redefining_the_table_area_recalculates_its_readers()
    {
        var (workbook, engine) = Sales(totals: false);
        engine.SetFormula(C("D1"), "=SUM(Sales[Amount])");
        engine.SetValue(C("B5"), CellValue.Number(40));

        workbook.DatabaseRanges.Define(workbook.DatabaseRanges.Find("Sales")! with { Range = new CellRange(C("A1").Address, C("B5").Address) });

        Assert.Equal(100, engine.GetValue(C("D1")).AsNumber());
    }

    [Fact]
    public void Flattening_writes_absolute_ranges_for_the_tables_not_kept()
    {
        var (workbook, _) = Sales();

        var flattened = new TableReferenceFlattening().Flatten("=SUM(Sales[Amount])+Sales[@Amount]", workbook, C("C3"), _ => false);

        Assert.Equal("=SUM($B$2:$B$4)+$B$3", flattened);
    }

    private sealed class IdentityRewriter : FormulaRewriter
    {
        public override Parsing.FormulaNode VisitReference(Parsing.ReferenceNode node) => node;

        public override Parsing.FormulaNode VisitRange(Parsing.RangeNode node) => node;
    }
}
