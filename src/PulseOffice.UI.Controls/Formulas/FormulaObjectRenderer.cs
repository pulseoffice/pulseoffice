using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Formulas;

namespace PulseOffice.UI.Controls.Formulas;

public interface IFormulaObjectRenderer
{
    void Render(DrawingContext context, FormulaObject formula, Rect bounds);
}

public sealed class FormulaObjectRenderer(IFormulaRenderer formulas, IFormulaParser parser) : IFormulaObjectRenderer
{
    private const double LineHeight = 1.1;

    private const double SmallestGlyph = 8;

    private const double LargestGlyph = 1638;

    private const int CachedExpressions = 256;

    private readonly Dictionary<string, MathFormula> _parsed = new(StringComparer.Ordinal);

    public void Render(DrawingContext context, FormulaObject formula, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(formula);

        formulas.Render(context, Parsed(formula.Expression), bounds, Glyph(bounds.Height / LineHeight),
            new SolidColorBrush(Color.TryParse(formula.ColorHex, out var colour) ? colour : Colors.Black));
    }

    private double Glyph(double size) =>
        double.IsFinite(size) ? Math.Clamp(size, SmallestGlyph, LargestGlyph) : SmallestGlyph;

    private MathFormula Parsed(string expression)
    {
        if (_parsed.TryGetValue(expression, out var known)) return known;
        if (_parsed.Count >= CachedExpressions) _parsed.Clear();
        return _parsed[expression] = parser.Parse(expression);
    }
}
