namespace PulseOffice.Sheets;

public enum CriteriaSyntax { Wildcards, RegularExpressions, Literal }

public sealed record CalculationSettings
{
    public const int DefaultMaxIterations = 100;
    public const double DefaultMaxChange = 0.001;
    public const int MaximumIterations = 32767;

    public static readonly DateOnly StandardDateEpoch = new(1899, 12, 30);
    public static readonly DateOnly Mac1904DateEpoch = new(1904, 1, 1);

    public static readonly CalculationSettings Default = new();

    public DateOnly DateEpoch { get; init; } = StandardDateEpoch;

    public bool UsesStandardDateEpoch => DateEpoch == StandardDateEpoch;

    public bool Iterative { get; init; }

    public int MaxIterations { get; init => field = Math.Clamp(value, 1, MaximumIterations); } = DefaultMaxIterations;

    public double MaxChange { get; init; } = DefaultMaxChange;

    public bool CaseSensitive { get; init; }

    public bool AutomaticFindLabels { get; init; }

    public CriteriaSyntax Criteria { get; init; } = CriteriaSyntax.Wildcards;

    public bool WholeCellCriteria { get; init; } = true;

    public bool BooleansAreNumbersInArrays { get; init; } = true;
}
