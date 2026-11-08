using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class RangeConditionEvaluationTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private sealed class FixedClock(DateOnly today) : ISystemClock
    {
        public DateTime Now => today.ToDateTime(new TimeOnly(10, 0));

        public DateOnly Today => today;
    }

    private static readonly CellRange Column = new(new CellAddress(0, 0), new CellAddress(9, 0));

    private static readonly CellStyle Marked = CellStyle.Default with { BackgroundHex = "#FF0000" };

    private IReadOnlyList<int> MarkedRows(Worksheet sheet, ConditionalRule rule, DateOnly? today = null)
    {
        sheet.ConditionalRules.Add(rule with { Range = Column, Style = Marked });
        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator, new FixedClock(today ?? new DateOnly(2026, 10, 14)), new StandardDateSystem());
        return [.. Enumerable.Range(0, 10).Where(row => evaluator.Evaluate(sheet, new CellAddress(row, 0)).Style is not null)];
    }

    private static Worksheet Numbers(params double[] values)
    {
        var sheet = new Worksheet("Foglio1");
        for (var i = 0; i < values.Length; i++) sheet.SetValue(new CellAddress(i, 0), CellValue.Number(values[i]));
        return sheet;
    }

    [Fact]
    public void IDuplicatiEGliUniciSiRiconoscono()
    {
        Assert.Equal([0, 2], MarkedRows(Numbers(4, 5, 4, 6), new ConditionalRule { Range = Column, Operator = ConditionOperator.Duplicate }));
        Assert.Equal([1, 3], MarkedRows(Numbers(4, 5, 4, 6), new ConditionalRule { Range = Column, Operator = ConditionOperator.Unique }));
    }

    [Fact]
    public void IPrimiEGliUltimiSiRiconoscono()
    {
        Assert.Equal([7, 8, 9], MarkedRows(Numbers(1, 2, 3, 4, 5, 6, 7, 8, 9, 10), new ConditionalRule { Range = Column, Operator = ConditionOperator.TopItems, Operands = ["3"] }));
        Assert.Equal([0, 1], MarkedRows(Numbers(1, 2, 3, 4, 5, 6, 7, 8, 9, 10), new ConditionalRule { Range = Column, Operator = ConditionOperator.BottomPercent, Operands = ["20"] }));
    }

    [Fact]
    public void LaMediaSiConfronta()
    {
        Assert.Equal([2, 3], MarkedRows(Numbers(1, 2, 4, 7), new ConditionalRule { Range = Column, Operator = ConditionOperator.AboveAverage }));
        Assert.Equal([0, 1], MarkedRows(Numbers(1, 2, 4, 7), new ConditionalRule { Range = Column, Operator = ConditionOperator.BelowAverage }));
    }

    [Fact]
    public void LaMediaConDeviazioniStandardSpostaLaSoglia()
    {
        Assert.Equal([4], MarkedRows(Numbers(1, 2, 3, 4, 20), new ConditionalRule { Range = Column, Operator = ConditionOperator.AboveAverage, StandardDeviations = 1 }));
        Assert.Empty(MarkedRows(Numbers(1, 2, 3, 4, 20), new ConditionalRule { Range = Column, Operator = ConditionOperator.BelowAverage, StandardDeviations = 1 }));
    }

    [Fact]
    public void LeDateSiConfrontanoConIlGiornoCorrente()
    {
        var dates = new StandardDateSystem();
        var sheet = Numbers(
            dates.ToSerial(new DateTime(2026, 10, 14)),
            dates.ToSerial(new DateTime(2026, 10, 13, 18, 0, 0)),
            dates.ToSerial(new DateTime(2026, 9, 30)),
            dates.ToSerial(new DateTime(2025, 10, 14)));

        Assert.Equal([0], MarkedRows(sheet, new ConditionalRule { Range = Column, Operator = ConditionOperator.DateIs, DatePeriod = DatePeriod.Today }));
        sheet.ConditionalRules.Clear();
        Assert.Equal([1], MarkedRows(sheet, new ConditionalRule { Range = Column, Operator = ConditionOperator.DateIs, DatePeriod = DatePeriod.Yesterday }));
        sheet.ConditionalRules.Clear();
        Assert.Equal([2], MarkedRows(sheet, new ConditionalRule { Range = Column, Operator = ConditionOperator.DateIs, DatePeriod = DatePeriod.LastMonth }));
        sheet.ConditionalRules.Clear();
        Assert.Equal([3], MarkedRows(sheet, new ConditionalRule { Range = Column, Operator = ConditionOperator.DateIs, DatePeriod = DatePeriod.LastYear }));
    }
}
