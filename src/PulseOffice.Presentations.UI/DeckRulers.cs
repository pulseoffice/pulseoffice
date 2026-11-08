using Avalonia;
using Avalonia.VisualTree;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Rulers;

namespace PulseOffice.Presentations.UI;

public sealed record DeckRulerSources(
    SlideView Canvas,
    Func<DeckTarget?> Target,
    Func<bool> Visible,
    IRichTextFormatter Formatter,
    IParagraphRuler Paragraph);

public sealed class DeckRulers
{
    private const string InsetStart = "inset-start";
    private const string InsetEnd = "inset-end";

    private readonly DeckRulerSources _sources;

    public DeckRulers(DeckRulerSources sources)
    {
        _sources = sources;
        Horizontal = new RulerStrip(RulerOrientation.Horizontal);
        Vertical = new RulerStrip(RulerOrientation.Vertical);
        Horizontal.MarkerMoved += (_, move) => MoveHorizontal(move);
        Horizontal.MarkerRemoved += (_, marker) => RemoveTab(marker);
        Horizontal.Clicked += (_, position) => AddTab(position);
        Horizontal.BlankPressed += (_, press) => press.Handled = Guide(press, horizontal: true);
        Vertical.MarkerMoved += (_, move) => MoveVertical(move);
        Vertical.BlankPressed += (_, press) => press.Handled = Guide(press, horizontal: false);
        sources.Canvas.LayoutUpdated += (_, _) => Update();
    }

    public RulerStrip Horizontal { get; }

    public RulerStrip Vertical { get; }

    public void Update()
    {
        var visible = _sources.Visible();
        Horizontal.IsVisible = visible;
        Vertical.IsVisible = visible;
        var canvas = _sources.Canvas;
        if (!visible || canvas.Scale <= 0 || !Horizontal.IsAttachedToVisualTree() || !Vertical.IsAttachedToVisualTree()) return;

        var scale = canvas.Scale * ((double)PulseOffice.Foundation.Units.Twip.PerPoint);
        var across = canvas.TranslatePoint(canvas.Target.TopLeft, Horizontal) ?? default;
        var down = canvas.TranslatePoint(canvas.Target.TopLeft, Vertical) ?? default;
        var width = canvas.Size.Width.Value / ((double)PulseOffice.Foundation.Units.Twip.PerPoint);
        var height = canvas.Size.Height.Value / ((double)PulseOffice.Foundation.Units.Twip.PerPoint);

        if (Editing() is { } editing)
        {
            var (element, paragraph) = editing;
            var box = Box(element);
            var insets = element.Text.Frame.Insets;
            var start = box.X + insets.Left;
            var end = box.Right - insets.Right;
            var left = start + (paragraph.MarginLeft ?? paragraph.BulletLevel * 18);
            List<RulerMarker> markers =
            [
                new(InsetStart, RulerMarkerKind.MarginStart, start),
                new(InsetEnd, RulerMarkerKind.MarginEnd, end),
                .. _sources.Paragraph.Markers(new ParagraphRulerFrame(left, end, paragraph.Indent, paragraph.MarginRight, paragraph.TabStops, left)),
            ];
            Horizontal.Model = new RulerModel(RulerOrientation.Horizontal, across.X, scale, width, start, end, markers, _sources.Paragraph.Unit);
            Vertical.Model = new RulerModel(RulerOrientation.Vertical, down.Y, scale, height, box.Y + insets.Top, box.Bottom - insets.Bottom,
            [
                new(InsetStart, RulerMarkerKind.MarginStart, box.Y + insets.Top),
                new(InsetEnd, RulerMarkerKind.MarginEnd, box.Bottom - insets.Bottom),
            ], _sources.Paragraph.Unit);
            return;
        }

        var selected = _sources.Target()?.Selected() is { } chosen ? Box(chosen) : new Rect(0, 0, width, height);
        Horizontal.Model = new RulerModel(RulerOrientation.Horizontal, across.X, scale, width, selected.X, selected.Right, [], _sources.Paragraph.Unit);
        Vertical.Model = new RulerModel(RulerOrientation.Vertical, down.Y, scale, height, selected.Y, selected.Bottom, [], _sources.Paragraph.Unit);
    }

    private (TextElement Element, TextParagraph Paragraph)? Editing()
    {
        if (_sources.Target() is not { } target || !target.IsTyping() || target.TextTarget() is not { } element) return null;
        var paragraph = _sources.Formatter.ParagraphAt(element.Text, target.ReadRange()) ?? new TextParagraph(string.Empty);
        return (element, paragraph);
    }

