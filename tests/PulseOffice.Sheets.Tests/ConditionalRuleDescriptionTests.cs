using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class ConditionalRuleDescriptionTests
{
    [Fact]
    public void LaDescrizioneEInFormaCanonicaInglese()
    {
        var rule = new ConditionalRule
        {
            Range = CellRange.TryParseA1("A1:A5", out var range) ? range : default,
            Operator = ConditionOperator.Between,
            Operands = ["1", "10"],
        };

        Assert.Equal($"Between 1 10 {rule.RangeText}", rule.Describe());
        Assert.Equal($"DataBar {rule.RangeText}", (rule with { Visual = ConditionalVisual.DataBar }).Describe());
    }
}
