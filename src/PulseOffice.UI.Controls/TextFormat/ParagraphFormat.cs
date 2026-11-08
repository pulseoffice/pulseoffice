using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

[Flags]
public enum ParagraphFeatures
{
    None = 0,
    Align = 1,
    Indents = 1 << 1,
    RightIndent = 1 << 2,
    Spacing = 1 << 3,
    LineSpacing = 1 << 4,
    LineModes = 1 << 5,
    Contextual = 1 << 6,
    All = (1 << 7) - 1,
}

public sealed record ParagraphFormatState
{
    public TextAlign Align { get; init; } = TextAlign.Left;

    public double Left { get; init; }

    public double Right { get; init; }

    public double FirstLine { get; init; }

    public double Before { get; init; }

    public double After { get; init; }

    public LineHeightMode LineMode { get; init; } = LineHeightMode.Proportional;

    public double LineValue { get; init; } = 100;

    public bool Contextual { get; init; }
}

public sealed record ParagraphFormatRequest(ParagraphFormatState Current, ParagraphFeatures Features)
{
    public bool Offers(ParagraphFeatures feature) => (Features & feature) == feature;
}
