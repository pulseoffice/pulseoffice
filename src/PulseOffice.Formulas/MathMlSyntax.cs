using System.Xml.Linq;

namespace PulseOffice.Formulas;

public interface IMathMlSyntax
{
    XElement Write(MathFormula formula);

    MathFormula Read(XElement element);
}

public sealed class MathMlSyntax : IMathMlSyntax
{
    private readonly MathMlWriter _writer = new();
    private readonly IStarMathSource _starMath;

    public MathMlSyntax(IStarMathSource? starMath = null) => _starMath = starMath ?? new StarMathSource();

    public static readonly XNamespace Namespace = "http://www.w3.org/1998/Math/MathML";

    public XElement Write(MathFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        var math = new XElement(
            Namespace + "math",
            new XAttribute("display", "block"),
            new XElement(Namespace + "semantics", formula.Root.Accept(_writer)));

        if (formula.Source.Length > 0)
            math.Element(Namespace + "semantics")!.Add(new XElement(
                Namespace + "annotation",
                new XAttribute("encoding", "StarMath 5.0"),
                _starMath.ToStarMath(formula.Source)));

        return math;
    }

    private readonly XmlDepthGuard _depth = new(256);

    public MathFormula Read(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_depth.Exceeds(element)) return new MathFormula(MathRow.Empty);

        var annotation = element
            .Descendants(Namespace + "annotation")
            .FirstOrDefault(node => (string?)node.Attribute("encoding") == "StarMath 5.0");

        var semantics = element
            .Descendants()
            .FirstOrDefault(node => node.Name.Namespace == Namespace && node.Name.LocalName == "semantics");

        var body = semantics?
            .Elements()
            .FirstOrDefault(node => node.Name.LocalName is not ("annotation" or "annotation-xml"));

        var root = semantics is null
            ? RowOrShape([.. element.Elements()])
            : body is null ? MathRow.Empty : Node(body);

        return new MathFormula(root) { Source = annotation?.Value ?? string.Empty };
    }

    private MathNode Node(XElement element)
    {
        var children = element.Elements().ToList();

        return element.Name.LocalName switch
        {
            "mn" => new MathNumber(element.Value),
            "mi" => new MathIdentifier(element.Value),
            "mo" => new MathOperator(element.Value),
            "mtext" => new MathText(element.Value),

            "mfrac" when children.Count == 2 => new MathFraction(Node(children[0]), Node(children[1])),

            "msqrt" => new MathRoot(children.Count == 1 ? Node(children[0]) : Row(children)),

            "mroot" when children.Count == 2 => new MathRoot(Node(children[0]), Node(children[1])),

            "msup" when children.Count == 2 => new MathSuperscript(Node(children[0]), Node(children[1])),
            "msub" when children.Count == 2 => new MathSubscript(Node(children[0]), Node(children[1])),

            "msubsup" when children.Count == 3 =>
                new MathSubSuperscript(Node(children[0]), Node(children[1]), Node(children[2])),

            "mfenced" => new MathFenced(
                children.Count == 1 ? Node(children[0]) : Row(children),
                (string?)element.Attribute("open") ?? "(",
                (string?)element.Attribute("close") ?? ")"),

            "mover" when children.Count == 2 && (string?)element.Attribute("accent") == "true" =>
                new MathAccent(Node(children[0]), children[1].Value),

            "munder" when children.Count == 2 =>
                new MathLargeOperator(children[0].Value, MathRow.Empty, Node(children[1])),

            "mover" when children.Count == 2 =>
                new MathLargeOperator(children[0].Value, MathRow.Empty, null, Node(children[1])),

            "munderover" when children.Count == 3 => new MathLargeOperator(
                children[0].Value, MathRow.Empty, Node(children[1]), Node(children[2])),

            "mtable" => new MathMatrix(
                [.. children.Select(row => (IReadOnlyList<MathNode>)
                    [.. row.Elements().Select(cell =>
                        cell.Elements().Count() == 1 ? Node(cell.Elements().First()) : Row([.. cell.Elements()]))])]),

            "mrow" => RowOrShape(children),

            _ => Row(children),
        };
    }

    private MathNode RowOrShape(IReadOnlyList<XElement> children)
    {
        if (children.Count == 1) return Node(children[0]);

        if (children.Count == 3
            && children[0].Name.LocalName == "mi"
            && children[1].Name.LocalName == "mo"
            && children[1].Value == "\u2061")
            return new MathFunction(children[0].Value, Node(children[2]));

        if (children.Count == 2
            && children[0].Name.LocalName is "munder" or "mover" or "munderover"
            && Node(children[0]) is MathLargeOperator head)
            return head with { Body = Node(children[1]) };

        if (children.Count == 2 && children[0].Name.LocalName == "mo" && Symbols.Contains(children[0].Value))
            return new MathLargeOperator(children[0].Value, Node(children[1]));

        return Row(children);
    }

    private readonly HashSet<string> Symbols =
        ["\u2211", "\u220F", "\u222B", "\u222C", "\u222E", "\u2210"];

    private MathNode Row(IReadOnlyList<XElement> children) =>
        children.Count == 0 ? MathRow.Empty : new MathRow([.. children.Select(Node)]);
}

