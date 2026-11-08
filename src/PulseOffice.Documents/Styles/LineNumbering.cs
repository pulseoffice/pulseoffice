namespace PulseOffice.Documents.Styles;

public enum LineNumberSide { Left, Right }

public sealed record LineNumbering(int Increment = 5)
{
    public LineNumberSide Side { get; init; } = LineNumberSide.Left;

    public double Distance { get; init; } = 0.5 * 72 / 2.54;

    public bool RestartEachPage { get; init; }

    public bool CountBlankLines { get; init; } = true;
}
