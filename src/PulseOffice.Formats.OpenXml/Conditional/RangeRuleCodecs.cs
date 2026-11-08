using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.OpenXml;

internal abstract class PlainStyledRuleCodec(SpreadsheetColours colours, DifferentialFormatTable formats) : StyledRuleCodec(colours, formats)
{
    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.Style && rule.Formula is null && Handles(rule.Operator);

    protected abstract bool Handles(ConditionOperator op);
}

internal sealed class XlsxDuplicateCodec(SpreadsheetColours colours, DifferentialFormatTable formats) : PlainStyledRuleCodec(colours, formats)
{
    protected override bool Handles(ConditionOperator op) => op is ConditionOperator.Duplicate or ConditionOperator.Unique;

    public override bool Reads(ConditionalFormattingRule element) =>
        element.Type?.Value == ConditionalFormatValues.DuplicateValues || element.Type?.Value == ConditionalFormatValues.UniqueValues;

    protected override void Condition(ConditionalFormattingRule element, ConditionalRule rule) =>
        element.Type = rule.Operator == ConditionOperator.Duplicate ? ConditionalFormatValues.DuplicateValues : ConditionalFormatValues.UniqueValues;

    protected override ConditionalRule Condition(ConditionalFormattingRule element, XlsxRuleReadContext context, ConditionalRule rule) =>
        rule with { Operator = element.Type?.Value == ConditionalFormatValues.DuplicateValues ? ConditionOperator.Duplicate : ConditionOperator.Unique };
}

internal sealed class XlsxTopItemsCodec(SpreadsheetColours colours, DifferentialFormatTable formats) : PlainStyledRuleCodec(colours, formats)
{
    protected override bool Handles(ConditionOperator op) =>
        op is ConditionOperator.TopItems or ConditionOperator.BottomItems or ConditionOperator.TopPercent or ConditionOperator.BottomPercent;

    public override bool Reads(ConditionalFormattingRule element) => element.Type?.Value == ConditionalFormatValues.Top10;

    protected override void Condition(ConditionalFormattingRule element, ConditionalRule rule)
    {
        element.Type = ConditionalFormatValues.Top10;
        element.Rank = rule.Operands.Count > 0 && uint.TryParse(rule.Operands[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) ? rank : 10;
        if (rule.Operator is ConditionOperator.TopPercent or ConditionOperator.BottomPercent) element.Percent = true;
        if (rule.Operator is ConditionOperator.BottomItems or ConditionOperator.BottomPercent) element.Bottom = true;
    }

    protected override ConditionalRule Condition(ConditionalFormattingRule element, XlsxRuleReadContext context, ConditionalRule rule)
    {
        var bottom = element.Bottom?.Value == true;
        var percent = element.Percent?.Value == true;
        var op = (bottom, percent) switch
        {
            (false, false) => ConditionOperator.TopItems,
            (true, false) => ConditionOperator.BottomItems,
            (false, true) => ConditionOperator.TopPercent,
            _ => ConditionOperator.BottomPercent,
        };
        return rule with { Operator = op, Operands = [(element.Rank?.Value ?? 10).ToString(CultureInfo.InvariantCulture)] };
    }
}

internal sealed class XlsxAverageCodec(SpreadsheetColours colours, DifferentialFormatTable formats) : PlainStyledRuleCodec(colours, formats)
{
    protected override bool Handles(ConditionOperator op) =>
        op is ConditionOperator.AboveAverage or ConditionOperator.BelowAverage or ConditionOperator.AboveOrEqualAverage or ConditionOperator.BelowOrEqualAverage;

    public override bool Reads(ConditionalFormattingRule element) => element.Type?.Value == ConditionalFormatValues.AboveAverage;

    protected override void Condition(ConditionalFormattingRule element, ConditionalRule rule)
    {
        element.Type = ConditionalFormatValues.AboveAverage;
        if (rule.Operator is ConditionOperator.BelowAverage or ConditionOperator.BelowOrEqualAverage) element.AboveAverage = false;
        if (rule.Operator is ConditionOperator.AboveOrEqualAverage or ConditionOperator.BelowOrEqualAverage) element.EqualAverage = true;
        if (rule.StandardDeviations > 0) element.StdDev = rule.StandardDeviations;
    }

