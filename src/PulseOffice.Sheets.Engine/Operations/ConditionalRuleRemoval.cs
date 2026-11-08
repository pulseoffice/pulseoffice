using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IConditionalRuleRemoval
{
    int Remove(Worksheet sheet, CellRange selection, string title, UndoStack? history = null);
}

public sealed class ConditionalRuleRemoval : IConditionalRuleRemoval
{
    public int Remove(Worksheet sheet, CellRange selection, string title, UndoStack? history = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var removed = sheet.ConditionalRules
            .Select((rule, index) => (Rule: rule, Index: index))
            .Where(pair => pair.Rule.Ranges.Any(range => range.IntersectsWith(selection)))
            .ToList();
        if (removed.Count == 0) return 0;

        void Redo()
        {
            foreach (var (rule, _) in removed) sheet.ConditionalRules.Remove(rule);
        }

        void Undo()
        {
            foreach (var (rule, index) in removed)
                sheet.ConditionalRules.Insert(Math.Min(index, sheet.ConditionalRules.Count), rule);
        }

        Redo();
        history?.Push(new DelegateAction(title, Redo, Undo));

        return removed.Count;
    }
}
