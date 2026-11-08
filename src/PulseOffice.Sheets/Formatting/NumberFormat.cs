namespace PulseOffice.Sheets.Formatting;

public sealed record FormatCondition(string Operator, double Value)
{
    public bool Matches(double number) => Operator switch
    {
        "<" => number < Value,
        "<=" => number <= Value,
        ">" => number > Value,
        ">=" => number >= Value,
        "=" => number == Value,
        "<>" => number != Value,
        _ => false,
    };
}

public enum FormatValueKind { Number, DateTime, Text, Fraction, Scientific }

public sealed record FormatSection
{
    public required string Pattern { get; init; }
    public required FormatValueKind ValueKind { get; init; }

    public string? ColorName { get; init; }

    public FormatCondition? Condition { get; init; }

    public int DecimalPlaces { get; init; }

    public bool UseThousandSeparator { get; init; }

    public bool IsPercentage { get; init; }

    public int ThousandScale { get; init; }

    public string Prefix { get; init; } = string.Empty;
    public string Suffix { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public IReadOnlyList<FormatToken> Tokens { get; init; } = [];
}

public sealed record FormatToken(string Text, bool IsLiteral);

public sealed record NumberFormat
{
    public required string Code { get; init; }
    public required IReadOnlyList<FormatSection> Sections { get; init; }

    public static readonly NumberFormat General = new()
    {
        Code = "General",
        Sections = [new FormatSection { Pattern = "General", ValueKind = FormatValueKind.Number }],
    };

    public bool IsGeneral => Sections.Count == 1 && Sections[0].Pattern == "General";

    public FormatSection Select(double number)
    {
        var conditional = Sections.Where(s => s.Condition is not null).ToList();
        if (conditional.Count > 0)
            return conditional.FirstOrDefault(s => s.Condition!.Matches(number))
                   ?? Sections.FirstOrDefault(s => s.Condition is null)
                   ?? Sections[0];

        return number switch
        {
            < 0 when Sections.Count > 1 => Sections[1],
            0 when Sections.Count > 2 => Sections[2],
            _ => Sections[0],
        };
    }

    public FormatSection? TextSection =>
        Sections.Count > 3 ? Sections[3] : Sections.FirstOrDefault(s => s.ValueKind == FormatValueKind.Text);

    public bool ShowsMagnitude(FormatSection section) =>
        Sections.All(s => s.Condition is null) && Sections.Count > 1 && !ReferenceEquals(section, Sections[0]);
}
