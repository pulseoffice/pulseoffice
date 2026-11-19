using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;

namespace PulseOffice.Presentations.UI;

public enum SlideDropKind
{
    None,
    Select,
    Move,
    IntoSection,
}

public sealed record SlideDrop(SlideDropKind Kind, int To = -1, EmptySection? Section = null);

public sealed class SlideDragSession(DeckSectionHeaders headers, Presentation deck, DeckActions actions, bool horizontal)
{
    private const double IndicatorThickness = 3;
    private Control? _marked;
    private Point _origin;
    private Size _slop;

    public int From { get; private set; } = -1;

    public bool Dragging { get; private set; }

    public void Begin(Slide slide, Point origin, Size slop)
    {
        From = deck.IndexOf(slide);
        _origin = origin;
        _slop = slop;
        Dragging = false;
    }

    public bool Arm(Point position)
    {
        if (From < 0) return false;
        if (!Dragging
            && (Math.Abs(position.X - _origin.X) > _slop.Width / 2 || Math.Abs(position.Y - _origin.Y) > _slop.Height / 2))
            Dragging = true;
        return Dragging;
    }

    public SlideDrop Resolve(EmptySection? section, int hit) =>
        From < 0 || From >= deck.Slides.Count ? new SlideDrop(SlideDropKind.None)
        : !Dragging ? new SlideDrop(SlideDropKind.Select)
        : section is not null ? new SlideDrop(SlideDropKind.IntoSection, Section: section)
        : hit < 0 || hit == From ? new SlideDrop(SlideDropKind.Select)
        : new SlideDrop(SlideDropKind.Move, hit);

    public void Track(Visual root, Point position, IReadOnlyList<Control> frames)
    {
        if (!Arm(position)) return;
        var drop = Resolve(headers.EmptyAt(root, position), Hit(root, position, frames));
        var target = drop.Kind switch
        {
            SlideDropKind.IntoSection => headers.ControlOf(drop.Section!),
            SlideDropKind.Move => frames[drop.To],
            _ => null,
        };
        Mark(target, drop);
    }

    public bool Finish(Visual root, Point position, IReadOnlyList<Control> frames)
    {
        Arm(position);
        var drop = Resolve(headers.EmptyAt(root, position), Hit(root, position, frames));
        var slide = drop.Kind is SlideDropKind.None ? null : deck.Slides[From];
        Mark(null, drop);
        From = -1;
        Dragging = false;
        switch (drop.Kind)
        {
            case SlideDropKind.IntoSection:
                actions.MoveSlideIntoSection(slide!, drop.Section!);
                return true;
            case SlideDropKind.Move:
                actions.MoveSlide(slide!, drop.To);
                return true;
            default:
                return false;
        }
    }

    public void Cancel()
    {
        Mark(null, new SlideDrop(SlideDropKind.None));
        From = -1;
        Dragging = false;
    }

    public Size PlatformSlop(Avalonia.Input.PointerType pointer) =>
        Application.Current?.PlatformSettings?.GetTapSize(pointer) ?? new Size(4, 4);

    private static int Hit(Visual root, Point position, IReadOnlyList<Control> frames)
    {
        for (var i = 0; i < frames.Count; i++)
            if (frames[i].TranslatePoint(default, root) is { } origin && new Rect(origin, frames[i].Bounds.Size).Contains(position))
                return i;
        return -1;
    }

    private void Mark(Control? target, SlideDrop drop)
    {
        if (ReferenceEquals(target, _marked) && target is null) return;
        if (_marked is not null) AdornerLayer.SetAdorner(_marked, null);
        _marked = target;
        if (target is null) return;
        AdornerLayer.SetAdorner(target, new Border
        {
            IsHitTestVisible = false,
            BorderThickness = Edge(drop),
        }.Themed(Border.BorderBrushProperty, PulseOffice.UI.Theming.AccentService.AccentKey));
    }

    private Thickness Edge(SlideDrop drop)
    {
        if (drop.Kind is SlideDropKind.IntoSection) return new Thickness(IndicatorThickness);
        var before = drop.To < From;
        return horizontal
            ? new Thickness(before ? IndicatorThickness : 0, 0, before ? 0 : IndicatorThickness, 0)
            : new Thickness(0, before ? IndicatorThickness : 0, 0, before ? 0 : IndicatorThickness);
    }
}
