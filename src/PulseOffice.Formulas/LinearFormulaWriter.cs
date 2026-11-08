namespace PulseOffice.Formulas;

public interface ILinearFormulaWriter
{
    string Write(MathFormula formula);
}

public sealed class LinearFormulaWriter : MathNodeVisitor<string>, ILinearFormulaWriter
{
    private readonly Dictionary<string, string> _symbols;
    private readonly Dictionary<string, string> _large;
    private readonly Dictionary<string, string> _accents;
    private readonly Dictionary<string, string> _delimiters;

    public LinearFormulaWriter() : this(FormulaVocabulary.Default) { }

    public LinearFormulaWriter(IFormulaVocabulary vocabulary)
    {
        _symbols = Reverse(vocabulary.Symbols);
        _large = Reverse(vocabulary.LargeOperators);
        _accents = Reverse(vocabulary.Accents);
        _delimiters = Reverse(vocabulary.Delimiters);
    }

    public string Write(MathFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        return formula.Source is { Length: > 0 } source ? source : formula.Root.Accept(this).Trim();
    }

    public override string VisitNumber(MathNumber node) => node.Text;

    public override string VisitIdentifier(MathIdentifier node) => Glyph(node.Text);

    public override string VisitOperator(MathOperator node) => Glyph(node.Text);

    public override string VisitText(MathText node) => "\"" + node.Content.Replace("\"", "'", StringComparison.Ordinal) + "\"";

    public override string VisitRow(MathRow node) => string.Join(" ", node.Children.Select(child => child.Accept(this)));

    public override string VisitFraction(MathFraction node) => $"frac {Group(node.Numerator)} {Group(node.Denominator)}";

    public override string VisitRoot(MathRoot node) =>
        node.Index is null ? $"sqrt {Group(node.Radicand)}" : $"nroot {Group(node.Index)} {Group(node.Radicand)}";

    public override string VisitSuperscript(MathSuperscript node) => $"{Group(node.Base)}^{Group(node.Exponent)}";

    public override string VisitSubscript(MathSubscript node) => $"{Group(node.Base)}_{Group(node.Index)}";

    public override string VisitSubSuperscript(MathSubSuperscript node) => $"{Group(node.Base)}_{Group(node.Index)}^{Group(node.Exponent)}";

    public override string VisitFenced(MathFenced node) => $"left {Delimiter(node.Open)} {node.Content.Accept(this)} right {Delimiter(node.Close)}";

    public override string VisitFunction(MathFunction node) => $"{node.Name} {Group(node.Argument)}";

    public override string VisitLargeOperator(MathLargeOperator node)
    {
        var name = _large.GetValueOrDefault(node.Symbol, "sum");
        var lower = node.Lower is null ? string.Empty : "_" + Group(node.Lower);
        var upper = node.Upper is null ? string.Empty : "^" + Group(node.Upper);
        return $"{name}{lower}{upper} {Group(node.Body)}";
    }

    public override string VisitMatrix(MathMatrix node) =>
        "matrix {" + string.Join(" # ", node.Rows.Select(row => string.Join(" & ", row.Select(cell => cell.Accept(this))))) + "}";

    public override string VisitAccent(MathAccent node) => $"{_accents.GetValueOrDefault(node.Accent, "hat")} {Group(node.Base)}";

    protected override string Otherwise(MathNode node) => string.Empty;

    private string Group(MathNode node) => "{" + node.Accept(this) + "}";

    private string Glyph(string text) => _symbols.TryGetValue(text, out var name) ? "%" + name : text;

    private string Delimiter(string glyph) =>
        glyph.Length == 0 ? "none"
        : glyph is "(" or ")" or "[" or "]" ? glyph
        : _delimiters.GetValueOrDefault(glyph, glyph);

    private Dictionary<string, string> Reverse(IReadOnlyDictionary<string, string> table)
    {
        var reversed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, glyph) in table)
            if (glyph.Length > 0) reversed.TryAdd(glyph, name);
        return reversed;
    }
}
