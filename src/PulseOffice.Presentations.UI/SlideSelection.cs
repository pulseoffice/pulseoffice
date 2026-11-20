using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public enum SlidePick { Only, Toggle, Extend }

public sealed class SlideSelection
{
    private readonly HashSet<Slide> _slides = new(ReferenceEqualityComparer.Instance);
    private Slide? _anchor;

    public void Pick(Presentation deck, Slide slide, SlidePick pick)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        switch (pick)
        {
            case SlidePick.Toggle:
                if (_slides.Count == 0 && _anchor is { } previous) _slides.Add(previous);
                if (!_slides.Remove(slide)) _slides.Add(slide);
                _anchor = slide;
                break;
            case SlidePick.Extend when _anchor is { } anchor && deck.IndexOf(anchor) is var from and >= 0:
                var to = deck.IndexOf(slide);
                _slides.Clear();
                for (var index = Math.Min(from, to); index <= Math.Max(from, to); index++) _slides.Add(deck.Slides[index]);
                break;
            default:
                _slides.Clear();
                _slides.Add(slide);
                _anchor = slide;
                break;
        }
    }

    public void ExtendTo(Presentation deck, Slide current, Slide target)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_anchor is null || !_slides.Contains(current)) Collapse(current);
        Pick(deck, target, SlidePick.Extend);
    }

    public Slide Lead(Presentation deck, Slide picked) =>
        _slides.Contains(picked) ? picked : deck.Slides.FirstOrDefault(_slides.Contains) ?? picked;

    public SlidePick PickFor(Avalonia.Input.KeyModifiers modifiers) =>
        modifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift) ? SlidePick.Extend
        : (modifiers & (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Meta)) != 0 ? SlidePick.Toggle
        : SlidePick.Only;

    public void Collapse(Slide? current)
    {
        _slides.Clear();
        _anchor = current;
        if (current is not null) _slides.Add(current);
    }

    public bool Marks(Slide slide, Slide? current) =>
        current is not null && _slides.Contains(current) && _slides.Contains(slide);

    public IReadOnlyList<Slide> Ordered(Presentation? deck, Slide? current)
    {
        if (current is null) return [];
        if (deck is null || !_slides.Contains(current)) return [current];
        return [.. deck.Slides.Where(_slides.Contains)];
    }
}
