using System.Globalization;
using System.Runtime.CompilerServices;

namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IEnhancedExpression
{
    double Evaluate(EnhancedScope scope);
}

public sealed record EnhancedConstant(double Value) : IEnhancedExpression
{
    public double Evaluate(EnhancedScope scope) => Value;
}

public sealed record EnhancedModifier(int Index) : IEnhancedExpression
{
    public double Evaluate(EnhancedScope scope) => scope.Modifier(Index);
}

public sealed record EnhancedReference(string Name) : IEnhancedExpression
{
    public double Evaluate(EnhancedScope scope) => scope.Equation(Name);
}

public sealed record EnhancedKeyword(string Name) : IEnhancedExpression
{
    public double Evaluate(EnhancedScope scope) => scope.Keyword(Name);
}

public sealed record EnhancedNegation(IEnhancedExpression Operand) : IEnhancedExpression
{
    public double Evaluate(EnhancedScope scope)
    {
        scope.Descend();
        return -Operand.Evaluate(scope);
    }
}

public sealed record EnhancedBinary(char Operator, IEnhancedExpression Left, IEnhancedExpression Right) : IEnhancedExpression
{
    public double Evaluate(EnhancedScope scope)
    {
        scope.Descend();
        var left = Left.Evaluate(scope);
        var right = Right.Evaluate(scope);
        return Operator switch
        {
            '+' => left + right,
            '-' => left - right,
            '*' => left * right,
            _ => right == 0 ? 0 : left / right,
        };
    }
}

public sealed record EnhancedFunction(string Name, IReadOnlyList<IEnhancedExpression> Arguments) : IEnhancedExpression
{
    public double Evaluate(EnhancedScope scope)
    {
        scope.Descend();
        double At(int index) => index < Arguments.Count ? Arguments[index].Evaluate(scope) : 0;
        return Name switch
        {
            "abs" => Math.Abs(At(0)),
            "sqrt" => Math.Sqrt(Math.Max(0, At(0))),
            "sin" => Math.Sin(At(0)),
            "cos" => Math.Cos(At(0)),
            "tan" => Math.Tan(At(0)),
            "atan" => Math.Atan(At(0)),
            "atan2" => Math.Atan2(At(0), At(1)),
            "min" => Math.Min(At(0), At(1)),
            "max" => Math.Max(At(0), At(1)),
            "if" => At(0) > 0 ? At(1) : At(2),
            _ => 0,
        };
    }
}

public interface IEnhancedFormulaParser
{
    IEnhancedExpression Parse(string formula);
}

public sealed class EnhancedFormulaParser : IEnhancedFormulaParser
{
    private readonly IReadOnlySet<string> _functions = new HashSet<string>(StringComparer.Ordinal)
    {
        "abs", "sqrt", "sin", "cos", "tan", "atan", "atan2", "min", "max", "if",
    };

    public IEnhancedExpression Parse(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var reader = new Reader(formula, _functions);
        var expression = reader.Sum();
        reader.SkipSpaces();
        if (!reader.AtEnd) throw new FormatException($"Unexpected text in enhanced geometry formula '{formula}'.");
        return expression;
    }

    private sealed class Reader(string text, IReadOnlySet<string> functions)
    {
        private int _position;

        public bool AtEnd => _position >= text.Length;

        public void SkipSpaces()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }

