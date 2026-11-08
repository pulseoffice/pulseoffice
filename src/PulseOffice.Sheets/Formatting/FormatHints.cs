using System.Globalization;

namespace PulseOffice.Sheets.Formatting;

public enum FormatKind { Undefined, Date, Time, DateTime, Duration, Percent, Currency }

public sealed record FormatHint(FormatKind Kind, int FormatId)
{
    public static readonly FormatHint None = new(FormatKind.Undefined, 0);

    public static FormatHint OfKind(FormatKind kind) => new(kind, 0);

    public bool IsNone => Kind == FormatKind.Undefined && FormatId == 0;

    public FormatHint AsKind() => OfKind(Kind);

    public bool IsTemporal => Kind is FormatKind.Date or FormatKind.Time or FormatKind.DateTime or FormatKind.Duration;
}

public interface IFormatKindClassifier
{
    FormatKind Classify(NumberFormat format);
}

public sealed class FormatKindClassifier : IFormatKindClassifier
{
    public FormatKind Classify(NumberFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (format.IsGeneral || format.Sections.Count == 0) return FormatKind.Undefined;

        var section = format.Sections[0];
        if (section.ValueKind == FormatValueKind.DateTime) return Temporal(section);
        if (section.IsPercentage) return FormatKind.Percent;
        return IsCurrency(format, section) ? FormatKind.Currency : FormatKind.Undefined;
    }

    private FormatKind Temporal(FormatSection section)
    {
        var codes = section.Tokens.Where(token => !token.IsLiteral).Select(token => token.Text.ToLowerInvariant()).ToList();
        if (codes.Any(code => code.StartsWith('['))) return FormatKind.Duration;

        var date = codes.Any(code => code.Contains('y') || code.Contains('d') || code.StartsWith("mmm", StringComparison.Ordinal));
        var time = codes.Any(code => code.Contains('h') || code.Contains('s') || code.Contains("am/pm", StringComparison.Ordinal));
        if (!date && !time) date = codes.Any(code => code.Contains('m'));
        return date && time ? FormatKind.DateTime : time ? FormatKind.Time : FormatKind.Date;
    }

    private bool IsCurrency(NumberFormat format, FormatSection section) =>
        format.Code.Contains("[$", StringComparison.Ordinal)
        || (section.Prefix + section.Suffix + string.Concat(section.Tokens.Where(token => token.IsLiteral).Select(token => token.Text)))
            .Any(character => CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.CurrencySymbol);
}

public interface IFormatHints
{
    FormatHint Of(int formatId, NumberFormatCatalog formats);

    int Resolve(FormatHint hint, NumberFormatCatalog formats);
}

public sealed class FormatHints(IFormatKindClassifier classifier) : IFormatHints
{
    private readonly Dictionary<FormatKind, string> _defaults = new()
    {
        [FormatKind.Date] = "dd/mm/yyyy",
        [FormatKind.Time] = "hh:mm:ss",
        [FormatKind.DateTime] = "dd/mm/yyyy hh:mm",
        [FormatKind.Duration] = "[hh]:mm:ss",
        [FormatKind.Percent] = "0.00%",
    };

    public FormatHint Of(int formatId, NumberFormatCatalog formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        if (formatId == 0) return FormatHint.None;
        var kind = classifier.Classify(formats.Get(formatId));
        return new FormatHint(kind, formatId);
    }

    public int Resolve(FormatHint hint, NumberFormatCatalog formats)
    {
        ArgumentNullException.ThrowIfNull(hint);
        ArgumentNullException.ThrowIfNull(formats);
        if (hint.FormatId != 0) return hint.FormatId;
        return _defaults.TryGetValue(hint.Kind, out var code) ? formats.Register(code) : 0;
    }
}
