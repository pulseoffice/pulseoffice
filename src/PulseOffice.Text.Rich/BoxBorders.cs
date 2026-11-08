namespace PulseOffice.Text.Rich;

public enum BorderLineStyle
{
    Solid,
    Dotted,
    Dashed,
    Double,
}

public sealed record BorderLine(double Width, uint Color)
{
    public BorderLineStyle Style { get; init; }
}

public enum BorderSide
{
    Top,
    Bottom,
    Left,
    Right,
}

public sealed record BoxBorders
{
    public BorderLine? Top { get; init; }

    public BorderLine? Bottom { get; init; }

    public BorderLine? Left { get; init; }

    public BorderLine? Right { get; init; }

    public double PaddingTop { get; init; }

    public double PaddingBottom { get; init; }

    public double PaddingLeft { get; init; }

    public double PaddingRight { get; init; }

    public bool HasLines => Top is not null || Bottom is not null || Left is not null || Right is not null;

    public bool IsUniform => Top == Bottom && Top == Left && Top == Right
        && PaddingTop == PaddingBottom && PaddingTop == PaddingLeft && PaddingTop == PaddingRight;

    public BorderLine? Line(BorderSide side) => side switch
    {
        BorderSide.Top => Top,
        BorderSide.Bottom => Bottom,
        BorderSide.Left => Left,
        _ => Right,
    };

    public double Padding(BorderSide side) => side switch
    {
        BorderSide.Top => PaddingTop,
        BorderSide.Bottom => PaddingBottom,
        BorderSide.Left => PaddingLeft,
        _ => PaddingRight,
    };

    public double Inset(BorderSide side) => (Line(side)?.Width ?? 0) + Padding(side);

    public BoxBorders With(BorderSide side, BorderLine? line, double padding) => side switch
    {
        BorderSide.Top => this with { Top = line, PaddingTop = padding },
        BorderSide.Bottom => this with { Bottom = line, PaddingBottom = padding },
        BorderSide.Left => this with { Left = line, PaddingLeft = padding },
        _ => this with { Right = line, PaddingRight = padding },
    };

    public static BoxBorders Uniform(BorderLine? line, double padding) => new()
    {
        Top = line,
        Bottom = line,
        Left = line,
        Right = line,
        PaddingTop = padding,
        PaddingBottom = padding,
        PaddingLeft = padding,
        PaddingRight = padding,
    };
}
