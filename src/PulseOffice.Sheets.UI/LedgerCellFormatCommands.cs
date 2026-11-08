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

public sealed class LedgerCellFormatCommands(
    ISheetFormattingService formatting,
    IActiveSheetContext context,
    INumberFormatCodeEditor codes,
    ISystemDateCode systemDates) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. FontCommands(),
        .. AlignmentCommands(),
        .. NumberFormatCommands(),
        .. FillCommands(),
    ];

    private IEnumerable<CommandDefinition> FontCommands() =>
    [
        Toggle("ledger.format.bold", "Ctrl+B",
            style => style.Font.Bold,
            (style, value) => style with { Font = style.Font with { Bold = value } }),

        Toggle("ledger.format.italic", "Ctrl+I",
            style => style.Font.Italic,
            (style, value) => style with { Font = style.Font with { Italic = value } }),

        Toggle("ledger.format.underline", "Ctrl+U",
            style => style.Font.Underline,
            (style, value) => style with { Font = style.Font with { Underline = value } }),

        Toggle("ledger.format.strikethrough", null,
            style => style.Font.Strikethrough,
            (style, value) => style with { Font = style.Font with { Strikethrough = value } }),

        Toggle("ledger.format.doubleUnderline", null,
            style => style.Font.DoubleUnderline,
            (style, value) => style with
            {
                Font = style.Font with { DoubleUnderline = value, Underline = value || style.Font.Underline },
            }),

        Toggle("ledger.format.overline", null,
            style => style.Font.Overline,
            (style, value) => style with { Font = style.Font with { Overline = value } }),

        Toggle("ledger.format.shadow", null,
            style => style.Font.Shadow,
            (style, value) => style with { Font = style.Font with { Shadow = value } }),

        Toggle("ledger.format.outline", null,
            style => style.Font.Outline,
            (style, value) => style with { Font = style.Font with { Outline = value } }),

        Toggle("ledger.format.superscript", null,
            style => style.Font.Script == CellScript.Superscript,
            (style, value) => style with
            {
                Font = style.Font with { Script = value ? CellScript.Superscript : CellScript.Normal },
            }),

        Toggle("ledger.format.subscript", null,
            style => style.Font.Script == CellScript.Subscript,
            (style, value) => style with
            {
                Font = style.Font with { Script = value ? CellScript.Subscript : CellScript.Normal },
            }),

        Apply("ledger.format.grow", "font", "Ctrl+Shift+>",
            style => style with { Font = style.Font with { Size = Math.Min(style.Font.Size + 1, 96) } }),

        Apply("ledger.format.shrink", "font", "Ctrl+Shift+<",
            style => style with { Font = style.Font with { Size = Math.Max(style.Font.Size - 1, 4) } }),
    ];

    private IEnumerable<CommandDefinition> AlignmentCommands() =>
    [
        Apply("ledger.align.left", "alignment", null,
            style => style with { Horizontal = CellHorizontalAlignment.Left })
            with { CheckedWhen = _ => Current().Horizontal == CellHorizontalAlignment.Left },

        Apply("ledger.align.center", "alignment", null,
            style => style with { Horizontal = CellHorizontalAlignment.Center })
            with { CheckedWhen = _ => Current().Horizontal == CellHorizontalAlignment.Center },

        Apply("ledger.align.right", "alignment", null,
            style => style with { Horizontal = CellHorizontalAlignment.Right })
            with { CheckedWhen = _ => Current().Horizontal == CellHorizontalAlignment.Right },

        Apply("ledger.align.general", "alignment", null,
            style => style with { Horizontal = CellHorizontalAlignment.General }),

        Toggle("ledger.align.wrap", null,
            style => style.WrapText,
            (style, value) => style with { WrapText = value }),

        Apply("ledger.align.indent", "alignment", null,
            style => style with { Indent = Math.Min(style.Indent + 1, 15) }),

        Apply("ledger.align.outdent", "alignment", null,
            style => style with { Indent = Math.Max(style.Indent - 1, 0) }),
    ];

    private IEnumerable<CommandDefinition> NumberFormatCommands() =>
    [
        NumberFormat("ledger.number.general", "General"),
        NumberFormat("ledger.number.integer", "#,##0"),
        NumberFormat("ledger.number.decimal", "#,##0.00"),
        NumberFormat("ledger.number.currency", "#,##0.00 \"€\""),
        NumberFormat("ledger.number.percent", "0.00%"),
        NumberFormat("ledger.number.scientific", "0.00E+00"),
        NumberFormat("ledger.number.date", () => systemDates.ShortDate(System.Globalization.CultureInfo.CurrentCulture)),
        NumberFormat("ledger.number.time", "hh:mm:ss"),
        NumberFormat("ledger.number.text", "@"),

        Apply("ledger.number.moreDecimals", "numbers", null,
            style => WithDecimals(style, +1)),

        Apply("ledger.number.fewerDecimals", "numbers", null,
            style => WithDecimals(style, -1)),
    ];

    private IEnumerable<CommandDefinition> FillCommands() =>
    [
        Apply("ledger.fill.none", "cells", null,
            style => style with { BackgroundHex = null }),

        Apply("ledger.fill.yellow", "cells", null,
            style => style with { BackgroundHex = "#FFF3B0" }),

        Apply("ledger.fill.green", "cells", null,
            style => style with { BackgroundHex = "#D7F0DC" }),

        Apply("ledger.fill.red", "cells", null,
            style => style with { BackgroundHex = "#FBD5D5" }),
    ];

    private CommandDefinition Toggle(
    string id, string? gesture,
    Func<CellStyle, bool> read, Func<CellStyle, bool, CellStyle> write) =>
    Build(id, "font", gesture, () => formatting.ToggleOnSelection(read, write))
        with { CheckedWhen = _ => read(Current()) };

    private CellStyle Current() => formatting.CanFormat ? formatting.CurrentStyle : CellStyle.Default;

    private CommandDefinition Apply(
    string id, string group, string? gesture, Func<CellStyle, CellStyle> change) =>
    Build(id, group, gesture, () => formatting.ApplyToSelection(change));

    private CommandDefinition NumberFormat(string id, string code) => NumberFormat(id, () => code);

    private CommandDefinition NumberFormat(string id, Func<string> code) =>
    Build(id, "numbers", null, () =>
    {
        if (Context.Workbook is not { } workbook) return;

        var formatId = workbook.NumberFormats.Register(code());
        formatting.ApplyToSelection(style => style with { NumberFormatId = formatId });
    });

    private CommandDefinition Build(
        string id, string group, string? gesture, Action action) =>
        Prototype(id) with
        {
            MenuPath = $"format/{group}",
            RibbonPath = $"home/{group}",
            DefaultGesture = gesture,
            Surface = CommandSurface.Common,

            RequiredSelection = SelectionKind.AnyCell,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => formatting.Accepts(id),
            Execute = (_, _) =>
            {
                action();
                return Task.CompletedTask;
            },
        };

    private CellStyle WithDecimals(CellStyle style, int delta)
    {
        if (Context.Workbook is not { } workbook) return style;

        var current = workbook.NumberFormats.Get(style.NumberFormatId);
        var first = current.Sections[0];

        if (first.ValueKind is not (FormatValueKind.Number or FormatValueKind.Scientific)) return style;

        var decimals = Math.Clamp(first.DecimalPlaces + delta, 0, 15);

        if (current.IsGeneral) return style with { NumberFormatId = workbook.NumberFormats.Register(decimals == 0 ? "0" : "0." + new string('0', decimals)) };

        return style with { NumberFormatId = workbook.NumberFormats.Register(codes.WithDecimals(current.Code, decimals)) };
    }
}
