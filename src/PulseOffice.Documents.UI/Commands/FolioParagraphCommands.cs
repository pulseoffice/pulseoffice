using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Rich.Bidi;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioParagraphCommands(IActiveFolioContext context) : FolioCommandSet(context)
{
    private const double IndentStep = 36;
    private const double SpacingStep = 6;
    private const uint BorderColour = 0xFF000000;

    private readonly AlignMirroring _mirroring = new();

    private readonly IReadOnlyList<(string Id, TextAlign Align, string Gesture)> _aligns =
    [
        ("folio.paragraph.alignLeft", TextAlign.Left, "Ctrl+L"),
        ("folio.paragraph.alignCenter", TextAlign.Center, "Ctrl+E"),
        ("folio.paragraph.alignRight", TextAlign.Right, "Ctrl+R"),
        ("folio.paragraph.justify", TextAlign.Justify, "Ctrl+J"),
    ];

    private readonly IReadOnlyList<(string Id, TextDirection Direction)> _directions =
    [
        ("folio.paragraph.leftToRight", TextDirection.LeftToRight),
        ("folio.paragraph.rightToLeft", TextDirection.RightToLeft),
    ];

    private readonly IReadOnlyList<(string Id, string ListStyle)> _lists =
    [
        ("folio.paragraph.bullets", DefaultStyleNames.List1),
        ("folio.paragraph.numbering", DefaultStyleNames.Numbering123),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. _aligns.Select(Align),
        .. _directions.Select(Direction),
        .. _lists.Select(List),
        Action(Editing("folio.paragraph.indentMore"), editor => Indent(editor, IndentStep)),
        Action(Editing("folio.paragraph.indentLess") with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && ((editor.EffectiveParagraph().MarginLeft ?? 0) > 0 || editor.CurrentParagraph().ListStyleName is not null),
        }, editor => Indent(editor, -IndentStep)),
        Action(Editing("folio.paragraph.spacingMore"), editor => Space(editor, SpacingStep)),
        Action(Editing("folio.paragraph.spacingLess") with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.EffectiveParagraph() is { } p && p.SpaceBefore + p.SpaceAfter > 0,
        }, editor => Space(editor, -SpacingStep)),
        Action(Editing("folio.paragraph.border") with
        {
            CheckedWhen = _ => Editor is { } editor && Bordered(editor),
        }, editor =>
        {
            var on = !Bordered(editor);
            Paragraph(editor, new ParagraphProperties { Borders = on ? BoxBorders.Uniform(new BorderLine(0.5, BorderColour), 4) : new BoxBorders() });
        }),
        Action(Editing("folio.paragraph.keepWithNext") with
        {
            CheckedWhen = _ => Editor?.EffectiveParagraph().KeepWithNext == true,
        }, editor => Paragraph(editor, new ParagraphProperties { KeepWithNext = editor.EffectiveParagraph().KeepWithNext != true })),
        Action(Editing("folio.paragraph.pageBreakBefore") with
        {
            CheckedWhen = _ => Editor?.EffectiveParagraph().PageBreakBefore == true,
        }, editor => Paragraph(editor, new ParagraphProperties { PageBreakBefore = editor.EffectiveParagraph().PageBreakBefore != true })),
    ];

    private CommandDefinition Align((string Id, TextAlign Align, string Gesture) entry) =>
        Action(Editing(entry.Id) with
        {
            DefaultGesture = entry.Gesture,
            CheckedWhen = _ => Editor is { } editor && editor.EffectiveParagraph() is var effective && effective.Align == Physical(effective, entry.Align),
        }, editor => Paragraph(editor, new ParagraphProperties { Align = Physical(editor.EffectiveParagraph(), entry.Align) }));

    private TextAlign Physical(TextParagraph effective, TextAlign align) => _mirroring.Physical(align, effective.Direction);

    private CommandDefinition Direction((string Id, TextDirection Direction) entry) =>
        Action(Editing(entry.Id) with
        {
            CheckedWhen = _ => Editor is { } editor && (editor.EffectiveParagraph().Direction ?? TextDirection.LeftToRight) == entry.Direction,
        }, editor => Paragraph(editor, new ParagraphProperties { Direction = entry.Direction }));

    private CommandDefinition List((string Id, string ListStyle) entry) =>
        Action(Editing(entry.Id) with
        {
            CheckedWhen = _ => Editor?.CurrentParagraph().ListStyleName == entry.ListStyle,
        }, editor =>
        {
            var current = editor.CurrentParagraph();
            var next = current.ListStyleName == entry.ListStyle ? null : entry.ListStyle;
            editor.Apply((documents, range) => documents.SetList(editor.Document, editor.History, range, next, current.BulletLevel), editor.ParagraphRange());
        });

    private void Indent(FolioEditor editor, double step)
    {
        var current = editor.CurrentParagraph();
        if (current.ListStyleName is { } list)
        {
            var level = Math.Clamp(current.BulletLevel + Math.Sign(step), 0, ListStyle.LevelCount - 1);
            editor.Apply((documents, range) => documents.SetList(editor.Document, editor.History, range, list, level), editor.ParagraphRange());
            return;
        }
        var margin = Math.Max(0, (editor.EffectiveParagraph().MarginLeft ?? 0) + step);
        Paragraph(editor, new ParagraphProperties { MarginLeft = margin });
    }

    private void Space(FolioEditor editor, double step)
    {
        var effective = editor.EffectiveParagraph();
        Paragraph(editor, new ParagraphProperties
        {
            SpaceBefore = Math.Max(0, effective.SpaceBefore + step),
            SpaceAfter = Math.Max(0, effective.SpaceAfter + step),
        });
    }

    private bool Bordered(FolioEditor editor) => editor.EffectiveParagraph().Borders?.HasLines == true;

    private void Paragraph(FolioEditor editor, ParagraphProperties properties) =>
        editor.Apply((documents, range) => documents.SetParagraph(editor.Document, editor.History, range, properties), editor.ParagraphRange());
}
