using PulseOffice.Foundation.Modules;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.TextFormat;

namespace PulseOffice.Presentations.UI;

public sealed record DeckTextDialogs(
    ICharacterFormatDialog Characters,
    IParagraphFormatDialog Paragraphs,
    IListFormatDialog Lists,
    IDeckTextFormats Formats);

public interface IDeckTextFormats
{
    CharacterFormatState Character(TextRun run);

    TextRun Apply(TextRun run, CharacterFormatState before, CharacterFormatState after);

    ParagraphFormatState Paragraph(TextParagraph paragraph);

    TextParagraph Apply(TextParagraph paragraph, ParagraphFormatState before, ParagraphFormatState after);

    ListLevelFormat List(TextParagraph paragraph);

    TextParagraph Apply(TextParagraph paragraph, ListLevelFormat level, int bulletLevel);

    ListFormatRequest ListRequest(TextParagraph first);
}

public sealed class DeckTextFormats(IParagraphListFormats lists) : IDeckTextFormats
{
    public const int LevelCount = ParagraphListFormats.LevelCount;

    public DeckTextFormats() : this(new ParagraphListFormats())
    {
    }

    private const double DefaultIndentStep = 18;

    public CharacterFormatState Character(TextRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new CharacterFormatState
        {
            Font = run.Font ?? string.Empty,
            Size = run.FontSize ?? 18,
            Bold = run.Bold,
            Italic = run.Italic,
            Underline = run.DoubleUnderline ? UnderlineKind.Double : run.Underline ? UnderlineKind.Single : UnderlineKind.None,
            Strikethrough = run.Strikethrough,
            Overline = run.Overline,
            SmallCaps = run.SmallCaps,
            Shadow = run.Shadow,
            Outline = run.Outline,
            Color = run.Color,
            Highlight = run.Highlight,
            Baseline = run.Baseline,
        };
    }

    public TextRun Apply(TextRun run, CharacterFormatState before, CharacterFormatState after)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var changed = run;
        if (after.Font != before.Font && after.Font.Length > 0) changed = changed with { Font = after.Font };
        if (after.Size != before.Size) changed = changed with { FontSize = after.Size };
        if (after.Bold != before.Bold) changed = changed with { Bold = after.Bold };
        if (after.Italic != before.Italic) changed = changed with { Italic = after.Italic };
        if (after.Underline != before.Underline)
            changed = changed with { Underline = after.Underline == UnderlineKind.Single, DoubleUnderline = after.Underline == UnderlineKind.Double };
        if (after.Strikethrough != before.Strikethrough) changed = changed with { Strikethrough = after.Strikethrough };
        if (after.Overline != before.Overline) changed = changed with { Overline = after.Overline };
        if (after.SmallCaps != before.SmallCaps) changed = changed with { SmallCaps = after.SmallCaps };
        if (after.Shadow != before.Shadow) changed = changed with { Shadow = after.Shadow };
        if (after.Outline != before.Outline) changed = changed with { Outline = after.Outline };
        if (after.Color != before.Color) changed = changed with { Color = after.Color, ColorRole = after.Color is null ? changed.ColorRole : null };
        if (after.Highlight != before.Highlight) changed = changed with { Highlight = after.Highlight };
        if (after.Baseline != before.Baseline) changed = changed with { Baseline = after.Baseline };
        return changed;
    }

    public ParagraphFormatState Paragraph(TextParagraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        return new ParagraphFormatState
        {
            Align = paragraph.Align,
            Left = paragraph.MarginLeft ?? paragraph.BulletLevel * DefaultIndentStep,
            Right = paragraph.MarginRight ?? 0,
            FirstLine = paragraph.Indent ?? 0,
            Before = paragraph.SpaceBefore,
            After = paragraph.SpaceAfter,
            LineMode = LineHeightMode.Proportional,
            LineValue = Math.Round(paragraph.LineSpacing * 100, 2),
        };
    }

    public TextParagraph Apply(TextParagraph paragraph, ParagraphFormatState before, ParagraphFormatState after)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var changed = paragraph;
        if (after.Align != before.Align) changed = changed with { Align = after.Align };
        if (Different(after.Left, before.Left)) changed = changed with { MarginLeft = Math.Max(0, after.Left) };
        if (Different(after.Right, before.Right)) changed = changed with { MarginRight = Math.Max(0, after.Right) };
        if (Different(after.FirstLine, before.FirstLine)) changed = changed with { Indent = after.FirstLine };
        if (Different(after.Before, before.Before)) changed = changed with { SpaceBefore = Math.Max(0, after.Before) };
        if (Different(after.After, before.After)) changed = changed with { SpaceAfter = Math.Max(0, after.After) };
        if (Different(after.LineValue, before.LineValue)) changed = changed with { LineSpacing = Math.Max(0.1, after.LineValue / 100) };
        return changed;
    }

    public ListLevelFormat List(TextParagraph paragraph) => lists.List(paragraph);

    public TextParagraph Apply(TextParagraph paragraph, ListLevelFormat level, int bulletLevel) => lists.Apply(paragraph, level, bulletLevel);

    public ListFormatRequest ListRequest(TextParagraph first) => lists.Request(first);

    private bool Different(double first, double second) => Math.Abs(first - second) > 0.001;
}

