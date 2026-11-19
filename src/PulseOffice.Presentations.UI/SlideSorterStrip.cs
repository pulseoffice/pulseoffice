using Avalonia;
using Avalonia.Controls;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class SlideSorterStrip
{
    private readonly StackPanel _rows;
    private readonly SlideFrameCache<Border> _cache = new();
    private readonly Func<Slide, Border> _build;
    private readonly DeckSectionHeaders _headers;
    private readonly SlideStripLayout _layout;
    private readonly List<Control> _children = [];
    private IReadOnlyList<Border> _frames = [];

    public SlideSorterStrip(Thickness margin, Func<Slide, Border> build, DeckSectionHeaders headers, SlideStripLayout layout)
    {
        _rows = new StackPanel { Margin = margin };
        _build = build;
        _headers = headers;
        _layout = layout;
    }

    public Panel Panel => _rows;

    public int Rebuilt => _cache.Rebuilt;

    public void Update(Presentation deck, Slide? current)
    {
        var frames = _cache.Refresh(deck, current, (slide, _) => _build(slide));
        var children = new List<Control>();
        _headers.Begin();
        foreach (var item in _layout.Items(deck))
            children.Add(item.Header is { } header ? _headers.For(header) : frames[item.SlideIndex]);
        _headers.End();
        _frames = frames;

        if (_children.Count == children.Count && _children.Zip(children).All(pair => ReferenceEquals(pair.First, pair.Second))) return;
        _children.Clear();
        _children.AddRange(children);
        foreach (var row in _rows.Children.OfType<WrapPanel>()) row.Children.Clear();
        _rows.Children.Clear();
        var slideFrames = frames.ToHashSet(ReferenceEqualityComparer.Instance);
        WrapPanel? open = null;
        foreach (var child in children)
        {
            if (child is Border frame && slideFrames.Contains(frame))
            {
                if (open is null)
                {
                    open = new WrapPanel();
                    _rows.Children.Add(open);
                }
                open.Children.Add(frame);
                continue;
            }
            open = null;
            _rows.Children.Add(child);
        }
    }

    public IReadOnlyList<Control> Frames => _frames;
}
