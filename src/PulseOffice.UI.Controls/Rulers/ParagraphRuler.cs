using System.Globalization;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Rulers;

public enum ParagraphRulerHandle { None, FirstLine, Left, Right, Tab }

public sealed record ParagraphRulerFrame(double Left, double Right, double? Indent, double? MarginRight, IReadOnlyList<TabStop>? Tabs, double TabOrigin);

public interface IParagraphRuler
{
    double Unit { get; }

    IReadOnlyList<RulerMarker> Markers(ParagraphRulerFrame frame);

    ParagraphRulerHandle HandleOf(RulerMarker marker);

    IReadOnlyList<TabStop> MoveTab(IReadOnlyList<TabStop> tabs, RulerMarker marker, double offset);

    IReadOnlyList<TabStop> AddTab(IReadOnlyList<TabStop> tabs, double offset);

    IReadOnlyList<TabStop> RemoveTab(IReadOnlyList<TabStop> tabs, RulerMarker marker);
}

public sealed class ParagraphRuler : IParagraphRuler
{
    private const string FirstLine = "first-line";
    private const string Left = "left";
    private const string Right = "right";
    private const string TabPrefix = "tab-";

    public double Unit { get; } = RegionInfo.CurrentRegion.IsMetric ? 72 / 2.54 : 72;

    public IReadOnlyList<RulerMarker> Markers(ParagraphRulerFrame frame) =>
    [
        new(FirstLine, RulerMarkerKind.FirstLine, frame.Left + (frame.Indent ?? 0)),
        new(Left, RulerMarkerKind.Left, frame.Left),
        new(Right, RulerMarkerKind.Right, frame.Right - (frame.MarginRight ?? 0)),
        .. (frame.Tabs ?? []).Select((tab, index) => new RulerMarker(TabPrefix + index, Kind(tab.Alignment), frame.TabOrigin + tab.Position)),
    ];

    public ParagraphRulerHandle HandleOf(RulerMarker marker) => marker.Id switch
    {
        FirstLine => ParagraphRulerHandle.FirstLine,
        Left => ParagraphRulerHandle.Left,
        Right => ParagraphRulerHandle.Right,
        _ when marker.IsTab && marker.Id.StartsWith(TabPrefix, StringComparison.Ordinal) => ParagraphRulerHandle.Tab,
        _ => ParagraphRulerHandle.None,
    };

    public IReadOnlyList<TabStop> MoveTab(IReadOnlyList<TabStop> tabs, RulerMarker marker, double offset) =>
        Sorted(tabs.Select((tab, index) => TabPrefix + index == marker.Id ? tab with { Position = Math.Max(0, Math.Round(offset, 2)) } : tab));

    public IReadOnlyList<TabStop> AddTab(IReadOnlyList<TabStop> tabs, double offset) =>
        offset <= 0 ? tabs : Sorted([.. tabs, new TabStop(Math.Round(offset, 2))]);

    public IReadOnlyList<TabStop> RemoveTab(IReadOnlyList<TabStop> tabs, RulerMarker marker) =>
        marker.IsTab ? [.. tabs.Where((_, index) => TabPrefix + index != marker.Id)] : tabs;

    private IReadOnlyList<TabStop> Sorted(IEnumerable<TabStop> tabs) => [.. tabs.OrderBy(tab => tab.Position)];

    private RulerMarkerKind Kind(TabAlignment alignment) => alignment switch
    {
        TabAlignment.Center => RulerMarkerKind.TabCenter,
        TabAlignment.Right => RulerMarkerKind.TabRight,
        TabAlignment.Decimal => RulerMarkerKind.TabDecimal,
        _ => RulerMarkerKind.TabLeft,
    };
}