    protected override ConditionalRule Condition(ConditionalFormattingRule element, XlsxRuleReadContext context, ConditionalRule rule)
    {
        var above = element.AboveAverage?.Value != false;
        var equal = element.EqualAverage?.Value == true;
        var op = (above, equal) switch
        {
            (true, false) => ConditionOperator.AboveAverage,
            (true, true) => ConditionOperator.AboveOrEqualAverage,
            (false, false) => ConditionOperator.BelowAverage,
            _ => ConditionOperator.BelowOrEqualAverage,
        };
        return rule with { Operator = op, StandardDeviations = Math.Clamp(element.StdDev?.Value ?? 0, 0, 3) };
    }
}

internal sealed class XlsxTimePeriodCodec(SpreadsheetColours colours, DifferentialFormatTable formats) : PlainStyledRuleCodec(colours, formats)
{
    private readonly IReadOnlyList<(DatePeriod Period, TimePeriodValues? Sdk, Func<string, string> Formula)> _periods =
    [
        (DatePeriod.Today, TimePeriodValues.Today, cell => $"FLOOR({cell},1)=TODAY()"),
        (DatePeriod.Yesterday, TimePeriodValues.Yesterday, cell => $"FLOOR({cell},1)=TODAY()-1"),
        (DatePeriod.Tomorrow, TimePeriodValues.Tomorrow, cell => $"FLOOR({cell},1)=TODAY()+1"),
        (DatePeriod.Last7Days, TimePeriodValues.Last7Days, cell => $"AND(TODAY()-FLOOR({cell},1)<=6,FLOOR({cell},1)<=TODAY())"),
        (DatePeriod.ThisWeek, TimePeriodValues.ThisWeek, cell => $"AND(TODAY()-ROUNDDOWN({cell},0)<=WEEKDAY(TODAY())-1,ROUNDDOWN({cell},0)-TODAY()<=7-WEEKDAY(TODAY()))"),
        (DatePeriod.LastWeek, TimePeriodValues.LastWeek, cell => $"AND(TODAY()-ROUNDDOWN({cell},0)>=(WEEKDAY(TODAY())),TODAY()-ROUNDDOWN({cell},0)<(WEEKDAY(TODAY())+7))"),
        (DatePeriod.NextWeek, TimePeriodValues.NextWeek, cell => $"AND(ROUNDDOWN({cell},0)-TODAY()>(7-WEEKDAY(TODAY())),ROUNDDOWN({cell},0)-TODAY()<(15-WEEKDAY(TODAY())))"),
        (DatePeriod.ThisMonth, TimePeriodValues.ThisMonth, cell => $"AND(MONTH({cell})=MONTH(TODAY()),YEAR({cell})=YEAR(TODAY()))"),
        (DatePeriod.LastMonth, TimePeriodValues.LastMonth, cell => $"AND(MONTH({cell})=MONTH(EDATE(TODAY(),0-1)),YEAR({cell})=YEAR(EDATE(TODAY(),0-1)))"),
        (DatePeriod.NextMonth, TimePeriodValues.NextMonth, cell => $"AND(MONTH({cell})=MONTH(EDATE(TODAY(),0+1)),YEAR({cell})=YEAR(EDATE(TODAY(),0+1)))"),
        (DatePeriod.ThisYear, null, cell => $"YEAR({cell})=YEAR(TODAY())"),
        (DatePeriod.LastYear, null, cell => $"YEAR({cell})=YEAR(TODAY())-1"),
        (DatePeriod.NextYear, null, cell => $"YEAR({cell})=YEAR(TODAY())+1"),
    ];

    protected override bool Handles(ConditionOperator op) => op == ConditionOperator.DateIs;

    public override bool Reads(ConditionalFormattingRule element) => element.Type?.Value == ConditionalFormatValues.TimePeriod;

    protected override void Condition(ConditionalFormattingRule element, ConditionalRule rule)
    {
        var entry = _periods.First(row => row.Period == rule.DatePeriod);
        if (entry.Sdk is { } sdk)
        {
            element.Type = ConditionalFormatValues.TimePeriod;
            element.TimePeriod = sdk;
        }
        else element.Type = ConditionalFormatValues.Expression;
        element.AppendChild(new Formula(entry.Formula(rule.Anchor.ToA1())));
    }

    protected override ConditionalRule Condition(ConditionalFormattingRule element, XlsxRuleReadContext context, ConditionalRule rule)
    {
        var sdk = element.TimePeriod?.Value;
        var period = _periods.FirstOrDefault(row => row.Sdk is { } value && sdk is { } read && value == read, _periods[0]).Period;
        return rule with { Operator = ConditionOperator.DateIs, DatePeriod = period };
    }
}
