using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioCharacterCommands(IActiveFolioContext context, ITextCases cases) : FolioCommandSet(context)
{
    private readonly IReadOnlyList<CharacterEffect> _effects =
    [
        new("folio.format.strikethrough", null, run => run.Strikethrough, on => new CharacterProperties { Strikethrough = on }),
        new("folio.format.doubleUnderline", null, run => run.DoubleUnderline, on => new CharacterProperties { DoubleUnderline = on }),
        new("folio.format.overline", null, run => run.Overline, on => new CharacterProperties { Overline = on }),
        new("folio.format.smallCaps", null, run => run.SmallCaps, on => new CharacterProperties { SmallCaps = on }),
        new("folio.format.shadow", null, run => run.Shadow, on => new CharacterProperties { Shadow = on }),
        new("folio.format.outline", null, run => run.Outline, on => new CharacterProperties { Outline = on }),
        new("folio.format.superscript", null, run => run.Baseline == TextBaseline.Superscript,
            on => new CharacterProperties { Baseline = on ? TextBaseline.Superscript : TextBaseline.Normal }),
        new("folio.format.subscript", null, run => run.Baseline == TextBaseline.Subscript,
            on => new CharacterProperties { Baseline = on ? TextBaseline.Subscript : TextBaseline.Normal }),
    ];

    private readonly IReadOnlyList<(string Id, TextCaseKind Kind)> _cases =
    [
        ("folio.case.upper", TextCaseKind.Upper),
        ("folio.case.lower", TextCaseKind.Lower),
        ("folio.case.sentence", TextCaseKind.Sentence),
        ("folio.case.title", TextCaseKind.Title),
        ("folio.case.toggle", TextCaseKind.Toggle),
        ("folio.case.halfWidth", TextCaseKind.HalfWidth),
        ("folio.case.fullWidth", TextCaseKind.FullWidth),
        ("folio.case.hiragana", TextCaseKind.Hiragana),
        ("folio.case.katakana", TextCaseKind.Katakana),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. _effects.Select(Effect),
        Action(Editing("folio.format.grow"), editor => Resize(editor, 2)),
        Action(Editing("folio.format.shrink"), editor => Resize(editor, -2)),
        Action(Editing("folio.format.clear"),
            editor => editor.Apply((documents, range) => documents.ClearDirectFormatting(editor.Document, editor.History, range), editor.CharacterRange())),
        .. _cases.Select(Case),
        Action(Editing("folio.case.cycle") with { DefaultGesture = "Shift+F3" }, editor =>
        {
            var range = editor.CharacterRange();
            var sample = editor.HasSelection ? editor.SelectedText() : editor.TextOf(range.Start.ParagraphId) is { } text ? text[Math.Min(range.Start.Offset, text.Length)..Math.Min(range.End.Offset, text.Length)] : string.Empty;
            var next = cases.NextInCycle(sample);
            editor.Apply((documents, target) => documents.TransformText(editor.Document, editor.History, target, "case", next.Apply), range);
        }),
    ];

    private CommandDefinition Effect(CharacterEffect effect) =>
        Action(Editing(effect.Id) with
        {
            DefaultGesture = effect.Gesture,
            CheckedWhen = _ => Editor is { } editor && effect.Read(editor.EffectiveRun()),
        }, editor =>
        {
            var on = !effect.Read(editor.EffectiveRun());
            editor.Apply((documents, range) => documents.SetCharacter(editor.Document, editor.History, range, effect.Write(on)), editor.CharacterRange());
        });

    private CommandDefinition Case((string Id, TextCaseKind Kind) entry) =>
        Action(Editing(entry.Id), editor =>
            editor.Apply((documents, range) => documents.TransformText(editor.Document, editor.History, range, "case", cases[entry.Kind].Apply), editor.CharacterRange()));

    private void Resize(FolioEditor editor, double step)
    {
        var size = Math.Clamp((editor.EffectiveRun().FontSize ?? 12) + step, 2, 999);
        editor.Apply((documents, range) => documents.SetCharacter(editor.Document, editor.History, range, new CharacterProperties { FontSize = size }), editor.CharacterRange());
    }

    private sealed record CharacterEffect(string Id, string? Gesture, Func<TextRun, bool> Read, Func<bool, CharacterProperties> Write);
}
