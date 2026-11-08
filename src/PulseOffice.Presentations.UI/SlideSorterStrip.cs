using Avalonia;
using Avalonia.Controls;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class SlideSorterStrip
{
    private readonly WrapPanel _wrap;
    private readonly SlideFrameCache<Border> _cache = new();
    private readonly Func<Slide, Border> _build;

    public SlideSorterStrip(Thickness margin, Func<Slide, Border> build)
    {
        _wrap = new WrapPanel { Margin = margin };
        _build = build;
    }

    public Panel Panel => _wrap;

    public int Rebuilt => _cache.Rebuilt;

    public void Update(Presentation deck, Slide? current)
    {
        var frames = _cache.Refresh(deck, current, (slide, _) => _build(slide));

        if (_wrap.Children.Count == frames.Count && _wrap.Children.Zip(frames).All(pair => ReferenceEquals(pair.First, pair.Second))) return;
        _wrap.Children.Clear();
        foreach (var frame in frames) _wrap.Children.Add(frame);
    }

    public int DropTarget(Point position)
    {
        for (var i = 0; i < _wrap.Children.Count; i++)
            if (_wrap.Children[i].Bounds.Contains(position))
                return i;
        return -1;
    }
}
