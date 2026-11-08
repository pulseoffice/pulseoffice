using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class CriteriaMatcher
{
    private readonly Comparison _comparison;
    private readonly CellValue _operand;
    private readonly TextPattern? _pattern;
    private readonly StringComparison _textComparison;
    private readonly bool _blank;
    private readonly NumericText? _numbers;

    private enum Comparison { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }

    private CriteriaMatcher(Comparison comparison, CellValue operand, TextPattern? pattern, StringComparison textComparison, bool blank = false, NumericText? numbers = null)
    {
        _textComparison = textComparison;
        _numbers = numbers;
        _blank = blank;
        _comparison = comparison;
        _operand = operand;
        _pattern = pattern;
    }

    public static CriteriaMatcher Parse(CellValue criteria, NumericText numbers, TextRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        rules = rules.ForQueries;
        var textComparison = rules.Comparison;
        if (criteria.IsEmpty) criteria = CellValue.Number(0);
        if (criteria.Kind != ValueKind.Text)
            return new CriteriaMatcher(Comparison.Equal, criteria, null, textComparison);

        var text = criteria.AsText();
        var (comparison, rest) = SplitOperator(text);

        if (rest.Length == 0 && comparison is Comparison.Equal or Comparison.NotEqual)
            return new CriteriaMatcher(comparison, CellValue.Text(rest), null, textComparison, blank: true);

        if (comparison is Comparison.Equal or Comparison.NotEqual)
        {
            if (rules.Pattern(rest, anchored: true) is { } pattern)
                return new CriteriaMatcher(comparison, CellValue.Text(rest), pattern, textComparison);
        }

        ArgumentNullException.ThrowIfNull(numbers);
        if (numbers.TryRead(rest, out var number))
            return new CriteriaMatcher(comparison, CellValue.Number(number), null, textComparison, numbers: numbers);

        return new CriteriaMatcher(comparison, CellValue.Text(rest), null, textComparison);
    }

    public bool Matches(CellValue value)
    {
        if (_blank)
        {
            var isBlank = value.IsEmpty || (value.Kind == ValueKind.Text && value.AsText().Length == 0);
            return _comparison == Comparison.NotEqual ? !isBlank : isBlank;
        }

        if (_pattern is not null)
        {
            var isMatch = value.Kind == ValueKind.Text && _pattern.IsMatch(value.AsText());
            return _comparison == Comparison.NotEqual ? !isMatch : isMatch;
        }

        var order = Compare(NumericEquivalent(value), _operand);
        if (order is null) return _comparison == Comparison.NotEqual;

        return _comparison switch
        {
            Comparison.Equal => order == 0,
            Comparison.NotEqual => order != 0,
            Comparison.Less => order < 0,
            Comparison.LessOrEqual => order <= 0,
            Comparison.Greater => order > 0,
            Comparison.GreaterOrEqual => order >= 0,
            _ => false,
        };
    }

    private CellValue NumericEquivalent(CellValue value) =>
        _numbers is not null && _comparison is Comparison.Equal or Comparison.NotEqual
            && value.Kind == ValueKind.Text && _numbers.TryRead(value.AsText(), out var number)
            ? CellValue.Number(number)
            : value;

    private static (Comparison Operator, string Remainder) SplitOperator(string text)
    {
        if (text.StartsWith("<=", StringComparison.Ordinal)) return (Comparison.LessOrEqual, text[2..]);
        if (text.StartsWith(">=", StringComparison.Ordinal)) return (Comparison.GreaterOrEqual, text[2..]);
        if (text.StartsWith("<>", StringComparison.Ordinal)) return (Comparison.NotEqual, text[2..]);
        if (text.StartsWith('<')) return (Comparison.Less, text[1..]);
        if (text.StartsWith('>')) return (Comparison.Greater, text[1..]);
        if (text.StartsWith('=')) return (Comparison.Equal, text[1..]);
        return (Comparison.Equal, text);
    }

    private int? Compare(CellValue left, CellValue right)
    {
        if (left.IsNumeric && right.IsNumeric)
            return Numerics.SpreadsheetMath.ApproxCompare(left.AsNumber(), right.AsNumber());

        if (left.Kind == ValueKind.Text && right.Kind == ValueKind.Text)
            return string.Compare(left.AsText(), right.AsText(), _textComparison);

        if (left.IsEmpty && right.IsEmpty) return 0;
        return null;
    }
}
