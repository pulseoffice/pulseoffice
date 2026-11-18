namespace PulseOffice.Text.Rich;

public enum TextFlow
{
    Horizontal,
    Vertical,
    Vertical270,
    Stacked,
    EastAsianVertical,
    MongolianVertical,
    StackedRightToLeft,
}

public sealed record TextLayout
{
    public static TextLayout Default { get; } = new();

    public TextFlow Flow { get; init; } = TextFlow.Horizontal;

    public bool Upright { get; init; }

    public int Columns { get; init; } = 1;

    public double ColumnSpacing { get; init; }

    public bool IsPlain => Flow == TextFlow.Horizontal && !Upright && Columns <= 1;

    public bool IsVertical => Flow is TextFlow.Vertical or TextFlow.Vertical270 or TextFlow.EastAsianVertical or TextFlow.MongolianVertical;

    public bool IsStacked => Flow is TextFlow.Stacked or TextFlow.StackedRightToLeft;

    public bool LinesLeftToRight => Flow == TextFlow.MongolianVertical;

    public double FlowDegrees => Flow switch
    {
        TextFlow.Vertical or TextFlow.EastAsianVertical or TextFlow.MongolianVertical => 90,
        TextFlow.Vertical270 => 270,
        _ => 0,
    };
}
