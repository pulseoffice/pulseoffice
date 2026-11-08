using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Rtf;

public sealed record RtfParagraphBorders
{
    public RtfCellEdge? Top { get; init; }

    public RtfCellEdge? Left { get; init; }

    public RtfCellEdge? Bottom { get; init; }

    public RtfCellEdge? Right { get; init; }

    public int TopSpace { get; init; }

    public int LeftSpace { get; init; }

    public int BottomSpace { get; init; }

    public int RightSpace { get; init; }

    public IReadOnlyList<BorderSide> Current { get; init; } = [];

    public bool HasLines => Top is not null || Left is not null || Bottom is not null || Right is not null;

    public bool HasSpace => TopSpace > 0 || LeftSpace > 0 || BottomSpace > 0 || RightSpace > 0;

    public RtfCellEdge? Edge(BorderSide side) => side switch
    {
        BorderSide.Top => Top,
        BorderSide.Left => Left,
        BorderSide.Bottom => Bottom,
        _ => Right,
    };

    public int Space(BorderSide side) => side switch
    {
        BorderSide.Top => TopSpace,
        BorderSide.Left => LeftSpace,
        BorderSide.Bottom => BottomSpace,
        _ => RightSpace,
    };

    public RtfParagraphBorders WithEdge(BorderSide side, RtfCellEdge? edge) => side switch
    {
        BorderSide.Top => this with { Top = edge },
        BorderSide.Left => this with { Left = edge },
        BorderSide.Bottom => this with { Bottom = edge },
        _ => this with { Right = edge },
    };

    public RtfParagraphBorders WithSpace(BorderSide side, int space) => side switch
    {
        BorderSide.Top => this with { TopSpace = space },
        BorderSide.Left => this with { LeftSpace = space },
        BorderSide.Bottom => this with { BottomSpace = space },
        _ => this with { RightSpace = space },
    };
}

public sealed class RtfBorderLineStyles
{
    public BorderLineStyle? Style(string word) => word switch
    {
        "brdrs" or "brdrth" or "brdrsh" or "brdrhair" or "brdroutset" or "brdrinset" or "brdremboss" or "brdrengrave" => BorderLineStyle.Solid,
        "brdrdash" or "brdrdashsm" or "brdrdashd" or "brdrdashdd" or "brdrdashdotstr" => BorderLineStyle.Dashed,
        "brdrdot" => BorderLineStyle.Dotted,
        "brdrdb" or "brdrtriple" or "brdrtnthsg" or "brdrthtnsg" or "brdrtnthtnsg" => BorderLineStyle.Double,
        _ => null,
    };

    public string Word(BorderLineStyle style) => style switch
    {
        BorderLineStyle.Dashed => @"\brdrdash",
        BorderLineStyle.Dotted => @"\brdrdot",
        BorderLineStyle.Double => @"\brdrdb",
        _ => @"\brdrs",
    };
}

public sealed class RtfParagraphBorderWords(RtfBorderLineStyles styles)
{
    private const int MaximumBorderTwips = 255;
    private const int MaximumSpaceTwips = 2880;
    private const int DefaultWidthTwips = 10;

    public bool Opens(string word) => word is "brdrt" or "brdrb" or "brdrl" or "brdrr" or "box";

    public RtfParagraphBorders Open(RtfParagraphBorders? borders, string word)
    {
        IReadOnlyList<BorderSide> sides = word switch
        {
            "brdrt" => [BorderSide.Top],
            "brdrb" => [BorderSide.Bottom],
            "brdrl" => [BorderSide.Left],
            "brdrr" => [BorderSide.Right],
            _ => [BorderSide.Top, BorderSide.Left, BorderSide.Bottom, BorderSide.Right],
        };
        var opened = borders ?? new RtfParagraphBorders();
        foreach (var side in sides) opened = opened.WithEdge(side, new RtfCellEdge(BorderLineStyle.Solid, DefaultWidthTwips, 0));
        return opened with { Current = sides };
    }

    public RtfParagraphBorders? Apply(RtfParagraphBorders borders, string word, int value)
    {
        if (borders.Current.Count == 0) return null;
        switch (word)
        {
            case "brdrnone" or "brdrnil":
                return Each(borders, (current, side) => current.WithEdge(side, null));
            case "brdrw":
                return Edges(borders, edge => edge with { WidthTwips = Math.Clamp(value, 1, MaximumBorderTwips) });
            case "brdrcf":
                return Edges(borders, edge => edge with { ColorIndex = Math.Max(0, value) });
            case "brsp":
                return Each(borders, (current, side) => current.WithSpace(side, Math.Clamp(value, 0, MaximumSpaceTwips)));
            default:
                return styles.Style(word) is { } style ? Edges(borders, edge => edge with { Style = style }) : null;
        }
    }

    private RtfParagraphBorders Edges(RtfParagraphBorders borders, Func<RtfCellEdge, RtfCellEdge> change) =>
        Each(borders, (current, side) => current.Edge(side) is { } edge ? current.WithEdge(side, change(edge)) : current);

    private RtfParagraphBorders Each(RtfParagraphBorders borders, Func<RtfParagraphBorders, BorderSide, RtfParagraphBorders> change)
    {
        var result = borders;
        foreach (var side in borders.Current) result = change(result, side);
        return result;
    }
}