internal sealed class MathMlWriter : MathNodeVisitor<XElement>
{
    private static readonly XNamespace Ns = MathMlSyntax.Namespace;

    public override XElement VisitNumber(MathNumber node) => new(Ns + "mn", node.Text);

    public override XElement VisitIdentifier(MathIdentifier node) => new(Ns + "mi", node.Text);

    public override XElement VisitOperator(MathOperator node) => new(Ns + "mo", node.Text);

    public override XElement VisitText(MathText node) => new(Ns + "mtext", node.Content);

    public override XElement VisitRow(MathRow node) => new(Ns + "mrow", node.Children.Select(Write));

    public override XElement VisitFraction(MathFraction node) => new(Ns + "mfrac", Write(node.Numerator), Write(node.Denominator));

    public override XElement VisitRoot(MathRoot node) =>
        node.Index is null ? new(Ns + "msqrt", Write(node.Radicand)) : new(Ns + "mroot", Write(node.Radicand), Write(node.Index));

    public override XElement VisitSuperscript(MathSuperscript node) => new(Ns + "msup", Write(node.Base), Write(node.Exponent));

    public override XElement VisitSubscript(MathSubscript node) => new(Ns + "msub", Write(node.Base), Write(node.Index));

    public override XElement VisitSubSuperscript(MathSubSuperscript node) =>
        new(Ns + "msubsup", Write(node.Base), Write(node.Index), Write(node.Exponent));

    public override XElement VisitFenced(MathFenced node) =>
        new(Ns + "mfenced", new XAttribute("open", node.Open), new XAttribute("close", node.Close), Write(node.Content));

    public override XElement VisitFunction(MathFunction node) =>
        new(Ns + "mrow", new XElement(Ns + "mi", node.Name), new XElement(Ns + "mo", "\u2061"), Write(node.Argument));

    public override XElement VisitLargeOperator(MathLargeOperator node)
    {
        var symbol = new XElement(Ns + "mo", node.Symbol);
        XElement head = (node.Lower, node.Upper) switch
        {
            (null, null) => symbol,
            (not null, null) => new XElement(Ns + "munder", symbol, Write(node.Lower)),
            (null, not null) => new XElement(Ns + "mover", symbol, Write(node.Upper)),
            _ => new XElement(Ns + "munderover", symbol, Write(node.Lower!), Write(node.Upper!)),
        };
        return new XElement(Ns + "mrow", head, Write(node.Body));
    }

    public override XElement VisitMatrix(MathMatrix node) =>
        new(Ns + "mtable", node.Rows.Select(row => new XElement(Ns + "mtr", row.Select(cell => new XElement(Ns + "mtd", Write(cell))))));

    public override XElement VisitAccent(MathAccent node) =>
        new(Ns + "mover", new XAttribute("accent", "true"), Write(node.Base), new XElement(Ns + "mo", node.Accent));

    protected override XElement Otherwise(MathNode node) => new(Ns + "mrow");

    private XElement Write(MathNode node) => node.Accept(this);
}
