using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.Text.Rich;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFormatCommands(
    IActiveDeckContext context, IRichTextFormatter formatter,
    PulseOffice.Text.Scripts.IWidthConversion width, PulseOffice.Text.Scripts.IKanaConversion kana,
    PulseOffice.Text.ITextCases cases, IDeckTableCellEditing cells) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Char("deck.format.bold", CharAttribute.Bold) with { DefaultGesture = "Ctrl+B" },
        Char("deck.format.italic", CharAttribute.Italic) with { DefaultGesture = "Ctrl+I" },
        Char("deck.format.underline", CharAttribute.Underline) with { DefaultGesture = "Ctrl+U" },
        Char("deck.format.strikethrough", CharAttribute.Strikethrough),
        Char("deck.format.superscript", CharAttribute.Superscript),
        Char("deck.format.subscript", CharAttribute.Subscript),

        CharAdv("deck.format.doubleUnderline", CharAttribute.DoubleUnderline),
        CharAdv("deck.format.overline", CharAttribute.Overline),
        CharAdv("deck.format.shadow", CharAttribute.Shadow),
        CharAdv("deck.format.outline", CharAttribute.Outline),
        CharAdv("deck.format.smallCaps", CharAttribute.SmallCaps),

        Text("deck.format.grow", (t, r) => formatter.Scale(t, r, 1.1)),
        Text("deck.format.shrink", (t, r) => formatter.Scale(t, r, 1 / 1.1)),

        Inked(Text("deck.format.colorDefault", (t, r) => formatter.SetColor(t, r, null, ColorRole.Text)), null),
        Text("deck.format.colorAccent", (t, r) => formatter.SetColor(t, r, null, ColorRole.Accent1)),
        Inked(Text("deck.format.colorRed", (t, r) => formatter.SetColor(t, r, 0xFFC0392B, null)), "#C0392B"),
        Inked(Text("deck.format.colorGreen", (t, r) => formatter.SetColor(t, r, 0xFF16A34A, null)), "#16A34A"),
        Inked(Text("deck.format.colorBlue", (t, r) => formatter.SetColor(t, r, 0xFF2563EB, null)), "#2563EB"),

        CellAligned(Aligned(Text("deck.paragraph.alignLeft", (t, r) => formatter.SetAlign(t, r, TextAlign.Left)), TextAlign.Left), CellHorizontalAlign.Left),
        CellAligned(Aligned(Text("deck.paragraph.alignCenter", (t, r) => formatter.SetAlign(t, r, TextAlign.Center)), TextAlign.Center), CellHorizontalAlign.Center),
        CellAligned(Aligned(Text("deck.paragraph.alignRight", (t, r) => formatter.SetAlign(t, r, TextAlign.Right)), TextAlign.Right), CellHorizontalAlign.Right),
        Aligned(Text("deck.paragraph.alignJustify", (t, r) => formatter.SetAlign(t, r, TextAlign.Justify)), TextAlign.Justify),
        Text("deck.paragraph.bullets", (t, r) => formatter.ToggleBullets(t, r)) with { CheckedWhen = _ => Paragraph()?.Bullet == true },
        Text("deck.paragraph.numbering", (t, r) => formatter.ToggleNumbering(t, r)),
        Text("deck.paragraph.spaceIncrease", (t, r) => formatter.AdjustSpacing(t, r, 3), CommandSurface.Advanced),
        Text("deck.paragraph.spaceDecrease", (t, r) => formatter.AdjustSpacing(t, r, -3), CommandSurface.Advanced),
        Text("deck.format.clear", (t, r) => formatter.ClearDirectFormatting(t, r)),
        Text("deck.paragraph.alignStart", (t, r) => formatter.SetAlign(t, r, TextAlign.Left), CommandSurface.Advanced),
        Text("deck.paragraph.alignEnd", (t, r) => formatter.SetAlign(t, r, TextAlign.Right), CommandSurface.Advanced),
        Directed(Text("deck.paragraph.leftToRight", (t, r) => formatter.SetDirection(t, r, PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight), CommandSurface.Advanced), PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight),
        Directed(Text("deck.paragraph.rightToLeft", (t, r) => formatter.SetDirection(t, r, PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft), CommandSurface.Advanced), PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft),
        Text("deck.paragraph.moveDown", (t, r) => formatter.MoveParagraphs(t, r, 1), CommandSurface.Advanced) with { EnabledWhen = _ => Typing() },
        Text("deck.paragraph.moveUp", (t, r) => formatter.MoveParagraphs(t, r, -1), CommandSurface.Advanced) with { EnabledWhen = _ => Typing() },
        Text("deck.paragraph.indent", (t, r) => formatter.Indent(t, r, 1)),
        Text("deck.paragraph.outdent", (t, r) => formatter.Indent(t, r, -1)),

        Text("deck.paragraph.lineSpacing1", (t, r) => formatter.SetLineSpacing(t, r, 1.0), CommandSurface.Advanced),
        Text("deck.paragraph.lineSpacing15", (t, r) => formatter.SetLineSpacing(t, r, 1.5), CommandSurface.Advanced),
        Text("deck.paragraph.lineSpacing2", (t, r) => formatter.SetLineSpacing(t, r, 2.0), CommandSurface.Advanced),

        Text("deck.text.upper", (t, r) => formatter.Transform(t, r, cases[PulseOffice.Text.TextCaseKind.Upper].Apply), CommandSurface.Advanced),
        Text("deck.text.lower", (t, r) => formatter.Transform(t, r, cases[PulseOffice.Text.TextCaseKind.Lower].Apply), CommandSurface.Advanced),
        Text("deck.text.sentence", (t, r) => formatter.Transform(t, r, cases[PulseOffice.Text.TextCaseKind.Sentence].Apply), CommandSurface.Advanced),
        Text("deck.text.title", (t, r) => formatter.Transform(t, r, cases[PulseOffice.Text.TextCaseKind.Title].Apply), CommandSurface.Advanced),
        Text("deck.text.cycle", (t, r) => formatter.CycleCase(t, r), CommandSurface.Advanced),
        Text("deck.text.toggle", (t, r) => formatter.Transform(t, r, cases[PulseOffice.Text.TextCaseKind.Toggle].Apply), CommandSurface.Advanced),
        Text("deck.text.fullWidth", (t, r) => formatter.Transform(t, r, width.ToFullWidth), CommandSurface.Advanced),
        Text("deck.text.halfWidth", (t, r) => formatter.Transform(t, r, width.ToHalfWidth), CommandSurface.Advanced),
        Text("deck.text.hiragana", (t, r) => formatter.Transform(t, r, kana.ToHiragana), CommandSurface.Advanced),
        Text("deck.text.katakana", (t, r) => formatter.Transform(t, r, kana.ToKatakana), CommandSurface.Advanced),
    ];

    private CommandDefinition Char(string id, CharAttribute attribute)
    {
        var command = Text(id, (t, r) => formatter.ToggleChar(t, r, attribute)) with
        {
            CheckedWhen = _ => Character() is { } format
                ? attribute switch
                {
                    CharAttribute.Bold => format.Bold == true,
                    CharAttribute.Italic => format.Italic == true,
                    CharAttribute.Underline => format.Underline == true,
                    CharAttribute.Strikethrough => format.Strikethrough == true,
                    _ => false,
                }
                : cells.Look(Context.Target) is { } look && CellFlag(look, attribute) == true,
        };
        return CellFlag(TableCellLook.Plain, attribute) is null
            ? command
            : InTableCells(command, cells, target => cells.Toggle(target, look => CellFlag(look, attribute) == true, (look, on) => attribute switch
            {
                CharAttribute.Bold => look with { Bold = on },
                CharAttribute.Italic => look with { Italic = on },
                CharAttribute.Underline => look with { Underline = on },
                _ => look with { Strikethrough = on },
            }));
    }

    private bool? CellFlag(TableCellLook look, CharAttribute attribute) => attribute switch
    {
        CharAttribute.Bold => look.Bold,
        CharAttribute.Italic => look.Italic,
        CharAttribute.Underline => look.Underline,
        CharAttribute.Strikethrough => look.Strikethrough,
        _ => null,
    };

    private CommandDefinition Inked(CommandDefinition command, string? hex) =>
        InTableCells(command, cells, target => cells.Apply(target, look => look with { TextHex = hex }));

    private CommandDefinition CellAligned(CommandDefinition command, CellHorizontalAlign align)
    {
        var aligned = command.CheckedWhen;
        return InTableCells(command, cells, target => cells.Apply(target, look => look with { Horizontal = align })) with
        {
            CheckedWhen = ctx => cells.Available(Context.Target)
                ? cells.Look(Context.Target)?.Horizontal == align
                : aligned?.Invoke(ctx) == true,
        };
    }

    private CharacterFormat? Character() =>
        Context.Target is { } t && t.ReadText() is { } text ? formatter.ReadCharacter(text, t.ReadRange()) : null;

    private ParagraphFormat? Paragraph() =>
        Context.Target is { } t && t.ReadText() is { } text ? formatter.ReadParagraph(text, t.ReadRange()) : null;

    private CommandDefinition Aligned(CommandDefinition command, TextAlign align) =>
        command with { CheckedWhen = _ => Paragraph()?.Align == align };

    private CommandDefinition Directed(CommandDefinition command, PulseOffice.Text.Rich.Bidi.TextDirection direction) =>
        command with { CheckedWhen = _ => Paragraph()?.Direction == direction };

    private CommandDefinition CharAdv(string id, CharAttribute attribute) =>
        Text(id, (t, r) => formatter.ToggleChar(t, r, attribute), CommandSurface.Advanced);

    private bool Typing() => Context.Target is { } t && t.HasTextTarget() && t.IsTyping();

    private CommandDefinition Text(string id, Func<RichText, (int Start, int End)?, RichText> transform) =>
        Text(id, transform, CommandSurface.Common);

    private CommandDefinition Text(string id, Func<RichText, (int Start, int End)?, RichText> transform, CommandSurface surface) =>
        Prototype(id) with
        {
            Surface = surface,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.HasTextTarget(),
            Execute = (_, _) =>
            {
                Context.Target?.ApplyText(transform);
                return Task.CompletedTask;
            },
        };
}
