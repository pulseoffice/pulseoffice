using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Sheets.Formatting;

public interface INumberFormatParser
{
    NumberFormat Parse(string code);

    NumberFormat Parse(string code, string language, FormatCodeVocabulary vocabulary);
}

public sealed class NumberFormatParser : INumberFormatParser
{
    private readonly FormatSectionSplitter _splitter = new();
    private readonly FormatTokenizer _tokenizer = new();
    private readonly FormatBodyClassifier _classifier = new();
    private readonly IReadOnlyDictionary<FormatValueKind, PlaceholderSyntax> _syntaxes;
    private readonly FormatBodyMeasure _measure = new();

    public NumberFormatParser()
    {
        var numeric = new NumericSyntax();
        _syntaxes = new Dictionary<FormatValueKind, PlaceholderSyntax>
        {
            [FormatValueKind.DateTime] = new DateTimeSyntax(),
            [FormatValueKind.Fraction] = new FractionSyntax(numeric),
            [FormatValueKind.Number] = numeric,
            [FormatValueKind.Scientific] = new ScientificSyntax(),
            [FormatValueKind.Text] = new TextSyntax(),
        };
    }

    public NumberFormat Parse(string code, string language, FormatCodeVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        return Parse(vocabulary.ToCanonical(code, language));
    }

    public NumberFormat Parse(string code)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            code.Equals("General", StringComparison.OrdinalIgnoreCase) ||
            code.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            return NumberFormat.General;

        return new NumberFormat { Code = code, Sections = [.. _splitter.Split(code).Select(ParseSection)] };
    }

    private FormatSection ParseSection(string pattern)
    {
        var tokens = _tokenizer.Tokenize(pattern, out var color, out var condition);
        var body = string.Concat(tokens.Where(t => !t.IsLiteral).Select(t => t.Text));
        var kind = _classifier.Classify(body);
        var syntax = _syntaxes.GetValueOrDefault(kind, _syntaxes[FormatValueKind.Number]);
        var literals = new FormatLiterals(tokens, syntax);
        var dated = kind == FormatValueKind.DateTime;

        return new FormatSection
        {
            Pattern = pattern,
            ValueKind = kind,
            ColorName = color,
            Condition = condition,
            DecimalPlaces = _measure.Decimals(literals.Body),
            UseThousandSeparator = _measure.HasThousandSeparator(literals.Body),
            IsPercentage = literals.Body.Contains('%', StringComparison.Ordinal),
            ThousandScale = _measure.TrailingScale(literals.Body),
            Prefix = dated ? string.Empty : literals.Prefix,
            Suffix = dated ? string.Empty : literals.Suffix,
            Body = literals.Body,
            Tokens = tokens,
        };
    }
}

internal sealed class FormatSectionSplitter
{
    public IReadOnlyList<string> Split(string code)
    {
        var sections = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];

            if (c == '"') { inQuotes = !inQuotes; current.Append(c); continue; }
            if (c == '\\' && i + 1 < code.Length) { current.Append(c).Append(code[++i]); continue; }

            if (c == ';' && !inQuotes)
            {
                sections.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        sections.Add(current.ToString());
        return sections;
    }
}

internal sealed class FormatTokenizer
{
    private static readonly HashSet<string> ElapsedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "h", "hh", "m", "mm", "s", "ss",
    };

    private readonly ColorTag _color = new();
    private readonly ConditionTag _condition = new();

    public IReadOnlyList<FormatToken> Tokenize(string pattern, out string? color, out FormatCondition? condition)
    {
        color = null;
        condition = null;
        var tokens = new List<FormatToken>();
        var literal = new StringBuilder();

        void Flush()
        {
            if (literal.Length == 0) return;
            tokens.Add(new FormatToken(literal.ToString(), true));
            literal.Clear();
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];

            if (c == '"')
            {
                var end = pattern.IndexOf('"', i + 1);
                if (end < 0) end = pattern.Length;
                literal.Append(pattern[(i + 1)..end]);
                i = end;
            }
            else if (c == '\\' && i + 1 < pattern.Length)
            {
                literal.Append(pattern[++i]);
            }
            else if (c == '_' && i + 1 < pattern.Length)
            {
                literal.Append(' ');
                i++;
            }
            else if (c == '*' && i + 1 < pattern.Length)
            {
                i++;
            }
            else if (c == '[' && pattern.IndexOf(']', i) is var close and > 0)
            {
                var content = pattern[(i + 1)..close];
                i = close;

                if (ElapsedCodes.Contains(content))
                {
                    Flush();
                    tokens.Add(new FormatToken("[" + content.ToLowerInvariant() + "]", false));
                }
                else if (content.StartsWith('$'))
                {
                    var dash = content.IndexOf('-', StringComparison.Ordinal);
                    literal.Append(dash < 0 ? content[1..] : content[1..dash]);
                }
                else if (_color.Read(content) is { } named)
                {
                    color ??= named;
                }
                else if (_condition.Read(content) is { } parsed)
                {
                    condition ??= parsed;
                }
            }
            else
            {
                Flush();
                tokens.Add(new FormatToken(c.ToString(), false));
            }
        }

        Flush();
        return tokens;
    }
}

