using PulseOffice.Foundation.Zoom;
using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Canvas;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Prompts;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioRibbonCommands(
    IActiveFolioContext context,
    IColorDialog colours,
    IRibbonPaletteBuilder palettes,
    IRibbonMemory memory,
    ITextCatalog texts,
    IZoomPrompt zoom,
    PulseOffice.UI.Controls.Zoom.IZoomChoice zoomChoice,
    FolioDialogCommands dialogs,
    StyleCatalog.IFolioStyleNames names) : FolioCommandSet(context)
{
    private static readonly double[] FontSizes = [8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 48, 72];

    private static readonly double[] LineSpacings = [1.0, 1.15, 1.5, 2.0, 2.5, 3.0];

    private static readonly double[] ZoomSteps = [25, 50, 75, 100, 125, 150, 200, 300, 400, 600];

    private readonly IReadOnlyList<(string Name, string Key)> _builtInStyles =
    [
        (DefaultStyleNames.Standard, "standard"),
        (DefaultStyleNames.TextBody, "textBody"),
        (DefaultStyleNames.Title, "title"),
        (DefaultStyleNames.Subtitle, "subtitle"),
        (DefaultStyleNames.HeadingLevel(1), "heading1"),
        (DefaultStyleNames.HeadingLevel(2), "heading2"),
        (DefaultStyleNames.HeadingLevel(3), "heading3"),
        (DefaultStyleNames.Quotations, "quotations"),
        (DefaultStyleNames.PreformattedText, "preformatted"),
        (DefaultStyleNames.List, "list"),
    ];

    private string? _textColour = memory.Recall("folio.format.color", "#C00000");
    private string? _highlight = memory.Recall("folio.format.highlight", "#FFFF00");
    private string? _shading = memory.Recall("folio.paragraph.shading", "#DEEAF6");

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.format.fontFamily") with
        {
            RibbonControl = new FontFamilySpec(() => Editor?.EffectiveRun().Font),
            Execute = (ctx, ct) => ctx.Argument is string family
                ? Character(new CharacterProperties { Font = family })
                : dialogs.CharacterAsync(ct),
        },
        Editing("folio.format.fontSize") with
        {
            RibbonControl = new NumberBoxSpec(() => Editor?.EffectiveRun().FontSize, FontSizes, 2, 999),
            Execute = (ctx, ct) => ctx.Argument is double size
                ? Character(new CharacterProperties { FontSize = size })
                : dialogs.CharacterAsync(ct),
        },
        Editing("folio.format.color") with
        {
            RibbonControl = new ColorSplitSpec(Palette, () => _textColour, "ribbon.colour.automatic")
            {
                MoreCommandId = "folio.format.color",
                Selected = () => Editor?.EffectiveRun().Color is { } argb ? Hex(argb) : null,
            },
            Execute = async (ctx, ct) =>
            {
                if (await Pick(ctx, _textColour, true, "command.folio.format.color.title", ct).ConfigureAwait(true) is not { } colour) return;
                _textColour = colour.Hex;
                memory.Remember("folio.format.color", colour.Hex);
                await Runs(run => run with { Color = colour.Hex is { } hex ? Argb(hex) : null, ColorRole = null }).ConfigureAwait(true);
            },
        },
        Editing("folio.format.highlight") with
        {
            RibbonControl = new ColorSplitSpec(HighlightPalette, () => _highlight, "ribbon.colour.none")
            {
                MoreCommandId = "folio.format.highlight",
                Selected = () => Editor?.EffectiveRun().Highlight is { } argb ? Hex(argb) : null,
            },
            Execute = async (ctx, ct) =>
            {
                if (await Pick(ctx, _highlight, true, "command.folio.format.highlight.title", ct).ConfigureAwait(true) is not { } colour) return;
                _highlight = colour.Hex;
                memory.Remember("folio.format.highlight", colour.Hex);
                await Runs(run => run with { Highlight = colour.Hex is { } hex ? Argb(hex) : null }).ConfigureAwait(true);
            },
        },
        Editing("folio.paragraph.shading") with
        {
            RibbonControl = new ColorSplitSpec(Palette, () => _shading, "ribbon.colour.none")
            {
                MoreCommandId = "folio.paragraph.shading",
                Selected = () => Editor?.EffectiveParagraph().Background is { } argb ? Hex(argb) : null,
            },
            Execute = async (ctx, ct) =>
            {
                if (await Pick(ctx, _shading, true, "command.folio.paragraph.shading.title", ct).ConfigureAwait(true) is not { } colour) return;
                _shading = colour.Hex;
                memory.Remember("folio.paragraph.shading", colour.Hex);
                if (Editor is { } editor)
                    editor.Apply((documents, range) => documents.MapParagraphs(editor.Document, editor.History, range, "shading",
                        paragraph => paragraph with { Background = colour.Hex is { } hex ? Argb(hex) : null }), editor.ParagraphRange());
            },
        },
        Editing("folio.paragraph.lineSpacing") with
        {
            RibbonControl = new ChoiceSpec(SpacingChoices, () => Editor is { } editor ? Key(editor.EffectiveParagraph().LineSpacing) : null)
            {
                MoreCommandId = "folio.format.paragraph",
                Width = 72,
            },
            Execute = (ctx, ct) => ctx.Argument is string key && double.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out var spacing)
                ? Paragraph(new ParagraphProperties { LineSpacing = spacing })
                : dialogs.ParagraphAsync(ct),
        },
        Editing("folio.style.paragraph") with
        {
            RibbonControl = new ChoiceSpec(StyleChoices, () => Editor?.CurrentParagraph().StyleName ?? DefaultStyleNames.Standard)
            {
                Look = RibbonChoiceLook.Gallery,
                InlineTiles = 0,
                Width = 150,
                MoreCommandId = "folio.style.paragraph",
            },
            Execute = (ctx, ct) => ctx.Argument is string name ? ApplyStyle(name) : dialogs.StyleAsync(StyleChoices(), ct),
        },
        QuickStyle("folio.style.heading1", PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(1), "Ctrl+1"),
        QuickStyle("folio.style.heading2", PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(2), "Ctrl+2"),
        QuickStyle("folio.style.heading3", PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(3), "Ctrl+3"),
        QuickStyle("folio.style.textBody", PulseOffice.Documents.Styles.DefaultStyleNames.TextBody, "Ctrl+Shift+0"),
        Viewing("folio.zoom.set") with
        {
            RibbonControl = new NumberBoxSpec(() => Context.Target is { } t ? Math.Round(t.EffectiveZoom() * 100) : null, ZoomSteps, ZoomLimits.Pages.Minimum * 100, ZoomLimits.Pages.Maximum * 100)
            {
                Suffix = "%",
                Width = 72,
            },
            Execute = async (ctx, ct) =>
            {
                if (Context.Target is not { } t) return;
                if (ctx.Argument is double given)
                {
                    if (double.IsFinite(given)) t.SetZoom(ZoomLimits.Pages.At(given / 100));
                    return;
                }

                await zoomChoice.ChooseAsync(t.ZoomSubject, zoom, ct).ConfigureAwait(true);
            },
        },
    ];

    public IReadOnlyList<RibbonChoice> StyleChoices()
    {
        if (Editor is not { } editor) return [];
        var sheet = editor.Document.Styles;
        var builtIn = _builtInStyles.Where(entry => sheet.Paragraph.Contains(entry.Name))
            .Select(entry => Choice(editor, entry.Name, texts[$"folio.paragraphStyle.{entry.Key}"]));
        var known = _builtInStyles.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
        var custom = editor.Services.Stories.Paragraphs(editor.Document)
            .Select(paragraph => paragraph.StyleName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Where(name => !known.Contains(name) && sheet.Paragraph.Find(name) is { Hidden: false })
            .Select(name => Choice(editor, name, names.Title(sheet.Paragraph.Find(name)!)));
        return [.. builtIn, .. custom];
    }

    private RibbonChoice Choice(FolioEditor editor, string name, string title)
    {
        var style = editor.Services.Styles.ResolveParagraphStyle(editor.Document.Styles, name);
        var merged = style.Character.Over(editor.Document.Styles.DefaultCharacter);
        var paragraph = style.Paragraph.Over(editor.Document.Styles.DefaultParagraph);
        return new RibbonChoice(name, title)
        {
            Sample = new RibbonSample(null, merged.Color is { } colour ? Hex(colour) : null, merged.Bold == true, merged.Italic == true)
            {
                FontFamily = merged.Font,
                FontSize = merged.FontSize,
                Underline = merged.Underline == true || merged.DoubleUnderline == true,
                Strikethrough = merged.Strikethrough == true,
                Align = paragraph.Align switch
                {
                    PulseOffice.Text.Rich.TextAlign.Center => RibbonSampleAlign.Center,
                    PulseOffice.Text.Rich.TextAlign.Right => RibbonSampleAlign.End,
                    _ => RibbonSampleAlign.Start,
                },
            },
        };
    }

    private CommandDefinition QuickStyle(string id, string style, string gesture) =>
        Editing(id) with
        {
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            CheckedWhen = _ => Editor?.CurrentParagraph().StyleName == style,
            Execute = (_, _) => ApplyStyle(style),
        };

    private Task ApplyStyle(string name)
    {
        if (Editor is { } editor && editor.Document.Styles.Paragraph.Contains(name))
            editor.Apply((documents, range) => documents.ApplyParagraphStyle(editor.Document, editor.History, range, name), editor.ParagraphRange());
        return Task.CompletedTask;
    }

    private async Task<RibbonColor?> Pick(CommandContext ctx, string? current, bool allowNone, string titleKey, CancellationToken ct) =>
        ctx.Argument as RibbonColor ?? await colours.ChooseAsync(current, allowNone, titleKey, ct).ConfigureAwait(true);

    private Task Character(CharacterProperties properties)
    {
        if (Editor is { } editor)
            editor.Apply((documents, range) => documents.SetCharacter(editor.Document, editor.History, range, properties), editor.CharacterRange());
        return Task.CompletedTask;
    }

    private Task Runs(Func<PulseOffice.Text.Rich.TextRun, PulseOffice.Text.Rich.TextRun> map)
    {
        if (Editor is { } editor)
            editor.Apply((documents, range) => documents.MapRuns(editor.Document, editor.History, range, "character-format", map), editor.CharacterRange());
        return Task.CompletedTask;
    }

    private Task Paragraph(ParagraphProperties properties)
    {
        if (Editor is { } editor)
            editor.Apply((documents, range) => documents.SetParagraph(editor.Document, editor.History, range, properties), editor.ParagraphRange());
        return Task.CompletedTask;
    }

    private RibbonPalette Palette() => palettes.Build(palettes.OfficeTheme);

    private RibbonPalette HighlightPalette() => new([],
    [
        "#FFFF00", "#00FF00", "#00FFFF", "#FF00FF", "#0000FF",
        "#FF0000", "#000080", "#008080", "#008000", "#800080",
    ]);

    private IReadOnlyList<RibbonChoice> SpacingChoices() =>
        [.. LineSpacings.Select(spacing => new RibbonChoice(Key(spacing), spacing.ToString("0.0#", CultureInfo.CurrentCulture)))];

    private string Key(double spacing) => spacing.ToString("0.0#", CultureInfo.InvariantCulture);

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();

    private uint Argb(string hex) =>
        ArgbColor.FromRgbHex(hex)?.Value ?? ArgbColor.Black.Value;
}
