using System.Globalization;
using System.Text;

namespace PulseOffice.Documents.Fields;

public interface IFieldCondition
{
    bool Holds(string? condition, IReadOnlyDictionary<string, string> variables);

    string Value(string? expression, IReadOnlyDictionary<string, string> variables);
}

public sealed class FieldCondition : IFieldCondition
{
    private readonly IReadOnlyDictionary<string, string> _wordOperators = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["EQ"] = "==",
        ["NEQ"] = "!=",
        ["NE"] = "!=",
        ["LT"] = "<",
        ["GT"] = ">",
        ["LEQ"] = "<=",
        ["LE"] = "<=",
        ["GEQ"] = ">=",
        ["GE"] = ">=",
        ["AND"] = "&&",
        ["OR"] = "||",
        ["NOT"] = "!",
    };

    public bool Holds(string? condition, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(condition)) return false;
        try
        {
            return Truthy(new Parser(Tokens(Strip(condition)), variables).Parse());
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string Value(string? expression, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(expression)) return string.Empty;
        try
        {
            return new Parser(Tokens(Strip(expression)), variables).Parse() switch
            {
                double number => number.ToString(CultureInfo.InvariantCulture),
                var other => other.ToString() ?? string.Empty,
            };
        }
        catch (FormatException)
        {
            return expression;
        }
    }

    private string Strip(string text)
    {
        var trimmed = text.Trim();
        foreach (var prefix in new[] { "ooow:", "ooo:", "oooc:" })
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return trimmed[prefix.Length..];
        return trimmed;
    }

    private bool Truthy(object value) => value switch
    {
        bool flag => flag,
        double number => number != 0,
        string text => text.Length > 0 && text != "0" && !text.Equals("false", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            if (c is '"' or '\'')
            {
                var end = text.IndexOf(c, i + 1);
                if (end < 0) throw new FormatException("Unterminated string.");
                tokens.Add("\"" + text[(i + 1)..end]);
                i = end + 1;
                continue;
            }
            var two = i + 1 < text.Length ? text.Substring(i, 2) : string.Empty;
            if (two is "==" or "!=" or "<=" or ">=" or "&&" or "||" or "<>")
            {
                tokens.Add(two == "<>" ? "!=" : two);
                i += 2;
                continue;
            }
            if (c is '<' or '>' or '!' or '(' or ')' or '+' or '-' or '*' or '/' or '=')
            {
                tokens.Add(c == '=' ? "==" : c.ToString());
                i++;
                continue;
            }
            var word = new StringBuilder();
            while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '.' or ','))
            {
                word.Append(text[i]);
                i++;
            }
            if (word.Length == 0) throw new FormatException("Unexpected character.");
            var value = word.ToString();
            tokens.Add(_wordOperators.TryGetValue(value, out var symbol) ? symbol : value);
        }
        return tokens;
    }

    private sealed class Parser(List<string> tokens, IReadOnlyDictionary<string, string> variables)
    {
        private const int MaximumDepth = 256;

        private const int LongestText = 65_536;

        private int _at;

        private int _depth;

        public object Parse()
        {
            var value = Or();
            if (_at != tokens.Count) throw new FormatException("Trailing tokens.");
            return value;
        }

        private string? Peek => _at < tokens.Count ? tokens[_at] : null;

        private bool Take(string token)
        {
            if (Peek != token) return false;
            _at++;
            return true;
        }

        private object Or()
        {
            var left = And();
            while (Take("||"))
            {
                var right = And();
                left = Truth(left) || Truth(right);
            }
            return left;
        }

        private object And()
        {
            var left = Not();
            while (Take("&&"))
            {
                var right = Not();
                left = Truth(left) && Truth(right);
            }
            return left;
        }

        private object Not()
        {
            if (!Take("!")) return Compare();
            Descend();
            var negated = !Truth(Not());
            _depth--;
            return negated;
        }

        private void Descend()
        {
            if (++_depth > MaximumDepth) throw new FormatException("Expression nested too deeply.");
        }

        private object Compare()
        {
            var left = Sum();
            var op = Peek;
            if (op is not ("==" or "!=" or "<" or ">" or "<=" or ">=")) return left;
            _at++;
            var right = Sum();
            var order = Order(left, right);
            return op switch
            {
                "==" => order == 0,
                "!=" => order != 0,
                "<" => order < 0,
                ">" => order > 0,
                "<=" => order <= 0,
                _ => order >= 0,
            };
        }

        private object Sum()
        {
            var left = Product();
            while (Peek is "+" or "-")
            {
                var op = tokens[_at++];
                var right = Product();
                left = Number(left) is { } a && Number(right) is { } b
                    ? op == "+" ? a + b : a - b
                    : op == "+" ? Joined(Text(left), Text(right)) : Text(left);
            }
            return left;
        }

        private object Product()
        {
            var left = Primary();
            while (Peek is "*" or "/")
            {
                var op = tokens[_at++];
                var right = Primary();
                var a = Number(left) ?? 0;
                var b = Number(right) ?? 0;
                left = op == "*" ? a * b : b == 0 ? 0 : a / b;
            }
            return left;
        }

        private object Primary()
        {
            if (Peek is not { } token) throw new FormatException("Missing operand.");
            _at++;
            if (token == "(")
            {
                Descend();
                var inner = Or();
                if (!Take(")")) throw new FormatException("Missing parenthesis.");
                _depth--;
                return inner;
            }
            if (token == "-")
            {
                Descend();
                var negative = -(Number(Primary()) ?? 0);
                _depth--;
                return negative;
            }
            if (token.StartsWith('"')) return token[1..];
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
            if (token.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (token.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return variables.TryGetValue(token, out var value) ? value : string.Empty;
        }

        private bool Truth(object value) => value switch
        {
            bool flag => flag,
            double number => number != 0,
            string text => text.Length > 0 && text != "0",
            _ => false,
        };

        private double? Number(object value) => value switch
        {
            double number => number,
            bool flag => flag ? 1 : 0,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };

        private string Joined(string left, string right) =>
            left.Length + right.Length > LongestText ? throw new FormatException("Text value too long.") : left + right;

        private string Text(object value) => value switch
        {
            double number => number.ToString(CultureInfo.InvariantCulture),
            bool flag => flag ? "1" : "0",
            _ => value.ToString() ?? string.Empty,
        };

        private int Order(object left, object right) =>
            Number(left) is { } a && Number(right) is { } b ? a.CompareTo(b) : string.CompareOrdinal(Text(left), Text(right));
    }
}
