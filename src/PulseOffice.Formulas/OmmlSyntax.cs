using System.Xml.Linq;

namespace PulseOffice.Formulas;

public interface IOmmlSyntax
{
    XElement Write(MathFormula formula);

    MathFormula Read(XElement element);
}

public sealed class OmmlSyntax : IOmmlSyntax
{
    private readonly OmmlWriter _writer = new();
    private readonly IFormulaParser _parser;

    public OmmlSyntax() : this(new FormulaParser()) { }

    public OmmlSyntax(IFormulaParser parser) => _parser = parser;

    public static readonly XNamespace Namespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/math";

    public XElement Write(MathFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        return new XElement(Namespace + "oMath", _writer.Nodes(formula.Root));
    }

    private readonly XmlDepthGuard _depth = new(256);

    public MathFormula Read(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_depth.Exceeds(element)) return new MathFormula(MathRow.Empty);

        var children = element.Elements().Select(Node).ToList();

        return new MathFormula(children.Count == 1 ? children[0] : new MathRow(children));
    }

    private MathNode Node(XElement element)
    {
        MathNode Part(string name)
        {
            var part = element.Element(Namespace + name);

            return part is null ? MathRow.Empty : Sequence(part);
        }

        return element.Name.LocalName switch
        {
            "r" => Text(element),
            "e" => Sequence(element),

            "f" => new MathFraction(Part("num"), Part("den")),

            "rad" => element.Element(Namespace + "deg") is { } degree && degree.HasElements
                ? new MathRoot(Part("e"), Sequence(degree))
                : new MathRoot(Part("e")),

            "sSup" => new MathSuperscript(Part("e"), Part("sup")),
            "sSub" => new MathSubscript(Part("e"), Part("sub")),
            "sSubSup" => new MathSubSuperscript(Part("e"), Part("sub"), Part("sup")),

            "d" => new MathFenced(
                Delimited(element),
                Attribute(element, "dPr", "begChr") ?? "(",
                Attribute(element, "dPr", "endChr") ?? ")"),

            "func" => new MathFunction(
                element.Element(Namespace + "fName")?.Value ?? string.Empty, Part("e")),

            "nary" => new MathLargeOperator(
                Attribute(element, "naryPr", "chr") ?? "\u222B",
                Part("e"),
                element.Element(Namespace + "sub") is { HasElements: true } lower ? Sequence(lower) : null,
                element.Element(Namespace + "sup") is { HasElements: true } upper ? Sequence(upper) : null),

            "m" => new MathMatrix(
                [.. element.Elements(Namespace + "mr").Select(row => (IReadOnlyList<MathNode>)
                    [.. row.Elements(Namespace + "e").Select(Sequence)])]),

            "acc" => new MathAccent(Part("e"), Attribute(element, "accPr", "chr") ?? "\u0302"),

            _ => Sequence(element),
        };
    }

    private MathNode Text(XElement run)
    {
        var text = string.Concat(run.Elements(Namespace + "t").Select(node => node.Value));

        if (text.Length == 0) return MathRow.Empty;
        if (run.Element(Namespace + "rPr")?.Element(Namespace + "nor") is not null) return new MathText(text);
        if (text.All(character => char.IsDigit(character) || character is '.')) return new MathNumber(text);
        if (text.Length > 1) return _parser.Parse(text).Root;
        if (text.All(char.IsLetter)) return new MathIdentifier(text);

        return new MathOperator(text);
    }

    private MathNode Delimited(XElement element)
    {
        var parts = element.Elements(Namespace + "e").Select(Sequence).ToList();

        if (parts.Count == 0) return MathRow.Empty;
        if (parts.Count == 1) return parts[0];

        var separator = Attribute(element, "dPr", "sepChr") ?? "|";
        var children = new List<MathNode>();

        foreach (var part in parts)
        {
            if (children.Count > 0) children.Add(new MathOperator(separator));
            children.Add(part);
        }

        return new MathRow(children);
    }

    private MathNode Sequence(XElement element)
    {
        var children = element.Elements()
            .Where(node => node.Name.LocalName is not ("fPr" or "radPr" or "dPr" or "naryPr" or "accPr" or "rPr"))
            .Select(Node)
            .Where(node => node is not MathRow { Children.Count: 0 })
            .ToList();

        return children.Count == 1 ? children[0] : new MathRow(children);
    }

    private string? Attribute(XElement element, string properties, string name) =>
        element.Element(Namespace + properties)?
            .Element(Namespace + name)?
            .Attribute(Namespace + "val")?.Value;
}

