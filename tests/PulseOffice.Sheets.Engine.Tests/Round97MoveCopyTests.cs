using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round97MoveCopyTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly SheetRangeMove _moves = new(new SheetStructure());

    private readonly SheetDuplicator _duplicator = new();

    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private static CellAddress A(int row, int column) => new(row, column);

    private static double Number(CalculationEngine engine, int sheet, int row, int column) =>
        engine.GetValue(new SheetCell(sheet, A(row, column))).AsNumber();

    [Fact]
    public void Formulas_and_names_pointing_into_a_moved_range_follow_it_and_partial_ranges_stay()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var name = workbook.Sheets[0].Name;
        engine.SetValue(new SheetCell(0, A(0, 0)), CellValue.Number(5));
        engine.SetValue(new SheetCell(0, A(1, 0)), CellValue.Number(7));
        engine.SetFormula(new SheetCell(0, A(0, 2)), "=A1");
        engine.SetFormula(new SheetCell(0, A(5, 1)), "=SUM(A1:A2)");
        engine.SetFormula(new SheetCell(0, A(6, 1)), "=SUM(A1:A3)");
        workbook.DefineName(new DefinedName("Data", $"{name}.$A$1:$A$2"));

        var move = new RangeMove(name, R(0, 0, 1, 0), name, A(0, 4));
        Assert.True(_moves.KeepsBlocksWhole(workbook, move));
        _moves.Move(engine, move);

        Assert.Equal(5, Number(engine, 0, 0, 2));
        Assert.Equal(12, Number(engine, 0, 5, 1));
        Assert.Equal(0, Number(engine, 0, 6, 1));
        Assert.Contains("E1", workbook.Sheets[0][A(0, 2)].Formula, StringComparison.Ordinal);
        Assert.Contains("A1:A3", workbook.Sheets[0][A(6, 1)].Formula, StringComparison.Ordinal);
        Assert.Equal($"{name}.$E$1:$E$2", workbook.FindName("Data")!.Expression);
        Assert.True(workbook.Sheets[0][A(0, 0)].Value.IsEmpty);
    }

    [Fact]
    public void A_move_to_another_sheet_qualifies_references_and_breaks_references_to_overwritten_cells()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var name = workbook.Sheets[0].Name;
        workbook.AddSheet("Two");
        engine.SetValue(new SheetCell(0, A(0, 0)), CellValue.Number(3));
        engine.SetValue(new SheetCell(0, A(8, 1)), CellValue.Number(4));
        engine.SetFormula(new SheetCell(0, A(1, 0)), "=B9*2");
        engine.SetFormula(new SheetCell(0, A(0, 3)), "=A1+A2");
        engine.SetFormula(new SheetCell(1, A(5, 5)), "=A1");

        _moves.Move(engine, new RangeMove(name, R(0, 0, 1, 0), "Two", A(0, 0)));

        Assert.Equal(8, Number(engine, 1, 1, 0));
        Assert.Equal(11, Number(engine, 0, 0, 3));
        Assert.Contains("Two", workbook.Sheets[0][A(0, 3)].Formula, StringComparison.Ordinal);
        Assert.Contains("#REF!", workbook.Sheets[1][A(5, 5)].Formula, StringComparison.Ordinal);
    }

    [Fact]
    public void A_move_that_cuts_a_merge_or_an_array_or_a_filter_is_refused()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Merges.AddRange([R(0, 1, 0, 2)]);
        engine.SetArrayFormula(new SheetCell(0, A(4, 0)), R(4, 0, 5, 0), "=ROW(A1:A2)");
        sheet.AutoFilter.Activate(R(10, 0, 14, 1));

        Assert.False(_moves.KeepsBlocksWhole(workbook, new RangeMove(sheet.Name, R(0, 0, 0, 1), sheet.Name, A(20, 0))));
        Assert.False(_moves.KeepsBlocksWhole(workbook, new RangeMove(sheet.Name, R(20, 0, 20, 0), sheet.Name, A(0, 2))));
        Assert.False(_moves.KeepsBlocksWhole(workbook, new RangeMove(sheet.Name, R(4, 0, 4, 0), sheet.Name, A(20, 0))));
        Assert.False(_moves.KeepsBlocksWhole(workbook, new RangeMove(sheet.Name, R(10, 0, 11, 0), sheet.Name, A(20, 0))));
        Assert.True(_moves.KeepsBlocksWhole(workbook, new RangeMove(sheet.Name, R(0, 0, 5, 2), sheet.Name, A(20, 0))));
        Assert.False(_moves.KeepsBlocksWhole(workbook, new RangeMove(sheet.Name, R(0, 0, 0, 0), sheet.Name, A(CellAddress.MaxRows, 0))));
    }

    [Fact]
    public void Rules_validations_comments_merges_charts_sparklines_and_pivots_follow_a_moved_range()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        var name = sheet.Name;
        var other = workbook.AddSheet("Two");
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R(0, 0, 3, 0), Formula = "A1>0" });
        sheet.Validations.Add(new DataValidation { Range = R(0, 0, 3, 0), FirstFormula = "B1" });
        sheet.Comments[A(1, 0)] = new CellComment { Text = "note" };
        sheet.Merges.AddRange([R(2, 0, 2, 1)]);
        sheet.Sparklines.Add(new SparklineGroup { Id = "s", Sparklines = [new Sparkline(A(0, 1), R(0, 0, 3, 0))] });
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = name, SourceRange = "A1:B4" };
        other.Objects.Add(new PlacedObject { Id = "c", Content = chart, Anchor = A(0, 0) });
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p", Name = "p", SourceSheet = name, SourceRange = R(0, 0, 3, 1), Destination = A(0, 3), DestinationSheet = "Two",
        });

        _moves.Move(engine, new RangeMove(name, R(0, 0, 3, 1), name, A(10, 4)));

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal(R(10, 4, 13, 4), rule.Range);
        Assert.Equal("E11>0", rule.Formula);
        var validation = Assert.Single(sheet.Validations);
        Assert.Equal(R(10, 4, 13, 4), validation.Range);
        Assert.Equal("F11", validation.FirstFormula);
        Assert.Equal("note", sheet.Comments[A(11, 4)].Text);
        Assert.False(sheet.Comments.ContainsKey(A(1, 0)));
        Assert.Contains(R(12, 4, 12, 5), sheet.Merges.All);
        Assert.DoesNotContain(R(2, 0, 2, 1), sheet.Merges.All);
        Assert.Equal(new Sparkline(A(10, 5), R(10, 4, 13, 4)), sheet.Sparklines.Groups[0].Sparklines[0]);
        Assert.Equal("E11:F14", Assert.IsType<ChartDefinition>(other.Objects[0].Content).SourceRange);
        Assert.Equal(R(10, 4, 13, 5), workbook.Pivots[0].SourceRange);
    }

    [Fact]
    public void A_sheet_copy_rebases_local_names_charts_and_pivots_and_leaves_the_original_alone()
    {
        var (workbook, _) = fixture.CreateWorkbook();
        var source = workbook.Sheets[0];
        var name = source.Name;
        workbook.DefineName(new DefinedName("Local", $"{name}.$A$1", name));
        workbook.DefineName(new DefinedName("Global", $"{name}.$B$1:$B$3"));
        workbook.DefineName(new DefinedName("Elsewhere", "Other.$A$1"));
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = name, SourceRange = "A1:B4" };
        source.Objects.Add(new PlacedObject { Id = "c", Content = chart, Anchor = A(0, 3) });
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p", Name = "p", SourceSheet = name, SourceRange = R(0, 0, 3, 1), Destination = A(0, 5), DestinationSheet = name,
        });

        var copy = _duplicator.Duplicate(workbook, source, "Copy");
        _duplicator.Insert(workbook, copy, 1);

        Assert.Equal("Copy.$A$1", workbook.FindName("Local", "Copy")!.Expression);
        Assert.Equal("Copy.$B$1:$B$3", workbook.FindName("Global", "Copy")!.Expression);
        Assert.Null(workbook.FindName("Elsewhere", "Copy"));
        Assert.Equal($"{name}.$A$1", workbook.FindName("Local", name)!.Expression);
        Assert.Equal("Copy", Assert.IsType<ChartDefinition>(copy.Sheet.Objects[0].Content).SourceSheet);
        Assert.Equal(name, Assert.IsType<ChartDefinition>(source.Objects[0].Content).SourceSheet);
        Assert.Equal(2, workbook.Pivots.Count);
        Assert.Equal("Copy", workbook.Pivots[1].DestinationSheet);
        Assert.Equal("Copy", workbook.Pivots[1].SourceSheet);
        Assert.NotEqual(workbook.Pivots[0].Name, workbook.Pivots[1].Name);

        _duplicator.Remove(workbook, copy);
        Assert.Null(workbook.FindName("Local", "Copy"));
        Assert.Single(workbook.Pivots);
        Assert.Single(workbook.Sheets);

        _duplicator.Insert(workbook, copy, 1);
        Assert.Equal("Copy.$A$1", workbook.FindName("Local", "Copy")!.Expression);
        Assert.Equal(2, workbook.Pivots.Count);
    }
}
