using PulseOffice.Foundation.Zoom;
using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckRibbonCommands(
    IActiveDeckContext context,
    IRichTextFormatter formatter,
    DeckTextDialogCommands dialogs,
    IColorDialog colours,
    IShapeStyling styling,
    IRibbonPaletteBuilder palettes,
    ITextCatalog texts,
    IRibbonMemory memory,
    PulseOffice.UI.Controls.Prompts.IZoomPrompt zoom,
    PulseOffice.UI.Controls.Zoom.IZoomChoice zoomChoice,
    ObjectDialogs.IDeckObjectEditing objectEditing,
    IDeckTableCellEditing cells) : DeckCommandSet(context)
{
    private static readonly double[] FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 44, 48, 54, 60, 72, 96];

    private static readonly double[] LineSpacings = [1.0, 1.15, 1.5, 2.0, 2.5, 3.0];

    private static readonly double[] ZoomSteps = [25, 50, 75, 100, 125, 150, 200, 300, 400];

    private string? _textColour = memory.Recall("deck.format.color", "#C00000");
    private string? _highlight = memory.Recall("deck.format.highlight", "#FFFF00");
    private string? _fillColour = memory.Recall("deck.object.fill", "#4472C4");
    private string? _lineColour = memory.Recall("deck.object.lineColor", "#1F3864");

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Cells(Text("deck.format.fontFamily") with
        {
            RibbonControl = new FontFamilySpec(() => Character()?.Font ?? CellLook()?.FontFamily ?? ThemeFont()),
            Execute = (ctx, ct) => ctx.Argument is string family
                ? ApplyText((text, range) => formatter.SetFont(text, range, family))
                : dialogs.CharacterAsync(ct),
        }, (ctx, look) => ctx.Argument is string { Length: > 0 } family ? look with { FontFamily = family } : look),

        Cells(Text("deck.format.fontSize") with
        {
            RibbonControl = new NumberBoxSpec(() => Character()?.Size ?? CellLook()?.FontSize, FontSizes, 6, 300),
            Execute = (ctx, ct) => ctx.Argument is double size
                ? ApplyText((text, range) => formatter.SetFontSize(text, range, size))
                : dialogs.CharacterAsync(ct),
        }, (ctx, look) => ctx.Argument is double size && double.IsFinite(size) ? look with { FontSize = Math.Clamp(size, 6, 300) } : look),

        Text("deck.format.color") with
        {
            EnabledWhen = _ => Context.Target is { } t && (t.HasTextTarget() || cells.Available(t)),
            RibbonControl = new ColorSplitSpec(Palette, () => _textColour, "ribbon.colour.automatic")
            {
                MoreCommandId = "deck.format.color",
                Selected = () => Character()?.Color is { } argb ? Hex(argb) : CellLook()?.TextHex,
            },
            Execute = async (ctx, ct) =>
            {
                var colour = ctx.Argument as RibbonColor
                    ?? await colours.ChooseAsync(_textColour, true, "command.deck.format.color.title", ct).ConfigureAwait(true);

                if (colour is null) return;

                _textColour = colour.Hex;
                memory.Remember("deck.format.color", colour.Hex);
                if (cells.Available(Context.Target))
                {
                    cells.Apply(Context.Target, look => look with { TextHex = colour.Hex is { } chosen && Argb(chosen) is { } argb ? new ArgbColor(argb).ToHashedHex() : null });
                    return;
                }
                await ApplyText((text, range) => colour.Hex is { } hex && Argb(hex) is { } argb
                    ? formatter.SetColor(text, range, argb, null)
                    : formatter.SetColor(text, range, null, ColorRole.Text));
            },
        },

        Text("deck.format.highlight") with
        {
            RibbonControl = new ColorSplitSpec(HighlightPalette, () => _highlight, "ribbon.colour.none")
            {
                MoreCommandId = "deck.format.highlight",
                Selected = () => Character()?.Highlight is { } argb ? Hex(argb) : null,
            },
            Execute = async (ctx, ct) =>
            {
                var colour = ctx.Argument as RibbonColor
                    ?? await colours.ChooseAsync(_highlight, true, "command.deck.format.highlight.title", ct).ConfigureAwait(true);

                if (colour is null) return;

                _highlight = colour.Hex;
                memory.Remember("deck.format.highlight", colour.Hex);
                await ApplyText((text, range) => formatter.SetHighlight(text, range, colour.Hex is { } hex ? Argb(hex) : null));
            },
        },

        Text("deck.paragraph.lineSpacing") with
        {
            RibbonControl = new ChoiceSpec(SpacingChoices, () => Paragraph()?.LineSpacing is { } spacing ? Key(spacing) : null)
            {
                MoreCommandId = "deck.format.paragraph",
                Width = 72,
            },
            Execute = (ctx, ct) => ctx.Argument is string key && double.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out var spacing)
                ? ApplyText((text, range) => formatter.SetLineSpacing(text, range, spacing))
                : dialogs.ParagraphAsync(ct),
        },

        Shape("deck.object.fill") with
        {
            RibbonControl = new ColorSplitSpec(Palette, () => _fillColour, "ribbon.colour.none")
            {
                MoreCommandId = "deck.object.fill",
                Selected = () => SelectedStyle()?.Fill is { Kind: ShapeFillKind.Solid } fill ? fill.ColorHex : null,
            },
            Execute = async (ctx, ct) =>
            {
                var colour = ctx.Argument as RibbonColor
                    ?? await colours.ChooseAsync(_fillColour, true, "command.deck.object.fill.title", ct).ConfigureAwait(true);

                if (colour is null) return;

                _fillColour = colour.Hex;
                memory.Remember("deck.object.fill", colour.Hex);
                Restyle("deck.object.fill", style => style with
                {
                    Fill = colour.Hex is { } hex
                        ? style.Fill with { Kind = ShapeFillKind.Solid, ColorHex = hex, Opacity = 1 }
                        : ShapeFill.None,
                });
            },
        },

        Shape("deck.object.lineColor") with
        {
            RibbonControl = new ColorSplitSpec(Palette, () => _lineColour, "ribbon.colour.none")
            {
                MoreCommandId = "deck.object.lineColor",
                Selected = () => SelectedStyle()?.Line is { } line && line.Dash != ShapeStrokeStyle.None ? line.ColorHex : null,
            },
            Execute = async (ctx, ct) =>
            {
                var colour = ctx.Argument as RibbonColor
                    ?? await colours.ChooseAsync(_lineColour, true, "command.deck.object.lineColor.title", ct).ConfigureAwait(true);

                if (colour is null) return;

                _lineColour = colour.Hex;
                memory.Remember("deck.object.lineColor", colour.Hex);
                Restyle("deck.object.lineColor", style => style with
                {
                    Line = colour.Hex is { } hex
                        ? style.Line with
                        {
                            ColorHex = hex,
                            Dash = style.Line.Dash == ShapeStrokeStyle.None ? ShapeStrokeStyle.Solid : style.Line.Dash,
                            Opacity = 1,
                        }
                        : ShapeLine.None,
                });
            },
        },

        Shape("deck.object.quickStyle") with
        {
            RibbonControl = new ChoiceSpec(QuickStyles, () => null)
            {
                Look = RibbonChoiceLook.Gallery,
                InlineTiles = 0,
                Width = 120,
                MoreCommandId = "deck.object.style",
            },
            Execute = (ctx, ct) =>
            {
                if (ctx.Argument is not string key || QuickStyle(key) is not { } style) return objectEditing.EditStyleAsync(ct);

                Restyle("deck.object.quickStyle", current => current with
                {
                    Fill = style.Fill,
                    Line = style.Line,
                    Text = current.Text with { ColorHex = style.Text.ColorHex, AutomaticColor = style.Text.AutomaticColor },
                });
                return Task.CompletedTask;
            },
        },

        Prototype("deck.zoom.set") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            RibbonControl = new NumberBoxSpec(() => Context.Target is { } t ? Math.Round(t.EffectiveZoom() * 100) : null, ZoomSteps, 10, 400)
            {
                Suffix = "%",
                Width = 72,
            },
            Execute = async (ctx, ct) =>
            {
                if (Context.Target is not { } t) return;

                if (ctx.Argument is double given)
                {
                    if (double.IsFinite(given)) t.SetZoom(ZoomLimits.Slides.At(given / 100));
                    return;
                }

                await zoomChoice.ChooseAsync(t.ZoomSubject, zoom, ct).ConfigureAwait(true);
            },
        },
    ];

    private CommandDefinition Text(string id) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.HasTextTarget(),
        };

    private CommandDefinition Cells(CommandDefinition command, Func<CommandContext, PulseOffice.Document.Embedding.TableCellLook, PulseOffice.Document.Embedding.TableCellLook> change)
    {
        var enabled = command.EnabledWhen;
        return command with
        {
            EnabledWhen = ctx => (enabled?.Invoke(ctx) ?? true) || cells.Available(Context.Target),
            Execute = (ctx, ct) =>
            {
                if (!cells.Available(Context.Target)) return command.Execute(ctx, ct);
                if (ctx.Argument is not null) cells.Apply(Context.Target, look => change(ctx, look));
                return Task.CompletedTask;
            },
        };
    }

    private PulseOffice.Document.Embedding.TableCellLook? CellLook() => cells.Look(Context.Target);

    private CommandDefinition Shape(string id) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Styleable().Count > 0,
        };

    private IReadOnlyList<ObjectElement> Styleable() =>
        Context.Target is { } t
            ? [.. t.SelectedMany().OfType<ObjectElement>().Where(element => styling.Editable(element.Content) is not null)]
            : [];

    private ShapeStyle? SelectedStyle() =>
        Styleable().FirstOrDefault() is { } element ? styling.Editable(element.Content)?.Style : null;

    private RibbonPalette HighlightPalette() => new([],
    [
        "#FFFF00", "#00FF00", "#00FFFF", "#FF00FF", "#0000FF",
        "#FF0000", "#000080", "#008080", "#008000", "#800080",
    ]);

    private void Restyle(string key, Func<ShapeStyle, ShapeStyle> change)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        var updated = Styleable()
            .Select(element => styling.Restyle(element.Content, change) is { } content ? element with { Content = content } : null)
            .OfType<SlideElement>()
            .ToList();

        if (updated.Count > 0) t.Actions.ReplaceElements(slide, updated, $"command.{key}.title");
    }

    private Task ApplyText(Func<RichText, (int Start, int End)?, RichText> transform)
    {
        Context.Target?.ApplyText(transform);
        return Task.CompletedTask;
    }

    private CharacterFormat? Character() =>
        Context.Target is { } t && t.ReadText() is { } text ? formatter.ReadCharacter(text, t.ReadRange()) : null;

    private string? ThemeFont()
    {
        if (Context.Target is not { } t || t.ReadText() is not { } text || t.CurrentSlide() is not { } slide) return null;
        var fonts = t.Deck.ThemeOf(slide).Fonts;
        var major = text.Paragraphs.Count > 0
            ? text.Paragraphs.All(paragraph => paragraph.MajorFont)
            : text.Role is PlaceholderRole.Title;
        return major ? fonts.Major : fonts.Minor;
    }

    private ParagraphFormat? Paragraph() =>
        Context.Target is { } t && t.ReadText() is { } text ? formatter.ReadParagraph(text, t.ReadRange()) : null;

    private ColorScheme Scheme() =>
        Context.Target is { } t && t.CurrentSlide() is { } slide ? t.Deck.ThemeOf(slide).Colors : new ColorScheme();

    private RibbonPalette Palette()
    {
        var scheme = Scheme();

        return palettes.Build(
        [
            Hex(scheme.Background), Hex(scheme.Text), Hex(scheme.Background2), Hex(scheme.Text2),
            Hex(scheme.Accent1), Hex(scheme.Accent2), Hex(scheme.Accent3),
            Hex(scheme.Accent4), Hex(scheme.Accent5), Hex(scheme.Accent6),
        ]);
    }

    private IReadOnlyList<RibbonChoice> SpacingChoices() =>
        [.. LineSpacings.Select(spacing => new RibbonChoice(Key(spacing), spacing.ToString("0.0#", CultureInfo.CurrentCulture)))];

    private IReadOnlyList<RibbonChoice> QuickStyles()
    {
        var scheme = Scheme();
        uint[] accents = [scheme.Accent1, scheme.Accent2, scheme.Accent3, scheme.Accent4, scheme.Accent5, scheme.Accent6];

        return
        [
            .. accents.Select((accent, index) => new RibbonChoice($"solid{index + 1}", texts.Format("ribbon.shapeStyle.solid", index + 1))
            {
                Sample = new RibbonSample(Hex(accent), "#FFFFFF", BorderHex: palettes.Shade(Hex(accent), -0.3)),
            }),
            .. accents.Select((accent, index) => new RibbonChoice($"light{index + 1}", texts.Format("ribbon.shapeStyle.light", index + 1))
            {
                Sample = new RibbonSample(palettes.Shade(Hex(accent), 0.8), Hex(accent), BorderHex: Hex(accent)),
            }),
            .. accents.Select((accent, index) => new RibbonChoice($"outline{index + 1}", texts.Format("ribbon.shapeStyle.outline", index + 1))
            {
                Sample = new RibbonSample(null, Hex(accent), BorderHex: Hex(accent)),
            }),
        ];
    }

    private ShapeStyle? QuickStyle(string key) =>
        QuickStyles().FirstOrDefault(choice => choice.Key == key)?.Sample is { } sample
            ? new ShapeStyle
            {
                Fill = sample.FillHex is { } fill ? new ShapeFill { ColorHex = fill } : ShapeFill.None,
                Line = new ShapeLine { ColorHex = sample.BorderHex ?? "#000000", Width = Foundation.Units.Twip.FromPoints(key.StartsWith("outline", StringComparison.Ordinal) ? 2 : 1) },
                Text = new ShapeTextStyle { ColorHex = sample.TextHex ?? "#000000" },
            }
            : null;

    private string Key(double spacing) => spacing.ToString("0.0#", CultureInfo.InvariantCulture);

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();

    private uint? Argb(string hex) =>
        ArgbColor.FromRgbHex(hex)?.Value;
}
