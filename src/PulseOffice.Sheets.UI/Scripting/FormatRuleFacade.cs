using PulseOffice.Scripting.Editor.Facades;
using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class FormatRuleFacade(IScriptDispatcher dispatcher, IActiveSheetContext context, IScriptReferences references, IScriptColors palette, IScriptProtection protection) : ScriptFacade(dispatcher)
{
    public int Count => Read(() => Sheet()?.ConditionalRules.Count ?? 0);

    public string[] Describe() => Read<string[]>(() =>
        Sheet() is { } sheet ? [.. sheet.ConditionalRules.Select(rule => rule.Describe())] : []);

    public bool AddCondition(
        string reference,
        string comparison,
        string value,
        string? backgroundHex = null,
        string? colorHex = null,
        bool bold = false) => Add(reference, rule => Condition(rule with
    {
        Visual = ConditionalVisual.Style,
        Operator = Comparison(comparison),
        Style = new CellStyle
        {
            BackgroundHex = palette.OptionalHex(backgroundHex),
            Font = CellFont.Default with
            {
                Bold = bold,
                ColorHex = palette.OptionalHex(colorHex) ?? CellFont.Default.ColorHex,
            },
        },
    }, value));

    public bool AddDataBar(string reference, string? colorHex = null, string? negativeColorHex = null, bool gradient = true) =>
        Add(reference, rule => rule with
        {
            Visual = ConditionalVisual.DataBar,
            BarColorHex = palette.OptionalHex(colorHex) ?? "#638EC6",
            NegativeBarColorHex = palette.OptionalHex(negativeColorHex),
            Bar = rule.Bar with { Gradient = gradient },
        });

    public bool AddColorScale(string reference, string[]? colors = null) => Add(reference, rule => rule with
    {
        Visual = ConditionalVisual.ColorScale,
        ScaleColors = Scale(colors),
    });

    public bool AddFormula(
        string reference, string formula, string? backgroundHex = null) => Add(reference, rule => rule with
    {
        Visual = ConditionalVisual.Style,
        Formula = !string.IsNullOrWhiteSpace(formula) && formula != "="
            ? formula.StartsWith('=') ? formula : "=" + formula
            : throw new ArgumentException("The formula is empty.", nameof(formula)),
        Style = new CellStyle { BackgroundHex = palette.OptionalHex(backgroundHex) },
    });

    public int Clear(string? reference = null) => Write(() =>
    {
        var scope = references.Scope(reference);
        protection.Format();
        if (Sheet() is not { } sheet) return 0;

        var removed = scope is { } range
            ? sheet.ConditionalRules.Where(rule => rule.Ranges.Any(area => area.IntersectsWith(range))).ToList()
            : [.. sheet.ConditionalRules];

        if (removed.Count == 0) return 0;

        var before = sheet.ConditionalRules.ToList();
        foreach (var rule in removed) sheet.ConditionalRules.Remove(rule);
        var after = sheet.ConditionalRules.ToList();

        context.History?.Push(new DelegateAction(
            "ConditionalRules",
            redo: () => Restore(sheet, after),
            undo: () => Restore(sheet, before)));

        context.RequestRefresh();

        return removed.Count;
    });

    private bool Add(string reference, Func<ConditionalRule, ConditionalRule> build) =>
        Write(() =>
        {
            var range = references.Range(reference);
            protection.Format();
            if (Sheet() is not { } sheet) return false;

            var rule = build(new ConditionalRule
            {
                Range = range,
                Priority = sheet.ConditionalRules.Count + 1,
            });

            sheet.ConditionalRules.Add(rule);

            context.History?.Push(new DelegateAction(
                "ConditionalRule",
                redo: () => sheet.ConditionalRules.Add(rule),
                undo: () => sheet.ConditionalRules.Remove(rule)));

            context.RequestRefresh();

            return true;
        });

    private string[] Scale(string[]? scale) => scale switch
    {
        null or { Length: 0 } => ["#F8696B", "#FFEB84", "#63BE7B"],
        { Length: 2 or 3 } => [.. scale.Select(palette.Hex)],
        _ => throw new ArgumentException($"A colour scale has 2 or 3 colours, not {scale.Length}.", nameof(scale)),
    };

    private ConditionOperator Comparison(string comparison) => comparison.ToLowerInvariant() switch
    {
        ">" or "greater" or "maggiore" => ConditionOperator.GreaterThan,
        ">=" or "greaterorequal" => ConditionOperator.GreaterOrEqual,
        "<" or "less" or "minore" => ConditionOperator.LessThan,
        "<=" or "lessorequal" => ConditionOperator.LessOrEqual,
        "=" or "==" or "equal" or "uguale" => ConditionOperator.Equal,
        "!=" or "<>" or "notequal" => ConditionOperator.NotEqual,
        "between" or "fra" => ConditionOperator.Between,
        "contains" or "contiene" => ConditionOperator.ContainsText,
        "beginswith" => ConditionOperator.BeginsWith,
        "endswith" => ConditionOperator.EndsWith,
        "notcontains" => ConditionOperator.NotContainsText,
        "isblank" or "vuota" => ConditionOperator.IsBlank,
        "isnotblank" => ConditionOperator.IsNotBlank,
        "iserror" or "errore" => ConditionOperator.IsError,
        "isnoterror" => ConditionOperator.IsNotError,
        "duplicate" => ConditionOperator.Duplicate,
        "unique" => ConditionOperator.Unique,
        "top" => ConditionOperator.TopItems,
        "bottom" => ConditionOperator.BottomItems,
        "toppercent" => ConditionOperator.TopPercent,
        "bottompercent" => ConditionOperator.BottomPercent,
        "aboveaverage" => ConditionOperator.AboveAverage,
        "belowaverage" => ConditionOperator.BelowAverage,
        "aboveorequalaverage" => ConditionOperator.AboveOrEqualAverage,
        "beloworequalaverage" => ConditionOperator.BelowOrEqualAverage,
        "date" => ConditionOperator.DateIs,
        _ => throw new ArgumentException($"Unknown comparison '{comparison}'.", nameof(comparison)),
    };

    private ConditionalRule Condition(ConditionalRule rule, string value) => rule.Operator switch
    {
        ConditionOperator.DateIs => rule with { DatePeriod = Period(value) },
        ConditionOperator.TopItems or ConditionOperator.BottomItems or ConditionOperator.TopPercent or ConditionOperator.BottomPercent =>
            rule with { Operands = [RankCount(value, rule.Operator is ConditionOperator.TopPercent or ConditionOperator.BottomPercent)] },
        ConditionOperator.IsBlank or ConditionOperator.IsNotBlank or ConditionOperator.IsError or ConditionOperator.IsNotError
            or ConditionOperator.Duplicate or ConditionOperator.Unique => rule,
        ConditionOperator.AboveAverage or ConditionOperator.BelowAverage
            or ConditionOperator.AboveOrEqualAverage or ConditionOperator.BelowOrEqualAverage =>
            rule with { StandardDeviations = Deviations(value) },
        _ => rule with { Operands = Operands(value) },
    };

    private DatePeriod Period(string value) =>
        Enum.TryParse<DatePeriod>(value.Trim(), ignoreCase: true, out var period) && Enum.IsDefined(period)
            ? period
            : throw new ArgumentException($"Unknown date period '{value}'.", nameof(value));

    private int Deviations(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? 0
            : int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var deviations) && deviations is >= 0 and <= 3
                ? deviations
                : throw new ArgumentException($"'{value}' is not a number of standard deviations from 0 to 3.", nameof(value));

    private string RankCount(string value, bool percent) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var count) && count >= 1 && (!percent || count <= 100)
            ? Math.Round(count).ToString(CultureInfo.InvariantCulture)
            : throw new ArgumentException($"'{value}' is not a valid count.", nameof(value));

    private string[] Operands(string value) =>
        [.. value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private void Restore(Worksheet sheet, IReadOnlyList<ConditionalRule> rules)
    {
        sheet.ConditionalRules.Clear();
        sheet.ConditionalRules.AddRange(rules);
        context.RequestRefresh();
    }

    private Worksheet? Sheet() =>
        context.Workbook is { } workbook ? workbook.Sheets[context.SheetIndex] : null;
}
