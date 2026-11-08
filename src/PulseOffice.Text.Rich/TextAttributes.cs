namespace PulseOffice.Text.Rich;

[Flags]
public enum RunOverrides
{
    None = 0,
    Bold = 1,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Strikethrough = 1 << 3,
    DoubleUnderline = 1 << 4,
    Overline = 1 << 5,
    Shadow = 1 << 6,
    Outline = 1 << 7,
    SmallCaps = 1 << 8,
    Baseline = 1 << 9,
}

[Flags]
public enum ParagraphOverrides
{
    None = 0,
    Align = 1,
    LineSpacing = 1 << 1,
    SpaceBefore = 1 << 2,
    SpaceAfter = 1 << 3,
}

public enum TabAlignment
{
    Left,
    Center,
    Right,
    Decimal,
}

public enum TabLeader
{
    None,
    Dots,
    Dashes,
    Underline,
}

public sealed record TabStop(double Position)
{
    public TabAlignment Alignment { get; init; }

    public TabLeader Leader { get; init; }

    public char DecimalCharacter { get; init; } = '.';
}
