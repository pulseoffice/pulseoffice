using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Sheets.Formatting;

public enum NumberCategory { General, Number, Currency, Percent, Scientific, Fraction, Date, Time, Text, Custom }

public enum NegativeNumberStyle { Minus, Red, Parentheses, RedParentheses }

public sealed record CurrencySymbol(string Symbol, bool Before);

public sealed record NumberFormatChoice
{
    public NumberCategory Category { get; init; } = NumberCategory.General;

    public int Decimals { get; init; } = 2;

    public bool Thousands { get; init; }

    public NegativeNumberStyle Negative { get; init; } = NegativeNumberStyle.Minus;

    public CurrencySymbol Currency { get; init; } = new("€", false);

    public string Code { get; init; } = "General";
}

public interface INumberFormatDesigner
{
    IReadOnlyList<NumberCategory> Categories { get; }

    IReadOnlyList<CurrencySymbol> Currencies { get; }

    IReadOnlyList<NegativeNumberStyle> NegativeStyles { get; }

    IReadOnlyList<string> Presets(NumberCategory category, NumberFormatCatalog catalog);

    bool HasDecimals(NumberCategory category);

    bool HasThousands(NumberCategory category);

    bool HasNegatives(NumberCategory category);

    bool HasCurrency(NumberCategory category);

    bool HasPresets(NumberCategory category);

    string Compose(NumberFormatChoice choice);

    NumberFormatChoice Recognize(string code);

    double SampleNumber(NumberCategory category);
}

public sealed partial class NumberFormatDesigner : INumberFormatDesigner
{
    public const int MaximumDecimals = 10;

    private static readonly string[] DatePresets =
    [
        "dd/mm/yyyy", "d/m/yy", "d mmmm yyyy", "dddd d mmmm yyyy", "d-mmm-yy", "d-mmm", "mmm-yy", "mmmm yyyy",
        "yyyy-mm-dd", "dd/mm/yyyy h:mm",
    ];

    private static readonly string[] TimePresets =
    [
        "h:mm", "h:mm:ss", "h:mm AM/PM", "h:mm:ss AM/PM", "[h]:mm:ss", "mm:ss", "mm:ss.0",
    ];

    private static readonly string[] FractionPresets =
    [
        "# ?/?", "# ??/??", "# ???/???", "# ?/2", "# ?/4", "# ?/8", "# ??/16", "# ?/10", "# ??/100",
    ];

    public IReadOnlyList<NumberCategory> Categories { get; } = Enum.GetValues<NumberCategory>();

    public IReadOnlyList<CurrencySymbol> Currencies { get; } =
    [
        new("€", false), new("$", true), new("£", true), new("¥", true), new("CHF", false), new("₹", true), new("R$", true),
    ];

    public IReadOnlyList<NegativeNumberStyle> NegativeStyles { get; } = Enum.GetValues<NegativeNumberStyle>();

    public IReadOnlyList<string> Presets(NumberCategory category, NumberFormatCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return category switch
        {
            NumberCategory.Date => DatePresets,
            NumberCategory.Time => TimePresets,
            NumberCategory.Fraction => FractionPresets,
            NumberCategory.Custom => [.. NumberFormatCatalog.BuiltInFormats.Values.Concat(catalog.CustomFormats.Keys).Distinct(StringComparer.Ordinal)],
            _ => [],
        };
    }

    public bool HasDecimals(NumberCategory category) =>
        category is NumberCategory.Number or NumberCategory.Currency or NumberCategory.Percent or NumberCategory.Scientific;

    public bool HasThousands(NumberCategory category) => category == NumberCategory.Number;

    public bool HasNegatives(NumberCategory category) => category is NumberCategory.Number or NumberCategory.Currency;

    public bool HasCurrency(NumberCategory category) => category == NumberCategory.Currency;

    public bool HasPresets(NumberCategory category) =>
        category is NumberCategory.Date or NumberCategory.Time or NumberCategory.Fraction or NumberCategory.Custom;

    public double SampleNumber(NumberCategory category) => category switch
    {
        NumberCategory.Date => 46022,
        NumberCategory.Time => 0.6354166666666666,
        NumberCategory.Percent => 0.1275,
        _ => 1234.5678,
    };

    public string Compose(NumberFormatChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var decimals = Math.Clamp(choice.Decimals, 0, MaximumDecimals);
        var fraction = decimals > 0 ? "." + new string('0', decimals) : string.Empty;

        return choice.Category switch
        {
            NumberCategory.General => "General",
            NumberCategory.Text => "@",
            NumberCategory.Percent => "0" + fraction + "%",
            NumberCategory.Scientific => "0" + fraction + "E+00",
            NumberCategory.Number => WithNegative((choice.Thousands ? "#,##0" : "0") + fraction, choice.Negative),
            NumberCategory.Currency => WithNegative(Currency("#,##0" + fraction, choice.Currency), choice.Negative),
            _ => string.IsNullOrWhiteSpace(choice.Code) ? "General" : choice.Code,
        };
    }

