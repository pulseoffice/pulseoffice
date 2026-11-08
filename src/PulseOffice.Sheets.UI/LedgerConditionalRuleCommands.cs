using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.Sheets.Styling;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.Drawing.Assets;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerConditionalRuleCommands(
    ISheetFormattingService formatting,
    IActiveSheetContext context,
    IStatusBarService status,
    ISheetFormatGate gate,
    ITextCatalog texts,
    Engine.Operations.IConditionalRuleRemoval ruleRemoval) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() => ConditionalCommands();

    private IEnumerable<CommandDefinition> ConditionalCommands() =>
    [
        Conditional("ledger.conditional.greater",
            range => new ConditionalRule
            {
                Range = range,
                Operator = ConditionOperator.GreaterThan,
                Operands = [AverageOf(range).ToString(System.Globalization.CultureInfo.InvariantCulture)],
                Style = new CellStyle
                {
                    BackgroundHex = "#D7F0DC",
                    Font = CellFont.Default with { ColorHex = "#14532D" },
                },
            }),

        Conditional("ledger.conditional.negative",
            range => new ConditionalRule
            {
                Range = range,
                Operator = ConditionOperator.LessThan,
                Operands = ["0"],
                Style = new CellStyle
                {
                    BackgroundHex = "#FBD5D5",
                    Font = CellFont.Default with { ColorHex = "#991B1B" },
                },
            }),

        Conditional("ledger.conditional.dataBars",
            range => new ConditionalRule { Range = range, Visual = ConditionalVisual.DataBar }),

        Conditional("ledger.conditional.colorScale",
            range => new ConditionalRule { Range = range, Visual = ConditionalVisual.ColorScale }),

        Data("ledger.conditional.clear", null, () =>
        {
            if (Context.Workbook is not { } workbook) return;

            var sheet = workbook.Sheets[Context.SheetIndex];
            if (gate.Refuses(sheet)) return;
            var range = Context.Selection;

            if (ruleRemoval.Remove(
                sheet, range, texts["command.ledger.conditional.clear.title"], Context.History) == 0)
            {
                status.Report(texts["status.conditional.noneInSelection"], NotificationSeverity.Warning);
                return;
            }

            Context.RequestRefresh();
        }),
    ];

    private CommandDefinition Conditional(
    string id, Func<CellRange, ConditionalRule> build) =>
    Prototype(id) with
    {
        MenuPath = "format/conditional",
        Surface = CommandSurface.Common,
        RequiredState = DocumentState.DocumentOpen,
        RequiredSelection = SelectionKind.CellRange,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => formatting.CanFormat,
        Execute = (_, _) =>
        {
            if (Context.Workbook is not { } workbook) return Task.CompletedTask;

            var sheet = workbook.Sheets[Context.SheetIndex];
            if (gate.Refuses(sheet)) return Task.CompletedTask;

            var rule = build(Context.Selection) with
            {
                Priority = sheet.ConditionalRules.Count,
            };

            sheet.ConditionalRules.Add(rule);

            Context.History?.Push(new Document.Commands.DelegateAction(
                texts[$"command.{id}.title"],
                redo: () => sheet.ConditionalRules.Add(rule),
                undo: () => sheet.ConditionalRules.Remove(rule)));

            Context.RequestRefresh();
            return Task.CompletedTask;
        },
    };

    private double AverageOf(CellRange range)
    {
        if (Context.Workbook is not { } workbook) return 0;

        var numbers = workbook.Sheets[Context.SheetIndex]
            .NonEmptyCellsIn(range)
            .Where(pair => pair.Value.Value.Kind == ValueKind.Number)
            .Select(pair => pair.Value.Value.AsNumber())
            .ToList();

        return numbers.Count == 0 ? 0 : Math.Round(numbers.Average(), 4);
    }

    private CommandDefinition Data(string id, string? gesture, Action action, Func<bool>? enabled = null) =>
    Prototype(id) with
    {
        MenuPath = "data",
        DefaultGesture = gesture,
        Surface = CommandSurface.Common,
        RequiredState = DocumentState.DocumentOpen,
        RequiredSelection = SelectionKind.CellRange,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => formatting.CanFormat && (enabled?.Invoke() ?? true),
        Execute = (_, _) =>
        {
            action();
            return Task.CompletedTask;
        },
    };
}
