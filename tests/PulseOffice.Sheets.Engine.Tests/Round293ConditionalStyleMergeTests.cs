using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round293ConditionalStyleMergeTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly CellRange Cell = new(new CellAddress(0, 0), new CellAddress(0, 0));

    [Fact]
    public void Two_matching_rules_merge_their_formats_with_the_higher_priority_winning_conflicts()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(5));
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = Cell, Priority = 0, Operator = ConditionOperator.GreaterThan, Operands = ["1"],
            Style = CellStyle.Default with { Font = CellFont.Default with { Bold = true, ColorHex = "#9C0006" } },
        });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = Cell, Priority = 1, Operator = ConditionOperator.GreaterThan, Operands = ["2"],
            Style = CellStyle.Default with { BackgroundHex = "#FFC7CE", Font = CellFont.Default with { ColorHex = "#006100" } },
        });

        var style = new ConditionalFormattingEvaluator(fixture.Evaluator).Evaluate(sheet, new CellAddress(0, 0)).Style;

        Assert.NotNull(style);
        Assert.True(style.Font.Bold);
        Assert.Equal("#9C0006", style.Font.ColorHex);
        Assert.Equal("#FFC7CE", style.BackgroundHex);
    }

    [Fact]
    public void Stop_if_true_keeps_lower_rules_out_of_the_merge()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(5));
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = Cell, Priority = 0, Operator = ConditionOperator.GreaterThan, Operands = ["1"], StopIfTrue = true,
            Style = CellStyle.Default with { Font = CellFont.Default with { Bold = true } },
        });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = Cell, Priority = 1, Operator = ConditionOperator.GreaterThan, Operands = ["2"],
            Style = CellStyle.Default with { BackgroundHex = "#FFC7CE" },
        });

        var style = new ConditionalFormattingEvaluator(fixture.Evaluator).Evaluate(sheet, new CellAddress(0, 0)).Style;

        Assert.NotNull(style);
        Assert.Null(style.BackgroundHex);
    }
}
