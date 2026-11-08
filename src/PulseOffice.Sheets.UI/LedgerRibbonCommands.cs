using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerRibbonCommands(
    IActiveSheetContext context,
    ISheetFormattingService formatting,
    ICellFormatDialog cellDialog,
    IColorDialog colours,
    IBorderPresets borders,
    IRibbonPaletteBuilder palettes,
    INumberFormatParser parser,
    INumberFormatter formatter,
    ITextCatalog texts,
    PulseOffice.UI.Controls.Services.IRibbonMemory memory,
    PulseOffice.UI.Controls.Prompts.IZoomPrompt zoom,
    ISystemDateCode systemDates) : LedgerCommandSet(context)
{
    private static readonly double[] FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];

    private const double SmallestFontSize = 1;

    private const double LargestFontSize = 409;

    private static readonly double[] ZoomSteps = [50, 75, 90, 100, 125, 150, 200, 300, 400];

    private static readonly (string Key, string Code)[] NumberFormats =
    [
        ("general", "General"),
        ("number", "#,##0.00"),
        ("currency", "#,##0.00 \"€\""),
        ("accounting", "#,##0.00 \"€\";-#,##0.00 \"€\";\"- €\""),
        ("shortDate", "dd/mm/yyyy"),
        ("longDate", "dddd d mmmm yyyy"),
        ("time", "hh:mm:ss"),
        ("percent", "0.00%"),
        ("fraction", "# ?/?"),
        ("scientific", "0.00E+00"),
        ("text", "@"),
    ];

    private string? _fontColour = memory.Recall("ledger.font.color", "#C00000");
    private string? _fillColour = memory.Recall("ledger.fill.color", "#FFFF00");
    private string _lastBorder = memory.Recall("ledger.border.preset", "bottom") ?? "bottom";
    private RibbonBorderPen _pen = new(
        memory.Recall("ledger.border.line", "thin") ?? "thin",
        memory.Recall("ledger.border.colour", "#000000") ?? "#000000");

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Format("ledger.font.family") with
        {
            RibbonControl = new FontFamilySpec(() => Current()?.Font.Family),
            Execute = (ctx, ct) => ctx.Argument is string family
                ? Apply(ctx, style => style with { Font = style.Font with { Family = family } })
                : cellDialog.ShowAsync(FormatCellsTab.Font, ct),
        },

        Format("ledger.font.size") with
        {
            RibbonControl = new NumberBoxSpec(() => Current()?.Font.Size, FontSizes, 4, 409),
            Execute = (ctx, ct) => ctx.Argument is double size
                ? double.IsFinite(size)
                    ? Apply(ctx, style => style with { Font = style.Font with { Size = Math.Clamp(size, SmallestFontSize, LargestFontSize) } })
                    : Task.CompletedTask
                : cellDialog.ShowAsync(FormatCellsTab.Font, ct),
        },

        Format("ledger.font.color") with
        {
            RibbonControl = new ColorSplitSpec(Palette, () => _fontColour, "ribbon.colour.automatic")
            {
                Selected = () => Current()?.Font.ColorHex,
                MoreCommandId = "ledger.font.color",
            },
            Execute = async (ctx, ct) =>
            {
                var colour = ctx.Argument as RibbonColor
                    ?? await colours.ChooseAsync(_fontColour, true, "command.ledger.font.color.title", ct).ConfigureAwait(true);

                if (colour is null) return;

                _fontColour = colour.Hex;
                memory.Remember("ledger.font.color", colour.Hex);
                await Apply(ctx, style => style with { Font = style.Font with { ColorHex = colour.Hex ?? "#000000" } });
            },
        },

        Format("ledger.fill.color") with
        {
            RibbonControl = new ColorSplitSpec(Palette, () => _fillColour, "ribbon.colour.none")
            {
                Selected = () => Current()?.BackgroundHex,
                MoreCommandId = "ledger.fill.color",
            },
            Execute = async (ctx, ct) =>
            {
                var colour = ctx.Argument as RibbonColor
                    ?? await colours.ChooseAsync(_fillColour, true, "command.ledger.fill.color.title", ct).ConfigureAwait(true);

                if (colour is null) return;

                _fillColour = colour.Hex;
                memory.Remember("ledger.fill.color", colour.Hex);
                await Apply(ctx, style => style with { BackgroundHex = colour.Hex });
            },
        },

        Format("ledger.border.apply") with
        {
            RibbonControl = new BorderGallerySpec(
                [.. borders.All.Select(preset => new RibbonChoice(Key(preset), texts[$"command.ledger.border.{Key(preset)}.title"])
                {
                    IconKey = $"border.{Key(preset)}",
                })],
                [.. borders.LineStyles.Select(style => new RibbonChoice(Key(style), texts[$"border.{Key(style)}"]))],
                Palette,
                () => _pen,
                () => _lastBorder)
            {
                MoreCommandId = "ledger.border.more",
            },
            Execute = (ctx, ct) => ctx.Argument is RibbonBorder border
                ? ApplyBorder(border)
                : cellDialog.ShowAsync(FormatCellsTab.Borders, ct),
        },

        Format("ledger.border.more") with
        {
            Execute = (_, ct) => cellDialog.ShowAsync(FormatCellsTab.Borders, ct),
        },

        .. borders.All.Select(preset => Format($"ledger.border.{Key(preset)}") with
        {
            Execute = (_, _) => ApplyBorder(new RibbonBorder(Key(preset), _pen.Line, _pen.ColorHex)),
        }),

        Format("ledger.number.format") with
        {
            RibbonControl = new ChoiceSpec(NumberChoices, CurrentNumberFormat)
            {
                MoreCommandId = "ledger.number.more",
                Width = 128,
            },
            Execute = (ctx, ct) => ctx.Argument is string key && LocalNumberFormats().FirstOrDefault(entry => entry.Key == key).Code is { } code
                ? ApplyNumberFormat(code)
                : cellDialog.ShowAsync(FormatCellsTab.Numbers, ct),
        },

        Format("ledger.number.more") with
        {
            Execute = (_, ct) => cellDialog.ShowAsync(FormatCellsTab.Numbers, ct),
        },

        Prototype("ledger.view.zoomTo") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            RibbonControl = new NumberBoxSpec(() => Math.Round(Context.Zoom * 100), ZoomSteps, 25, 400)
            {
                Suffix = "%",
                Width = 72,
            },
            Execute = async (ctx, ct) =>
            {
                var percent = ctx.Argument is double given
                    ? given
                    : (await zoom.AskAsync(new PulseOffice.UI.Controls.Prompts.ZoomRequest(Context.Zoom * 100, 25, 400), ct).ConfigureAwait(true))?.Percent;

                if (percent is { } value && double.IsFinite(value)) Context.SetZoom(Math.Clamp(value, 25, 400) / 100);
            },
        },
    ];

    private CommandDefinition Format(string id) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.AnyCell,
            EnabledWhen = _ => formatting.Accepts(id),
        };

    private CellStyle? Current() => formatting.CanFormat ? formatting.CurrentStyle : null;

    private RibbonPalette Palette() => palettes.Build(palettes.OfficeTheme);

    private Task Apply(CommandContext ctx, Func<CellStyle, CellStyle> change)
    {
        formatting.ApplyToSelection(texts["history.format"], change);
        return Task.CompletedTask;
    }

    private Task ApplyBorder(RibbonBorder border)
    {
        var preset = borders.All.FirstOrDefault(candidate => Key(candidate) == border.Preset);
        var style = borders.LineStyles.FirstOrDefault(candidate => Key(candidate) == border.Line, BorderStyle.Thin);
        var pen = borders.Pen(style, border.ColorHex);

        _lastBorder = border.Preset;
        _pen = new RibbonBorderPen(border.Line, border.ColorHex);
        memory.Remember("ledger.border.preset", border.Preset);
        memory.Remember("ledger.border.line", border.Line);
        memory.Remember("ledger.border.colour", border.ColorHex);

        formatting.ApplyToSelection(
            texts[$"command.ledger.border.{border.Preset}.title"],
            (range, cell, current) => current with { Borders = borders.Apply(current.Borders, preset, pen, range, cell) });

        return Task.CompletedTask;
    }

    private Task ApplyNumberFormat(string code)
    {
        if (Context.Workbook is not { } workbook) return Task.CompletedTask;

        var id = workbook.NumberFormats.Register(code);
        formatting.ApplyToSelection(texts["command.ledger.number.format.title"], style => style with { NumberFormatId = id });
        return Task.CompletedTask;
    }

    private IReadOnlyList<RibbonChoice> NumberChoices()
    {
        var value = Context.Workbook is { } workbook
            ? workbook.Sheets[Context.SheetIndex][Context.Selection.Start].Value
            : Values.CellValue.Empty;

        return [.. LocalNumberFormats().Select(entry => new RibbonChoice(entry.Key, texts[$"ribbon.number.{entry.Key}"])
        {
            Preview = Preview(value, entry.Code),
        })];
    }

    private string Preview(Values.CellValue value, string code)
    {
        if (value.Kind is Values.ValueKind.Empty) return string.Empty;

        try
        {
            return formatter.Format(value, parser.Parse(code)).Text;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private string? CurrentNumberFormat()
    {
        if (Context.Workbook is not { } workbook || Current() is not { } style) return null;

        var code = workbook.NumberFormats.CodeOf(style.NumberFormatId);
        return LocalNumberFormats().FirstOrDefault(entry => entry.Code == code).Key ?? (style.NumberFormatId == 0 ? "general" : null);
    }

    private IEnumerable<(string Key, string Code)> LocalNumberFormats() =>
        NumberFormats.Select(entry => entry.Key == "shortDate"
            ? (entry.Key, systemDates.ShortDate(System.Globalization.CultureInfo.CurrentCulture))
            : entry);

    private string Key(BorderPreset preset) => char.ToLowerInvariant(preset.ToString()[0]) + preset.ToString()[1..];

    private string Key(BorderStyle style) => style.ToString().ToLowerInvariant();
}
