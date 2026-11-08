using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Scripts;
using PulseOffice.UI.Commands;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerTextCommands(
    IActiveSheetContext context,
    ISheetFormattingService formatting,
    ISheetTextTransform transform,
    IWidthConversion width,
    IKanaConversion kana,
    PulseOffice.Text.ITextCases cases,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Align("ledger.align.top", style => style with { Vertical = CellVerticalAlignment.Top }, style => style.Vertical == CellVerticalAlignment.Top),
        Align("ledger.align.middle", style => style with { Vertical = CellVerticalAlignment.Middle }, style => style.Vertical == CellVerticalAlignment.Middle),
        Align("ledger.align.bottom", style => style with { Vertical = CellVerticalAlignment.Bottom }, style => style.Vertical == CellVerticalAlignment.Bottom),
        Align("ledger.align.justify", style => style with { Horizontal = CellHorizontalAlignment.Justify }, style => style.Horizontal == CellHorizontalAlignment.Justify),

        Case("ledger.text.upper", cases[PulseOffice.Text.TextCaseKind.Upper].Apply),
        Case("ledger.text.lower", cases[PulseOffice.Text.TextCaseKind.Lower].Apply),
        Case("ledger.text.sentence", cases[PulseOffice.Text.TextCaseKind.Sentence].Apply),
        Case("ledger.text.title", cases[PulseOffice.Text.TextCaseKind.Title].Apply),
        Case("ledger.text.cycle", text => cases.NextInCycle(text).Apply(text)),
        Case("ledger.text.toggle", cases[PulseOffice.Text.TextCaseKind.Toggle].Apply),
        Case("ledger.text.fullWidth", text => width.ToFullWidth(text)),
        Case("ledger.text.halfWidth", text => width.ToHalfWidth(text)),
        Case("ledger.text.hiragana", text => kana.ToHiragana(text)),
        Case("ledger.text.katakana", text => kana.ToKatakana(text)),
    ];

    private CommandDefinition Align(string id, Func<CellStyle, CellStyle> change, Func<CellStyle, bool> isSet) =>
        Prototype(id) with
        {
            CheckedWhen = _ => formatting.CanFormat && isSet(formatting.CurrentStyle),
            IconKey = id,
            MenuPath = "format/alignment",
            ContextPaths = ["cell/format/align"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => formatting.Accepts(id),
            Execute = (_, _) =>
            {
                formatting.ApplyToSelection(texts[$"command.{id}.title"], change);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition Case(string id, Func<string, string> change) =>
        Prototype(id) with
        {
            MenuPath = "format/text",
            ContextPaths = ["cell/format/case"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                transform.ApplyToSelection(texts[$"command.{id}.title"], change);
                return Task.CompletedTask;
            },
        };

}