        private void Descend()
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
                throw new FormatException($"The enhanced geometry formula '{text}' is nested too deeply.");
        }

        private char Peek()
        {
            SkipSpaces();
            return AtEnd ? '\0' : text[_position];
        }

        public IEnhancedExpression Sum()
        {
            var left = Product();
            while (Peek() is '+' or '-')
            {
                var symbol = text[_position++];
                left = new EnhancedBinary(symbol, left, Product());
            }
            return left;
        }

        private IEnhancedExpression Product()
        {
            var left = Unary();
            while (Peek() is '*' or '/')
            {
                var symbol = text[_position++];
                left = new EnhancedBinary(symbol, left, Unary());
            }
            return left;
        }

        private IEnhancedExpression Unary()
        {
            Descend();
            if (Peek() == '-')
            {
                _position++;
                return new EnhancedNegation(Unary());
            }
            if (Peek() == '+')
            {
                _position++;
                return Unary();
            }
            return Primary();
        }

        private IEnhancedExpression Primary()
        {
            var next = Peek();
            if (next == '(')
            {
                _position++;
                var inner = Sum();
                Expect(')');
                return inner;
            }
            if (next == '$')
            {
                _position++;
                return new EnhancedModifier(int.Parse(Word(), CultureInfo.InvariantCulture));
            }
            if (next == '?')
            {
                _position++;
                return new EnhancedReference(Word());
            }
            if (char.IsDigit(next) || next == '.') return new EnhancedConstant(Number());
            if (char.IsLetter(next))
            {
                var word = Word();
                if (!functions.Contains(word)) return new EnhancedKeyword(word);
                Expect('(');
                var arguments = new List<IEnhancedExpression> { Sum() };
                while (Peek() == ',')
                {
                    _position++;
                    arguments.Add(Sum());
                }
                Expect(')');
                return new EnhancedFunction(word, arguments);
            }
            throw new FormatException($"Unexpected character in enhanced geometry formula '{text}'.");
        }

        private void Expect(char symbol)
        {
            if (Peek() != symbol) throw new FormatException($"Expected '{symbol}' in enhanced geometry formula '{text}'.");
            _position++;
        }

        private string Word()
        {
            var start = _position;
            while (_position < text.Length && (char.IsLetterOrDigit(text[_position]) || text[_position] == '_')) _position++;
            if (start == _position) throw new FormatException($"Expected a name in enhanced geometry formula '{text}'.");
            return text[start.._position];
        }

        private double Number()
        {
            var start = _position;
            while (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] == '.')) _position++;
            if (_position < text.Length && text[_position] is 'e' or 'E'
                && _position + 1 < text.Length && (char.IsDigit(text[_position + 1]) || text[_position + 1] is '-' or '+'))
            {
                _position += 2;
                while (_position < text.Length && char.IsDigit(text[_position])) _position++;
            }
            return double.Parse(text[start.._position], NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}

public sealed class EnhancedScope
{
    private readonly IReadOnlyDictionary<string, IEnhancedExpression> _equations;
    private readonly IReadOnlyList<double> _modifiers;
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _evaluating = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, double> _keywords;

    public EnhancedScope(
        IReadOnlyDictionary<string, IEnhancedExpression> equations, IReadOnlyList<double> modifiers,
        double left, double top, double width, double height, double logicalWidth, double logicalHeight)
    {
        _equations = equations;
        _modifiers = modifiers;
        _keywords = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["pi"] = Math.PI,
            ["left"] = left,
            ["top"] = top,
            ["right"] = left + width,
            ["bottom"] = top + height,
            ["width"] = width,
            ["height"] = height,
            ["logwidth"] = logicalWidth,
            ["logheight"] = logicalHeight,
            ["xstretch"] = 0,
            ["ystretch"] = 0,
            ["hasstroke"] = 1,
            ["hasfill"] = 1,
        };
    }

    public double Modifier(int index) => index >= 0 && index < _modifiers.Count ? _modifiers[index] : 0;

    public double Keyword(string name) => _keywords.TryGetValue(name, out var value) ? value : 0;

    public void Descend()
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw new FormatException("The enhanced geometry equations are nested too deeply.");
    }

    public double Equation(string name)
    {
        if (_values.TryGetValue(name, out var cached)) return cached;
        Descend();
        if (!_equations.TryGetValue(name, out var expression)) throw new FormatException($"The enhanced geometry refers to the missing equation '{name}'.");
        if (!_evaluating.Add(name)) return 0;
        var value = expression.Evaluate(this);
        _evaluating.Remove(name);
        if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
        _values[name] = value;
        return value;
    }
}
