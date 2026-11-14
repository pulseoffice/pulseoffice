using System.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ConditionalFormattingPerformanceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private const int Rows = 100_000;

    private const int Period = 997;

    private static readonly CellRange Column = new(new CellAddress(0, 0), new CellAddress(Rows - 1, 0));

    private static readonly CellStyle Marked = CellStyle.Default with { BackgroundHex = "#FF0000" };

    private static Worksheet LargeSheet()
    {
        var sheet = new Worksheet("Large");
        for (var row = 0; row < Rows; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row % Period));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = Column, Visual = ConditionalVisual.ColorScale, Priority = 1 });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = Column, Visual = ConditionalVisual.DataBar, Priority = 2 });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = Column, Operator = ConditionOperator.TopItems, Operands = ["10"], Priority = 3, Style = Marked,
        });
        return sheet;
    }

    [Fact]
    public void EveryCellOfALargeRangeIsPaintedWithoutRereadingTheRange()
    {
        var sheet = LargeSheet();
        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator);
        var clock = Stopwatch.StartNew();
        var evaluated = 0;
        var marked = 0;
        var scaled = 0;
        for (var row = 0; row < Rows && clock.Elapsed < TimeSpan.FromSeconds(10); row++, evaluated++)
        {
            var appearance = evaluator.Evaluate(sheet, new CellAddress(row, 0));
            if (appearance.Style is not null) marked++;
            if (appearance.ScaleColorHex is not null && appearance.BarFraction is not null) scaled++;
        }

        Assert.Equal(Rows, evaluated);
        Assert.Equal(Rows, scaled);
        Assert.Equal(Enumerable.Range(0, Rows).Count(row => row % Period == Period - 1), marked);
    }

    [Fact]
    public void A_paint_pass_orders_the_rules_by_priority_and_honours_stop_if_true_like_a_single_evaluation()
    {
        var sheet = new Worksheet("Order");
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0));
        for (var row = 0; row < 5; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        var low = CellStyle.Default with { BackgroundHex = "#00FF00" };
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Operator = ConditionOperator.GreaterThan, Operands = ["0"], Priority = 3, Style = Marked });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Operator = ConditionOperator.GreaterThan, Operands = ["2"], Priority = 1, Style = low, StopIfTrue = true });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Visual = ConditionalVisual.DataBar, Priority = 2 });
        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator);

        var pass = evaluator.Begin(sheet);

        for (var row = 0; row < 5; row++)
        {
            var address = new CellAddress(row, 0);
            Assert.Equal(evaluator.Evaluate(sheet, address), pass.At(address));
            Assert.Equal(pass.At(address), pass.At(address));
        }
        Assert.Equal(low, pass.At(new CellAddress(4, 0)).Style);
        Assert.Null(pass.At(new CellAddress(4, 0)).BarFraction);
        Assert.Equal(Marked, pass.At(new CellAddress(1, 0)).Style);
        Assert.NotNull(pass.At(new CellAddress(1, 0)).BarFraction);
    }

    [Fact]
    public void A_sheet_without_rules_gives_an_empty_pass()
    {
        var pass = new ConditionalFormattingEvaluator(fixture.Evaluator).Begin(new Worksheet("Empty"));

        Assert.True(pass.IsEmpty);
        Assert.Equal(ConditionalAppearance.None, pass.At(CellAddress.First));
    }

    [Fact]
    public void AnEditedValueIsSeenByTheNextEvaluation()
    {
        var sheet = new Worksheet("Small");
        for (var row = 0; row < 10; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Operator = ConditionOperator.TopItems, Operands = ["1"], Style = Marked });
        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator);
        Assert.NotNull(evaluator.Evaluate(sheet, new CellAddress(9, 0)).Style);

        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(100));

        Assert.Null(evaluator.Evaluate(sheet, new CellAddress(9, 0)).Style);
        Assert.NotNull(evaluator.Evaluate(sheet, new CellAddress(0, 0)).Style);
    }

    [Fact]
    public void AReplacedRuleIsNotServedFromTheCacheOfTheOldOne()
    {
        var sheet = new Worksheet("Rules");
        for (var row = 0; row < 10; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        var narrow = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Operator = ConditionOperator.TopItems, Operands = ["1"], Style = Marked });
        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator);
        Assert.Null(evaluator.Evaluate(sheet, new CellAddress(4, 0)).Style);

        sheet.ConditionalRules[0] = sheet.ConditionalRules[0] with { Range = narrow };

        Assert.NotNull(evaluator.Evaluate(sheet, new CellAddress(4, 0)).Style);
    }
}
