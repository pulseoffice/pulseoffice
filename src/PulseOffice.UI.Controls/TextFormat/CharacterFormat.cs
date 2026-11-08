using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public enum UnderlineKind
{
    None,
    Single,
    Double,
}

[Flags]
public enum CharacterFeatures
{
    None = 0,
    Font = 1,
    Size = 1 << 1,
    Style = 1 << 2,
    Underline = 1 << 3,
    DoubleUnderline = 1 << 4,
    Strikethrough = 1 << 5,
    Overline = 1 << 6,
    SmallCaps = 1 << 7,
    Shadow = 1 << 8,
    Outline = 1 << 9,
    Hidden = 1 << 10,
    Color = 1 << 11,
    Highlight = 1 << 12,
    Position = 1 << 13,
    Spacing = 1 << 14,
    Scale = 1 << 15,
    Kerning = 1 << 16,
    Case = 1 << 17,
    Relief = 1 << 18,
    Language = 1 << 19,
    Basic = Font | Size | Style | Underline | Strikethrough | Color,
    All = (1 << 20) - 1,
}

public sealed record CharacterFormatState
{
    public string Font { get; init; } = string.Empty;

    public double Size { get; init; } = 12;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public UnderlineKind Underline { get; init; }

    public bool Strikethrough { get; init; }

    public bool Overline { get; init; }

    public bool SmallCaps { get; init; }

    public bool Shadow { get; init; }

    public bool Outline { get; init; }

    public bool Hidden { get; init; }

    public uint? Color { get; init; }

    public uint? Highlight { get; init; }

    public TextBaseline Baseline { get; init; }

    public double LetterSpacing { get; init; }

    public double Scale { get; init; } = 100;

    public bool Kerning { get; init; } = true;

    public TextCase Case { get; init; }

    public FontRelief Relief { get; init; }

    public string? Language { get; init; }
}

public sealed record CharacterFormatRequest(CharacterFormatState Current, CharacterFeatures Features, string Sample)
{
    public bool Offers(CharacterFeatures feature) => (Features & feature) == feature;
}