internal sealed class OmmlWriter : MathNodeVisitor<XElement>
{
    private static readonly XNamespace Ns = OmmlSyntax.Namespace;

    public IEnumerable<XElement> Nodes(MathNode node) => node is MathRow row ? row.Children.SelectMany(Nodes) : [node.Accept(this)];

    public override XElement VisitNumber(MathNumber node) => Run(node.Text);

    public override XElement VisitIdentifier(MathIdentifier node) => Run(node.Text);

    public override XElement VisitOperator(MathOperator node) => Run(node.Text);

    public override XElement VisitText(MathText node) =>
        new(Ns + "r", new XElement(Ns + "rPr", new XElement(Ns + "nor")), new XElement(Ns + "t", node.Content));

    public override XElement VisitRow(MathRow node) => new(Ns + "e", node.Children.SelectMany(Nodes));

    public override XElement VisitFraction(MathFraction node) =>
        new(Ns + "f",
            new XElement(Ns + "fPr", new XElement(Ns + "type", new XAttribute(Ns + "val", "bar"))),
            new XElement(Ns + "num", Nodes(node.Numerator)),
            new XElement(Ns + "den", Nodes(node.Denominator)));

    public override XElement VisitRoot(MathRoot node) =>
        new(Ns + "rad",
            new XElement(Ns + "radPr", new XElement(Ns + "degHide", new XAttribute(Ns + "val", node.Index is null ? "1" : "0"))),
            new XElement(Ns + "deg", node.Index is null ? [] : Nodes(node.Index)),
            Wrapped(node.Radicand));

    public override XElement VisitSuperscript(MathSuperscript node) =>
        new(Ns + "sSup", Wrapped(node.Base), new XElement(Ns + "sup", Nodes(node.Exponent)));

    public override XElement VisitSubscript(MathSubscript node) =>
        new(Ns + "sSub", Wrapped(node.Base), new XElement(Ns + "sub", Nodes(node.Index)));

    public override XElement VisitSubSuperscript(MathSubSuperscript node) =>
        new(Ns + "sSubSup", Wrapped(node.Base), new XElement(Ns + "sub", Nodes(node.Index)), new XElement(Ns + "sup", Nodes(node.Exponent)));

    public override XElement VisitFenced(MathFenced node) =>
        new(Ns + "d",
            new XElement(Ns + "dPr",
                new XElement(Ns + "begChr", new XAttribute(Ns + "val", node.Open)),
                new XElement(Ns + "endChr", new XAttribute(Ns + "val", node.Close))),
            Wrapped(node.Content));

    public override XElement VisitFunction(MathFunction node) =>
        new(Ns + "func", new XElement(Ns + "fName", Run(node.Name)), Wrapped(node.Argument));

    public override XElement VisitLargeOperator(MathLargeOperator node) =>
        new(Ns + "nary",
            new XElement(Ns + "naryPr",
                new XElement(Ns + "chr", new XAttribute(Ns + "val", node.Symbol)),
                new XElement(Ns + "subHide", new XAttribute(Ns + "val", node.Lower is null ? "1" : "0")),
                new XElement(Ns + "supHide", new XAttribute(Ns + "val", node.Upper is null ? "1" : "0"))),
            new XElement(Ns + "sub", node.Lower is null ? [] : Nodes(node.Lower)),
            new XElement(Ns + "sup", node.Upper is null ? [] : Nodes(node.Upper)),
            Wrapped(node.Body));

    public override XElement VisitMatrix(MathMatrix node) =>
        new(Ns + "m", node.Rows.Select(row => new XElement(Ns + "mr", row.Select(Wrapped))));

    public override XElement VisitAccent(MathAccent node) =>
        new(Ns + "acc", new XElement(Ns + "accPr", new XElement(Ns + "chr", new XAttribute(Ns + "val", node.Accent))), Wrapped(node.Base));

    protected override XElement Otherwise(MathNode node) => new(Ns + "e");

    private XElement Run(string text) => new(Ns + "r", new XElement(Ns + "t", text));

    private XElement Wrapped(MathNode node) => new(Ns + "e", Nodes(node));
}