    private Rect Box(SlideElement element) => new(
        element.Bounds.Left.Value / ((double)PulseOffice.Foundation.Units.Twip.PerPoint), element.Bounds.Top.Value / ((double)PulseOffice.Foundation.Units.Twip.PerPoint),
        element.Bounds.Width.Value / ((double)PulseOffice.Foundation.Units.Twip.PerPoint), element.Bounds.Height.Value / ((double)PulseOffice.Foundation.Units.Twip.PerPoint));

    private bool Guide(RulerPress press, bool horizontal) =>
        Editing() is null && _sources.Canvas.BeginGuide(horizontal, press.Pointer.Pointer);

    private void MoveHorizontal(RulerMarkerMove move)
    {
        if (Editing() is not { } editing || _sources.Target() is not { } target) return;
        var (element, paragraph) = editing;
        var box = Box(element);
        var insets = element.Text.Frame.Insets;
        var start = box.X + insets.Left;
        var end = box.Right - insets.Right;
        var marginLeft = paragraph.MarginLeft ?? paragraph.BulletLevel * 18;
        var left = start + marginLeft;
        var formatter = _sources.Formatter;
        switch (move.Marker.Id)
        {
            case InsetStart:
                Frame(target, insets with { Left = Math.Clamp(move.Position - box.X, 0, Math.Max(0, box.Width - insets.Right - 1)) });
                break;
            case InsetEnd:
                Frame(target, insets with { Right = Math.Clamp(box.Right - move.Position, 0, Math.Max(0, box.Width - insets.Left - 1)) });
                break;
        }
        switch (_sources.Paragraph.HandleOf(move.Marker))
        {
            case ParagraphRulerHandle.FirstLine:
                target.ApplyText((text, range) => formatter.SetIndents(text, range, marginLeft, Math.Round(move.Position - left, 2)));
                break;
            case ParagraphRulerHandle.Left:
                target.ApplyText((text, range) => formatter.SetIndents(text, range, Math.Max(0, Math.Round(move.Position - start, 2)), paragraph.Indent ?? 0));
                break;
            case ParagraphRulerHandle.Right:
                target.ApplyText((text, range) => formatter.SetRightIndent(text, range, Math.Round(Math.Max(0, end - move.Position), 2)));
                break;
            case ParagraphRulerHandle.Tab:
                Tabs(target, paragraph, tabs => _sources.Paragraph.MoveTab(tabs, move.Marker, move.Position - left));
                break;
        }
    }

    private void MoveVertical(RulerMarkerMove move)
    {
        if (Editing() is not { } editing || _sources.Target() is not { } target) return;
        var box = Box(editing.Element);
        var insets = editing.Element.Text.Frame.Insets;
        Frame(target, move.Marker.Id == InsetStart
            ? insets with { Top = Math.Clamp(move.Position - box.Y, 0, Math.Max(0, box.Height - insets.Bottom - 1)) }
            : insets with { Bottom = Math.Clamp(box.Bottom - move.Position, 0, Math.Max(0, box.Height - insets.Top - 1)) });
    }

    private void AddTab(double position)
    {
        if (Editing() is not { } editing || _sources.Target() is not { } target) return;
        var (element, paragraph) = editing;
        var offset = position - Box(element).X - element.Text.Frame.Insets.Left - (paragraph.MarginLeft ?? paragraph.BulletLevel * 18);
        if (offset <= 0) return;
        Tabs(target, paragraph, tabs => _sources.Paragraph.AddTab(tabs, offset));
    }

    private void RemoveTab(RulerMarker marker)
    {
        if (!marker.IsTab || Editing() is not { } editing || _sources.Target() is not { } target) return;
        Tabs(target, editing.Paragraph, tabs => _sources.Paragraph.RemoveTab(tabs, marker));
    }

    private void Tabs(DeckTarget target, TextParagraph paragraph, Func<IReadOnlyList<TabStop>, IReadOnlyList<TabStop>> change)
    {
        var next = change(paragraph.TabStops ?? []);
        target.ApplyText((text, range) => _sources.Formatter.SetTabStops(text, range, next));
    }

    private void Frame(DeckTarget target, TextInsets insets) =>
        target.ApplyText((text, _) => _sources.Formatter.SetFrame(text, text.Frame with { Insets = insets }));
}
