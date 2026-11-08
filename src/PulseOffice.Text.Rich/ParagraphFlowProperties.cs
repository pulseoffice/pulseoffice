namespace PulseOffice.Text.Rich;

public enum LineHeightMode
{
    Proportional,
    AtLeast,
    Exact,
    Leading,
}

public static class LineSpacingFactor
{
    public const double Maximum = 132;

    public static double Bounded(double factor) => factor > Maximum ? Maximum : factor;
}

public sealed record ParagraphLineHeight(LineHeightMode Mode, double Value)
{
    public LineHeightMode Mode { get; init; } = Mode;

    public double Value
    {
        get;
        init => field = Bounded(Mode, value);
    } = Bounded(Mode, Value);

    private static double Bounded(LineHeightMode mode, double value) =>
        mode == LineHeightMode.Proportional ? LineSpacingFactor.Bounded(value) : value;

    public static ParagraphLineHeight Proportional(double factor) => new(LineHeightMode.Proportional, factor);

    public static ParagraphLineHeight AtLeast(double points) => new(LineHeightMode.AtLeast, points);

    public static ParagraphLineHeight Exact(double points) => new(LineHeightMode.Exact, points);

    public static ParagraphLineHeight Leading(double points) => new(LineHeightMode.Leading, points);
}

public sealed record ParagraphHyphenation
{
    public static readonly ParagraphHyphenation Off = new() { Enabled = false };

    public static readonly ParagraphHyphenation On = new();

    public bool Enabled { get; init; } = true;

    public int MinimumWordLength { get; init; } = 5;

    public int CharactersBefore { get; init; } = 2;

    public int CharactersAfter { get; init; } = 2;

    public int MaximumConsecutive { get; init; }

    public bool SkipCapitalized { get; init; }
}

public sealed record DropCap(int Lines, int Characters)
{
    public bool WholeWord { get; init; }

    public double Distance { get; init; }

    public string? CharacterStyleName { get; init; }

    public bool IsActive => Lines > 1 && (Characters > 0 || WholeWord);
}
