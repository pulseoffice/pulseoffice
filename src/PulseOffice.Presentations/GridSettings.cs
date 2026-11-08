using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public readonly record struct SlideGuide(bool Horizontal, int Position);

public sealed class GridSettings
{
    private readonly List<SlideGuide> _guides = [];

    public bool ShowGrid { get; set; }

    public bool SnapToGrid { get; set; } = true;

    public Twip Spacing { get; set; } = Twip.FromCentimeters(0.5);

    public bool ShowRulers { get; set; }

    public bool ShowGuides { get; set; } = true;

    public bool SnapToGuides { get; set; } = true;

    public bool GridInFront { get; set; } = true;

    public bool GuidesInFront { get; set; } = true;

    public bool HelplinesWhileMoving { get; set; }

    public bool SnapToObjectFrames { get; set; }

    public bool SnapToObjectPoints { get; set; }

    public bool SnapToPageMargins { get; set; } = true;

    public IReadOnlyList<SlideGuide> Guides => _guides;

    public bool IsStandard
    {
        get
        {
            var standard = new GridSettings();
            return _guides.Count == 0 && ShowGrid == standard.ShowGrid && SnapToGrid == standard.SnapToGrid && Spacing == standard.Spacing
                && ShowRulers == standard.ShowRulers && ShowGuides == standard.ShowGuides && SnapToGuides == standard.SnapToGuides
                && GridInFront == standard.GridInFront && GuidesInFront == standard.GuidesInFront && HelplinesWhileMoving == standard.HelplinesWhileMoving
                && SnapToObjectFrames == standard.SnapToObjectFrames && SnapToObjectPoints == standard.SnapToObjectPoints
                && SnapToPageMargins == standard.SnapToPageMargins;
        }
    }

    public void CopyTo(GridSettings target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.ShowGrid = ShowGrid;
        target.SnapToGrid = SnapToGrid;
        target.Spacing = Spacing;
        target.ShowRulers = ShowRulers;
        target.ShowGuides = ShowGuides;
        target.SnapToGuides = SnapToGuides;
        target.GridInFront = GridInFront;
        target.GuidesInFront = GuidesInFront;
        target.HelplinesWhileMoving = HelplinesWhileMoving;
        target.SnapToObjectFrames = SnapToObjectFrames;
        target.SnapToObjectPoints = SnapToObjectPoints;
        target.SnapToPageMargins = SnapToPageMargins;
        target._guides.Clear();
        target._guides.AddRange(_guides);
    }

    public void AddGuide(SlideGuide guide)
    {
        if (!_guides.Contains(guide)) _guides.Add(guide);
    }

    public void MoveGuide(int index, int position)
    {
        if (index >= 0 && index < _guides.Count)
            _guides[index] = _guides[index] with { Position = position };
    }

    public void RemoveGuide(int index)
    {
        if (index >= 0 && index < _guides.Count) _guides.RemoveAt(index);
    }

    public void ClearGuides() => _guides.Clear();
}
