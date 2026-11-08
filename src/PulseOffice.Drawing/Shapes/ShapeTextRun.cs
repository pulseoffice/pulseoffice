namespace PulseOffice.Drawing.Shapes;

public sealed record ShapeTextRun(string Text)
{
    public const char LineBreak = '\u2028';

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public double? Size { get; init; }

    public string? ColorHex { get; init; }

    public string? Family { get; init; }

    public string? Link { get; init; }
}