public sealed class DeckTextDialogCommands(IActiveDeckContext context, IRichTextFormatter formatter, DeckTextDialogs dialogs) : DeckCommandSet(context)
{
    private const CharacterFeatures Characters =
        CharacterFeatures.Font | CharacterFeatures.Size | CharacterFeatures.Style | CharacterFeatures.Underline | CharacterFeatures.DoubleUnderline
        | CharacterFeatures.Strikethrough | CharacterFeatures.Overline | CharacterFeatures.SmallCaps | CharacterFeatures.Shadow | CharacterFeatures.Outline
        | CharacterFeatures.Color | CharacterFeatures.Highlight | CharacterFeatures.Position;

    private const ParagraphFeatures Paragraphs =
        ParagraphFeatures.Align | ParagraphFeatures.Indents | ParagraphFeatures.RightIndent | ParagraphFeatures.Spacing | ParagraphFeatures.LineSpacing;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Dialog("deck.format.character", CharacterAsync),
        Dialog("deck.format.paragraph", ParagraphAsync),
        Dialog("deck.format.bulletsNumbering", BulletsAsync),
    ];

    public async Task CharacterAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target || CurrentText() is not { } text) return;
        var range = target.ReadRange();
        var before = dialogs.Formats.Character(formatter.RunsIn(text, range).FirstOrDefault() ?? new TextRun(string.Empty));
        var sample = string.Concat(formatter.RunsIn(text, range).Select(run => run.Text)).Split('\n', 2)[0].Trim();
        var request = new CharacterFormatRequest(before, Characters, sample.Length > 40 ? sample[..40] : sample);
        if (await dialogs.Characters.AskAsync(request, "command.deck.format.character.title", ct).ConfigureAwait(true) is not { } after || after == before) return;
        target.ApplyText((slideText, applied) => formatter.MapRuns(slideText, applied, run => dialogs.Formats.Apply(run, before, after)));
    }

    public async Task ParagraphAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target || CurrentText() is not { } text) return;
        var range = target.ReadRange();
        if (formatter.ParagraphsIn(text, range).FirstOrDefault() is not { } first) return;
        var before = dialogs.Formats.Paragraph(first);
        if (await dialogs.Paragraphs.AskAsync(new ParagraphFormatRequest(before, Paragraphs), "command.deck.format.paragraph.title", ct).ConfigureAwait(true) is not { } after
            || after == before) return;
        target.ApplyText((slideText, applied) => formatter.MapParagraphs(slideText, applied, paragraph => dialogs.Formats.Apply(paragraph, before, after)));
    }

    private async Task BulletsAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target || CurrentText() is not { } text) return;
        var range = target.ReadRange();
        if (formatter.ParagraphsIn(text, range).FirstOrDefault() is not { } first) return;
        var request = dialogs.Formats.ListRequest(first);
        if (await dialogs.Lists.AskAsync(request, "command.deck.format.bulletsNumbering.title", ct).ConfigureAwait(true) is not { } answer) return;
        var chosen = answer.Levels[answer.Level];
        target.ApplyText((slideText, applied) => formatter.MapParagraphs(slideText, applied, paragraph => dialogs.Formats.Apply(paragraph, chosen, answer.Level)));
    }

    private CommandDefinition Dialog(string id, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.HasTextTarget(),
            Execute = (_, ct) => run(ct),
        };

    private RichText? CurrentText() => Context.Target?.ReadText();
}
