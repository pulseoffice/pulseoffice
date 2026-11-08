using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats;

public sealed record TextFrameLayer
{
    public static TextFrameLayer Empty { get; } = new();

    public TextAnchor? Anchor { get; init; }

    public double? Left { get; init; }

    public double? Top { get; init; }

    public double? Right { get; init; }

    public double? Bottom { get; init; }

    public TextAutofit? Autofit { get; init; }

    public double? FontScale { get; init; }

    public double? LineReduction { get; init; }

    public bool? Wrap { get; init; }

    public TextFrameLayer Over(TextFrameLayer lower) => new()
    {
        Anchor = Anchor ?? lower.Anchor,
        Left = Left ?? lower.Left,
        Top = Top ?? lower.Top,
        Right = Right ?? lower.Right,
        Bottom = Bottom ?? lower.Bottom,
        Autofit = Autofit ?? lower.Autofit,
        FontScale = Autofit is null ? lower.FontScale : FontScale,
        LineReduction = Autofit is null ? lower.LineReduction : LineReduction,
        Wrap = Wrap ?? lower.Wrap,
    };

    public static TextFrameLayer Of(TextFrame frame) => new()
    {
        Anchor = frame.Anchor,
        Left = frame.Insets.Left,
        Top = frame.Insets.Top,
        Right = frame.Insets.Right,
        Bottom = frame.Insets.Bottom,
        Autofit = frame.Autofit,
        FontScale = frame.FontScale,
        LineReduction = frame.LineReduction,
        Wrap = frame.WordWrap,
    };

    public TextFrame ToFrame(TextFrame fallback) => new()
    {
        Anchor = Anchor ?? fallback.Anchor,
        Insets = new TextInsets(
            Left ?? fallback.Insets.Left, Top ?? fallback.Insets.Top,
            Right ?? fallback.Insets.Right, Bottom ?? fallback.Insets.Bottom),
        Autofit = Autofit ?? fallback.Autofit,
        FontScale = FontScale ?? 1.0,
        LineReduction = LineReduction ?? 0,
        WordWrap = Wrap ?? fallback.WordWrap,
    };
}
