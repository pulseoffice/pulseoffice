using PulseOffice.Foundation.Units;

namespace PulseOffice.Text.Rich;

public enum TextAnchor
{
    Top,
    Middle,
    Bottom,
    Justified,
}

public enum TextAutofit
{
    None,
    ShrinkText,
    ResizeShape,
}

public sealed record TextInsets(double Left, double Top, double Right, double Bottom)
{
    public static TextInsets Standard { get; } = new(7.2, 3.6, 7.2, 3.6);

    public double Horizontal => Left + Right;

    public double Vertical => Top + Bottom;
}

public sealed record TextFrame
{
    public static TextFrame Default { get; } = new();

    public TextAnchor Anchor { get; init; } = TextAnchor.Top;

    public TextInsets Insets { get; init; } = TextInsets.Standard;

    public TextAutofit Autofit { get; init; } = TextAutofit.None;

    public double FontScale { get; init; } = 1.0;

    public double LineReduction { get; init; }

    public bool WordWrap { get; init; } = true;
}

public interface ITextFrameResize
{
    DocumentRect Fit(DocumentRect bounds, Twip height, TextAnchor anchor);
}

public sealed class TextFrameResize : ITextFrameResize
{
    public DocumentRect Fit(DocumentRect bounds, Twip height, TextAnchor anchor)
    {
        var grown = Math.Max(1, height.Value);
        var delta = grown - bounds.Height.Value;
        if (delta == 0) return bounds;

        var top = anchor switch
        {
            TextAnchor.Bottom => bounds.Top.Value - delta,
            TextAnchor.Middle or TextAnchor.Justified => bounds.Top.Value - delta / 2,
            _ => bounds.Top.Value,
        };
        return bounds with { Top = new Twip(top), Height = new Twip(grown) };
    }
}
