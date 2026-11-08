namespace PulseOffice.Formulas;

public interface IFormulaVocabulary
{
    IReadOnlyDictionary<string, string> Symbols { get; }

    IReadOnlyDictionary<string, string> LargeOperators { get; }

    IReadOnlyDictionary<string, string> Accents { get; }

    IReadOnlySet<string> Functions { get; }

    IReadOnlySet<string> Constructs { get; }

    IReadOnlyDictionary<string, string> Delimiters { get; }

    IReadOnlyDictionary<string, string> StarMathNames { get; }

    IReadOnlyDictionary<string, string> CompoundOperators { get; }
}

public sealed class FormulaVocabulary : IFormulaVocabulary
{
    public static readonly FormulaVocabulary Default = new();

    public IReadOnlyDictionary<string, string> Symbols { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ",
        ["epsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η", ["theta"] = "θ",
        ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ",
        ["nu"] = "ν", ["xi"] = "ξ", ["rho"] = "ρ", ["tau"] = "τ",
        ["pi"] = "π", ["sigma"] = "σ", ["phi"] = "φ", ["chi"] = "χ",
        ["psi"] = "ψ", ["omega"] = "ω",
        ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ",
        ["Xi"] = "Ξ", ["Pi"] = "Π", ["Sigma"] = "Σ", ["Phi"] = "Φ",
        ["Psi"] = "Ψ", ["Omega"] = "Ω",
        ["times"] = "×", ["cdot"] = "⋅", ["div"] = "÷", ["pm"] = "±", ["mp"] = "∓",
        ["ast"] = "∗", ["circ"] = "∘", ["oplus"] = "⊕", ["otimes"] = "⊗",
        ["leq"] = "≤", ["geq"] = "≥", ["neq"] = "≠", ["approx"] = "≈",
        ["equiv"] = "≡", ["propto"] = "∝", ["sim"] = "∼",
        ["ll"] = "≪", ["gg"] = "≫",
        ["infinity"] = "∞", ["infty"] = "∞", ["partial"] = "∂", ["nabla"] = "∇",
        ["forall"] = "∀", ["exists"] = "∃", ["emptyset"] = "∅",
        ["in"] = "∈", ["notin"] = "∉", ["subset"] = "⊂", ["subseteq"] = "⊆",
        ["supset"] = "⊃", ["cup"] = "∪", ["cap"] = "∩",
        ["rightarrow"] = "→", ["leftarrow"] = "←", ["leftrightarrow"] = "↔",
        ["Rightarrow"] = "⇒", ["Leftarrow"] = "⇐", ["Leftrightarrow"] = "⇔",
        ["uparrow"] = "↑", ["downarrow"] = "↓", ["mapsto"] = "↦",
        ["degree"] = "°", ["angle"] = "∠", ["perp"] = "⊥", ["parallel"] = "∥",
        ["lbrace"] = "{", ["rbrace"] = "}",
        ["dots"] = "…", ["cdots"] = "⋯", ["vdots"] = "⋮", ["ddots"] = "⋱",
        ["aleph"] = "ℵ", ["hbar"] = "ℏ", ["Re"] = "ℜ", ["Im"] = "ℑ",
        ["union"] = "∪", ["intersection"] = "∩", ["prop"] = "∝",
        ["drarrow"] = "⇒", ["dlarrow"] = "⇐", ["dlrarrow"] = "⇔",
        ["dotsaxis"] = "⋯", ["dotslow"] = "…", ["dotsvert"] = "⋮", ["dotsdown"] = "⋱",
    };

    public IReadOnlyDictionary<string, string> StarMathNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["leq"] = "<=", ["geq"] = ">=", ["pm"] = "+-", ["mp"] = "-+",
        ["cup"] = "union", ["cap"] = "intersection", ["propto"] = "prop", ["infty"] = "infinity",
        ["Rightarrow"] = "drarrow", ["Leftarrow"] = "dlarrow", ["Leftrightarrow"] = "dlrarrow",
        ["dots"] = "dotslow", ["cdots"] = "dotsaxis", ["vdots"] = "dotsvert", ["ddots"] = "dotsdown",
    };

    public IReadOnlyDictionary<string, string> CompoundOperators { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["<="] = "≤", [">="] = "≥", ["<>"] = "≠", ["+-"] = "±", ["-+"] = "∓", ["<<"] = "≪", [">>"] = "≫",
    };

    public IReadOnlyDictionary<string, string> LargeOperators { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sum"] = "∑", ["prod"] = "∏", ["int"] = "∫",
        ["iint"] = "∬", ["iiint"] = "∭", ["oint"] = "∮",
        ["coprod"] = "∐", ["bigcup"] = "⋃", ["bigcap"] = "⋂",
    };

    public IReadOnlyDictionary<string, string> Accents { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bar"] = "¯", ["vec"] = "→", ["hat"] = "̂", ["dot"] = "˙", ["tilde"] = "~",
    };

    public IReadOnlySet<string> Functions { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan",
        "sinh", "cosh", "tanh", "ln", "log", "exp", "abs", "det", "lim", "max", "min",
        "gcd", "deg", "dim", "ker", "sup", "inf", "mod",
    };

    public IReadOnlySet<string> Constructs { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "frac", "over", "sqrt", "nroot", "matrix", "bar", "vec", "hat", "dot", "tilde", "left", "right",
    };

    public IReadOnlyDictionary<string, string> Delimiters { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["none"] = string.Empty, ["lbrace"] = "{", ["rbrace"] = "}", ["langle"] = "⟨", ["rangle"] = "⟩",
        ["lline"] = "|", ["rline"] = "|", ["ldline"] = "‖", ["rdline"] = "‖",
        ["lceil"] = "⌈", ["rceil"] = "⌉", ["lfloor"] = "⌊", ["rfloor"] = "⌋",
    };
}