internal sealed class ColorTag
{
    public string? Read(string content) =>
        FormatCodeVocabulary.CanonicalColor(content) is { } color
            ? char.ToUpperInvariant(color[0]) + color[1..].ToLowerInvariant()
            : null;
}

internal sealed class ConditionTag
{
    private readonly Regex _regex = new(@"^(<=|>=|<>|<|>|=)([-+]?[0-9.,]+)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public FormatCondition? Read(string content)
    {
        var match = _regex.Match(content);
        return match.Success && double.TryParse(match.Groups[2].Value.Replace(",", ".", StringComparison.Ordinal),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? new FormatCondition(match.Groups[1].Value, value)
            : null;
    }
}

internal sealed class FormatBodyClassifier
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

    private readonly Regex _scientific = new(@"[0#?][.,0#?]*[Ee][+-]", RegexOptions.CultureInvariant, Timeout);
    private readonly Regex _fraction = new(@"[0#?]\s*/\s*[0-9#?]", RegexOptions.CultureInvariant, Timeout);
    private readonly Regex _fractionalSeconds = new(@"[sS]\.0+", RegexOptions.CultureInvariant, Timeout);
    private readonly Regex _dateLetters = new(@"[dyhs]|\[[hms]+\]|AM/PM|A/P",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, Timeout);

    private readonly IReadOnlyList<(FormatValueKind Kind, Func<string, bool> Matches)> _rules;

    public FormatBodyClassifier()
    {
        _rules =
        [
            (FormatValueKind.Scientific, body => _scientific.IsMatch(body)),
            (FormatValueKind.Fraction, body => _fraction.IsMatch(body)),
            (FormatValueKind.DateTime, IsDateTime),
            (FormatValueKind.Text, body => body.Contains('@', StringComparison.Ordinal)),
        ];
    }

    public FormatValueKind Classify(string body) =>
        _rules.FirstOrDefault(rule => rule.Matches(body)) is { Matches: not null } rule ? rule.Kind : FormatValueKind.Number;

    private bool IsDateTime(string body)
    {
        var withoutFractions = _fractionalSeconds.Replace(body, "s");
        if (withoutFractions.IndexOfAny(['0', '#', '@', '?']) >= 0) return false;

        return _dateLetters.IsMatch(withoutFractions) || withoutFractions.Contains('m', StringComparison.OrdinalIgnoreCase);
    }
}

internal abstract class PlaceholderSyntax
{
    public abstract bool IsPlaceholder(char c);
}

internal sealed class NumericSyntax : PlaceholderSyntax
{
    public override bool IsPlaceholder(char c) => c is '0' or '#' or '?' or '.' or ',' or '%';
}

internal sealed class ScientificSyntax : PlaceholderSyntax
{
    public override bool IsPlaceholder(char c) => c is '0' or '#' or '?' or '.' or ',' or 'E' or 'e' or '+' or '-';
}

internal sealed class FractionSyntax(NumericSyntax numeric) : PlaceholderSyntax
{
    public override bool IsPlaceholder(char c) => numeric.IsPlaceholder(c) || char.IsAsciiDigit(c) || c is ' ' or '/';
}

internal sealed class TextSyntax : PlaceholderSyntax
{
    public override bool IsPlaceholder(char c) => c == '@';
}

internal sealed class DateTimeSyntax : PlaceholderSyntax
{
    public override bool IsPlaceholder(char c) => char.IsLetter(c) || c is ':' or '/' or '.' or '-' or ' ';
}

internal sealed class FormatLiterals
{
    public FormatLiterals(IReadOnlyList<FormatToken> tokens, PlaceholderSyntax syntax)
    {
        var body = new StringBuilder();
        var prefix = new StringBuilder();
        var suffix = new StringBuilder();
        var seenPlaceholder = false;

        foreach (var token in tokens)
        {
            if (!token.IsLiteral && syntax.IsPlaceholder(token.Text[0]) && (seenPlaceholder || token.Text[0] != ' '))
            {
                seenPlaceholder = true;
                body.Append(token.Text);
            }
            else
            {
                (seenPlaceholder ? suffix : prefix).Append(token.Text);
            }
        }

        var trimmed = body.ToString().TrimEnd(' ');
        suffix.Insert(0, new string(' ', body.Length - trimmed.Length));

        Prefix = prefix.ToString();
        Suffix = suffix.ToString();
        Body = trimmed;
    }

    public string Prefix { get; }

    public string Suffix { get; }

    public string Body { get; }
}

internal sealed class FormatBodyMeasure
{
    public int Decimals(string body)
    {
        var separator = body.LastIndexOf('.');
        return separator < 0 ? 0 : body[(separator + 1)..].TakeWhile(IsDigitPlaceholder).Count();
    }

    public bool HasThousandSeparator(string body)
    {
        for (var i = 1; i < body.Length - 1; i++)
            if (body[i] == ',' && IsDigitPlaceholder(body[i - 1]) && IsDigitPlaceholder(body[i + 1]))
                return true;

        return false;
    }

    public int TrailingScale(string body) => body.TrimEnd('%', ' ').Reverse().TakeWhile(c => c == ',').Count();

    private bool IsDigitPlaceholder(char c) => c is '0' or '#' or '?';
}
