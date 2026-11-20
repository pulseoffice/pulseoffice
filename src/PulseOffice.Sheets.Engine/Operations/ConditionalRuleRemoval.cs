using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IConditionalRuleRemoval
{
    int Remove(Worksheet sheet, CellRange selection, string title, UndoStack? history = null);
}

public sealed class ConditionalRuleRemoval : IConditionalRuleRemoval
{
    private readonly RangeCutter _cutter = new();
    private readonly FormulaTranslator _translator = new();

    public int Remove(Worksheet sheet, CellRange selection, string title, UndoStack? history = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        List<ConditionalRule> before = [.. sheet.ConditionalRules];
        var touched = 0;
        var after = new List<ConditionalRule>(before.Count);
        foreach (var rule in before)
        {
            if (!rule.Ranges.Any(range => range.IntersectsWith(selection)))
            {
                after.Add(rule);
                continue;
            }

            touched++;
            var (_, outside) = _cutter.Split(rule.Ranges, selection);
            if (outside.Count > 0) after.Add(Reanchored(rule, outside));
        }
        if (touched == 0) return 0;

        void Set(List<ConditionalRule> rules)
        {
            sheet.ConditionalRules.Clear();
            sheet.ConditionalRules.AddRange(rules);
        }

        Set(after);
        history?.Push(new DelegateAction(title, () => Set(after), () => Set(before)));
        return touched;
    }

    private ConditionalRule Reanchored(ConditionalRule rule, IReadOnlyList<CellRange> ranges)
    {
        var rowShift = ranges[0].Start.Row - rule.Anchor.Row;
        var columnShift = ranges[0].Start.Column - rule.Anchor.Column;
        var moved = rowShift == 0 && columnShift == 0 ? rule : rule.WithFormulas(formula => Shifted(formula, rowShift, columnShift));
        return moved with { Range = ranges[0], AdditionalRanges = [.. ranges.Skip(1)] };
    }

    private string? Shifted(string? formula, int rowShift, int columnShift)
    {
        if (formula is null) return null;
        var translated = _translator.Translate(formula.StartsWith('=') ? formula : "=" + formula, rowShift, columnShift);
        return translated is null ? formula : formula.StartsWith('=') ? translated : translated.TrimStart('=');
    }
}
