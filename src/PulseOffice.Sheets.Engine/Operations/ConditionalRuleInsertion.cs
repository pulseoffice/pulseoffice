using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IConditionalRuleInsertion
{
    ConditionalRule InsertFirst(Worksheet sheet, ConditionalRule rule, string title, UndoStack? history = null);
}

public sealed class ConditionalRuleInsertion : IConditionalRuleInsertion
{
    public ConditionalRule InsertFirst(Worksheet sheet, ConditionalRule rule, string title, UndoStack? history = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rule);

        List<ConditionalRule> before = [.. sheet.ConditionalRules];
        List<ConditionalRule> after =
        [
            .. before.OrderBy(existing => existing.Priority).Prepend(rule)
                .Select((existing, index) => existing.Priority == index ? existing : existing with { Priority = index }),
        ];

        void Set(List<ConditionalRule> rules)
        {
            sheet.ConditionalRules.Clear();
            sheet.ConditionalRules.AddRange(rules);
        }

        Set(after);
        history?.Push(new DelegateAction(title, () => Set(after), () => Set(before)));
        return after[0];
    }
}
