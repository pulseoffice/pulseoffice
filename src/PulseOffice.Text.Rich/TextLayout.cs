namespace PulseOffice.Text.Rich;

public enum TextFlow
{
    Horizontal,
    Vertical,
    Vertical270,
    Stacked,
}

public sealed record TextLayout
{
    public static TextLayout Default { get; } = new();

    public TextFlow Flow { get; init; } = TextFlow.Horizontal;

    public bool Upright { get; init; }

    public int Columns { get; init; } = 1;

    public double ColumnSpacing { get; init; }

    public bool IsPlain => Flow == TextFlow.Horizontal && !Upright && Columns <= 1;

    public double FlowDegrees => Flow switch
    {
        TextFlow.Vertical => 90,
        TextFlow.Vertical270 => 270,
        _ => 0,
    };
}