    public NumberFormatChoice Recognize(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || string.Equals(code, "General", StringComparison.OrdinalIgnoreCase))
            return new NumberFormatChoice { Category = NumberCategory.General, Code = "General" };
        if (code == "@") return new NumberFormatChoice { Category = NumberCategory.Text, Code = code };

        if (Matches(DatePresets, code)) return new NumberFormatChoice { Category = NumberCategory.Date, Code = code };
        if (Matches(TimePresets, code)) return new NumberFormatChoice { Category = NumberCategory.Time, Code = code };
        if (Matches(FractionPresets, code)) return new NumberFormatChoice { Category = NumberCategory.Fraction, Code = code };

        if (PercentPattern().Match(code) is { Success: true } percent)
            return new NumberFormatChoice { Category = NumberCategory.Percent, Decimals = percent.Groups["d"].Length, Code = code };
        if (ScientificPattern().Match(code) is { Success: true } scientific)
            return new NumberFormatChoice { Category = NumberCategory.Scientific, Decimals = scientific.Groups["d"].Length, Code = code };

        var sections = code.Split(';');
        if (sections.Length <= 2 && Body(sections[0]) is { } body)
        {
            var negative = sections.Length == 1 ? NegativeNumberStyle.Minus : NegativeOf(sections[1], sections[0]);
            if (negative is { } style)
            {
                if (body.Currency is { } symbol)
                    return new NumberFormatChoice
                    {
                        Category = NumberCategory.Currency, Decimals = body.Decimals, Negative = style,
                        Currency = Currencies.FirstOrDefault(known => known.Symbol == symbol.Symbol) ?? symbol, Code = code,
                    };
                return new NumberFormatChoice
                {
                    Category = NumberCategory.Number, Decimals = body.Decimals, Thousands = body.Thousands, Negative = style, Code = code,
                };
            }
        }

        return new NumberFormatChoice { Category = NumberCategory.Custom, Code = code };
    }

    private static bool Matches(IEnumerable<string> presets, string code) =>
        presets.Any(preset => string.Equals(preset, code, StringComparison.OrdinalIgnoreCase));

    private static string Currency(string body, CurrencySymbol symbol)
    {
        var quoted = new StringBuilder().Append('"').Append(symbol.Symbol).Append('"').ToString();
        return symbol.Before ? quoted + body : body + " " + quoted;
    }

    private static string WithNegative(string body, NegativeNumberStyle style) => style switch
    {
        NegativeNumberStyle.Red => body + ";[RED]-" + body,
        NegativeNumberStyle.Parentheses => body + ";(" + body + ")",
        NegativeNumberStyle.RedParentheses => body + ";[RED](" + body + ")",
        _ => body,
    };

    private static NegativeNumberStyle? NegativeOf(string section, string positive) =>
        section == "-" + positive ? NegativeNumberStyle.Minus
        : section.Equals("[RED]-" + positive, StringComparison.OrdinalIgnoreCase) ? NegativeNumberStyle.Red
        : section == "(" + positive + ")" ? NegativeNumberStyle.Parentheses
        : section.Equals("[RED](" + positive + ")", StringComparison.OrdinalIgnoreCase) ? NegativeNumberStyle.RedParentheses
        : null;

    private sealed record NumberBody(bool Thousands, int Decimals, CurrencySymbol? Currency);

    private static NumberBody? Body(string section)
    {
        var match = NumberPattern().Match(section);
        if (!match.Success) return null;
        var before = match.Groups["before"].Success ? match.Groups["before"].Value : null;
        var after = match.Groups["after"].Success ? match.Groups["after"].Value : null;
        if (before is not null && after is not null) return null;
        var thousands = match.Groups["int"].Value == "#,##0";
        var symbol = before is not null ? new CurrencySymbol(before, true) : after is not null ? new CurrencySymbol(after, false) : null;
        if (symbol is not null && !thousands) return null;
        return new NumberBody(thousands, match.Groups["d"].Length, symbol);
    }

    [GeneratedRegex("^(?:\"(?<before>[^\"]+)\")?(?<int>#,##0|0)(?:\\.(?<d>0+))?(?: \"(?<after>[^\"]+)\")?$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex("^0(?:\\.(?<d>0+))?%$")]
    private static partial Regex PercentPattern();

    [GeneratedRegex("^0(?:\\.(?<d>0+))?E\\+00$", RegexOptions.IgnoreCase)]
    private static partial Regex ScientificPattern();
}
