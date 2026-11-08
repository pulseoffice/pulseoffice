using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Rtf;

public sealed record RtfCellEdge(BorderLineStyle Style, int WidthTwips, int ColorIndex);

public sealed record RtfCellDefinition(int Right, bool MergeFirst, bool MergeNext, bool VerticalFirst, bool VerticalNext, int? Shading, int VerticalAlign)
{
    public RtfCellEdge? TopEdge { get; init; }

    public RtfCellEdge? LeftEdge { get; init; }

    public RtfCellEdge? BottomEdge { get; init; }

    public RtfCellEdge? RightEdge { get; init; }

    public int? PaddingTop { get; init; }

    public int? PaddingLeft { get; init; }

    public int? PaddingBottom { get; init; }

    public int? PaddingRight { get; init; }

    public int Gap { get; init; }

    public bool HasBox => TopEdge is not null || LeftEdge is not null || BottomEdge is not null || RightEdge is not null
        || PaddingTop is not null || PaddingLeft is not null || PaddingBottom is not null || PaddingRight is not null;
}

internal sealed class RtfPendingCell
{
    private const int MaximumBorderTwips = 255;
    private const int MaximumPaddingTwips = 2880;

    private readonly Dictionary<BorderSide, RtfCellEdge?> _edges = [];
    private readonly Dictionary<BorderSide, int> _padding = [];
    private readonly Dictionary<BorderSide, int> _paddingUnits = [];
    private readonly RtfBorderLineStyles _styles = new();
    private BorderSide? _side;

    public bool MergeFirst { get; private set; }

    public bool MergeNext { get; private set; }

    public bool VerticalFirst { get; private set; }

    public bool VerticalNext { get; private set; }

    public int? Shading { get; private set; }

    public int VerticalAlign { get; private set; }

    public bool Apply(string word, int value)
    {
        switch (word)
        {
            case "clmgf": MergeFirst = true; return true;
            case "clmrg": MergeNext = true; return true;
            case "clvmgf": VerticalFirst = true; return true;
            case "clvmrg": VerticalNext = true; return true;
            case "clcbpat": Shading = value; return true;
            case "clvertalc": VerticalAlign = 1; return true;
            case "clvertalb": VerticalAlign = 2; return true;
            case "clvertalt": VerticalAlign = 0; return true;
            case "clbrdrt": return Side(BorderSide.Top);
            case "clbrdrl": return Side(BorderSide.Left);
            case "clbrdrb": return Side(BorderSide.Bottom);
            case "clbrdrr": return Side(BorderSide.Right);
            case "clpadl": _padding[BorderSide.Top] = Padding(value); return true;
            case "clpadt": _padding[BorderSide.Left] = Padding(value); return true;
            case "clpadb": _padding[BorderSide.Bottom] = Padding(value); return true;
            case "clpadr": _padding[BorderSide.Right] = Padding(value); return true;
            case "clpadfl": _paddingUnits[BorderSide.Top] = value; return true;
            case "clpadft": _paddingUnits[BorderSide.Left] = value; return true;
            case "clpadfb": _paddingUnits[BorderSide.Bottom] = value; return true;
            case "clpadfr": _paddingUnits[BorderSide.Right] = value; return true;
            case "brdrnone" or "brdrnil" when _side is { } none: _edges[none] = null; return true;
            case "brdrw" when _side is { } sized: return Edge(sized, edge => edge with { WidthTwips = Math.Clamp(value, 1, MaximumBorderTwips) });
            case "brdrcf" when _side is { } coloured: return Edge(coloured, edge => edge with { ColorIndex = Math.Max(0, value) });
            default:
                return _side is { } styled && _styles.Style(word) is { } style && Edge(styled, edge => edge with { Style = style });
        }
    }

    public RtfCellDefinition Build(int right, int gap) =>
        new(right, MergeFirst, MergeNext, VerticalFirst, VerticalNext, Shading, VerticalAlign)
        {
            TopEdge = _edges.GetValueOrDefault(BorderSide.Top),
            LeftEdge = _edges.GetValueOrDefault(BorderSide.Left),
            BottomEdge = _edges.GetValueOrDefault(BorderSide.Bottom),
            RightEdge = _edges.GetValueOrDefault(BorderSide.Right),
            PaddingTop = PaddingOf(BorderSide.Top),
            PaddingLeft = PaddingOf(BorderSide.Left),
            PaddingBottom = PaddingOf(BorderSide.Bottom),
            PaddingRight = PaddingOf(BorderSide.Right),
            Gap = gap,
        };

    private int? PaddingOf(BorderSide side) =>
        _padding.TryGetValue(side, out var twips) && _paddingUnits.GetValueOrDefault(side, 3) == 3 ? twips : null;

    private int Padding(int value) => Math.Clamp(value, 0, MaximumPaddingTwips);

    private bool Side(BorderSide side)
    {
        _side = side;
        _edges[side] = new RtfCellEdge(BorderLineStyle.Solid, 10, 0);
        return true;
    }

    private bool Edge(BorderSide side, Func<RtfCellEdge, RtfCellEdge> change)
    {
        if (_edges.GetValueOrDefault(side) is { } edge) _edges[side] = change(edge);
        return true;
    }
}
