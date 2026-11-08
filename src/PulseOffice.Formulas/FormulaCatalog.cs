namespace PulseOffice.Formulas;

public sealed record FormulaEntry(
    string Id,
    string Category,
    string Label,
    string Snippet,
    int CaretOffset = -1,
    bool HasSummary = false);

public interface IFormulaCatalog
{
    IReadOnlyList<string> Categories { get; }

    IReadOnlyList<FormulaEntry> All { get; }

    IReadOnlyList<FormulaEntry> InCategory(string category);

    IReadOnlyList<FormulaEntry> Search(string query);
}

public sealed class FormulaCatalog : IFormulaCatalog
{
    private readonly List<FormulaEntry> _entries;

    public FormulaCatalog() : this(FormulaVocabulary.Default) { }

    public FormulaCatalog(IFormulaVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        _entries = [.. Structure(), .. Symbols(vocabulary), .. Functions(vocabulary)];
    }

    public IReadOnlyList<string> Categories =>
        [.. _entries.Select(entry => entry.Category).Distinct()];

    public IReadOnlyList<FormulaEntry> All => _entries;

    public IReadOnlyList<FormulaEntry> InCategory(string category) =>
        [.. _entries.Where(entry => entry.Category == category)];

    public IReadOnlyList<FormulaEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _entries;

        return
        [
            .. _entries.Where(entry =>
                entry.Id.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || entry.Snippet.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || entry.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)),
        ];
    }

    private static IEnumerable<FormulaEntry> Structure() =>
    [
        new("fraction", "structure", "a/b", "frac {a} {b}", 6, true),
        new("square-root", "structure", "√a", "sqrt {a}", 6, true),
        new("nth-root", "structure", "ⁿ√a", "nroot {n} {a}", 7, true),
        new("superscript", "structure", "x²", "{a}^{2}", 1, true),
        new("subscript", "structure", "xᵢ", "{a}_{i}", 1, true),
        new("sub-superscript", "structure", "xᵢ²", "{a}_{i}^{2}", 1, true),
        new("parentheses", "structure", "(a)", "({a})", 2, true),
        new("brackets", "structure", "[a]", "[{a}]", 2, true),
        new("braces", "structure", "{a}", "lbrace {a} rbrace", 8, true),
        new("absolute", "structure", "|a|", "|{a}|", 2, true),
        new("text", "structure", "“ab”", "\"\"", 1, true),
        new("matrix", "matrices", "matrix", "matrix {a & b # c & d}", 8, true),
        new("vector", "matrices", "column", "matrix {a # b}", 8, true),
        new("bar", "accents", "ā", "bar {a}", 5, true),
        new("vec", "accents", "a⃗", "vec {a}", 5, true),
        new("hat", "accents", "â", "hat {a}", 5, true),
        new("dot", "accents", "ȧ", "dot {a}", 5, true),
        new("tilde", "accents", "ã", "tilde {a}", 7, true),
    ];

    private static IEnumerable<FormulaEntry> Symbols(IFormulaVocabulary vocabulary)
    {
        foreach (var (word, glyph) in vocabulary.LargeOperators)
            yield return new FormulaEntry(word, "operators", glyph, $"{word}_{{i=1}}^{{n}} {{a}}", -1, true);

        var aliases = vocabulary.StarMathNames.Values.ToHashSet(StringComparer.Ordinal);

        foreach (var (word, glyph) in vocabulary.Symbols.Where(pair => !aliases.Contains(pair.Key)))
            yield return new FormulaEntry(word, CategoryOf(word), glyph, word);
    }

    private static IEnumerable<FormulaEntry> Functions(IFormulaVocabulary vocabulary) =>
        vocabulary.Functions.Select(name => new FormulaEntry(name, "functions", name, $"{name} {{x}}"));

    private static string CategoryOf(string word) => word switch
    {
        "alpha" or "beta" or "gamma" or "delta" or "epsilon" or "zeta" or "eta" or "theta"
            or "iota" or "kappa" or "lambda" or "mu" or "nu" or "xi" or "rho" or "tau"
            or "pi" or "sigma" or "phi" or "chi" or "psi" or "omega"
            or "Gamma" or "Delta" or "Theta" or "Lambda" or "Xi" or "Pi" or "Sigma"
            or "Phi" or "Psi" or "Omega" => "greek",

        "leq" or "geq" or "neq" or "approx" or "equiv" or "propto" or "sim" or "ll" or "gg"
            or "perp" or "parallel" or "angle" => "relations",

        "in" or "notin" or "subset" or "subseteq" or "supset" or "cup" or "cap"
            or "forall" or "exists" or "emptyset" or "aleph" => "sets",

        "rightarrow" or "leftarrow" or "leftrightarrow" or "Rightarrow" or "Leftarrow"
            or "Leftrightarrow" or "uparrow" or "downarrow" or "mapsto" => "arrows",

        "times" or "cdot" or "div" or "pm" or "mp" or "ast" or "circ" or "oplus" or "otimes"
            or "partial" or "nabla" => "operators",

        _ => "misc",
    };
}
