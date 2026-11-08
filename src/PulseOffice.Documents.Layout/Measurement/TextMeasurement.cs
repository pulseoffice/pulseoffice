namespace PulseOffice.Documents.Layout.Measurement;

public sealed record FontKey(string Family, double Size, bool Bold, bool Italic)
{
    public bool Kerning { get; init; } = true;

    public FontKey Scaled(double factor) => this with { Size = Size * factor };
}

public sealed record FontMetrics(double Ascent, double Descent, double LineGap)
{
    public double Height => Ascent + Descent + LineGap;
}

public interface ITextMeasurer
{
    FontMetrics Metrics(FontKey font);

    IReadOnlyList<double> Advances(FontKey font, string text);
}

public sealed class FixedPitchMeasurer : ITextMeasurer
{
    private readonly double _width;
    private readonly double _spaceWidth;

    public FixedPitchMeasurer() : this(0.5, 0.25)
    {
    }

    public FixedPitchMeasurer(double width, double spaceWidth)
    {
        _width = width;
        _spaceWidth = spaceWidth;
    }

    public FontMetrics Metrics(FontKey font) => new(font.Size * 0.8, font.Size * 0.2, 0);

    public IReadOnlyList<double> Advances(FontKey font, string text)
    {
        var advances = new double[text.Length];
        var factor = font.Bold ? 1.1 : 1.0;
        for (var i = 0; i < text.Length; i++)
        {
            advances[i] = Advance(text[i]) * font.Size * factor;
        }
        return advances;
    }

    private double Advance(char character) => character switch
    {
        ' ' or '\u00A0' => _spaceWidth,
        '\u00AD' or '\u200B' => 0,
        _ when char.IsLowSurrogate(character) => 0,
        _ => _width,
    };
}
