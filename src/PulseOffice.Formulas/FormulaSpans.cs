namespace PulseOffice.Formulas;

public readonly record struct FormulaSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool Contains(int offset) => offset >= Start && offset <= End;
}

public interface IFormulaSpans
{
    bool TryGet(MathNode node, out FormulaSpan span);

    MathNode? DeepestAt(MathNode root, int offset);
}

public sealed class FormulaSpans : IFormulaSpans
{
    private readonly Dictionary<MathNode, FormulaSpan> _spans = new(ReferenceEqualityComparer.Instance);
    private readonly MathNodes _nodes = new();

    public void Record(MathNode node, FormulaSpan span) => _spans[node] = span;

    public bool TryGet(MathNode node, out FormulaSpan span) => _spans.TryGetValue(node, out span);

    public MathNode? DeepestAt(MathNode root, int offset)
    {
        ArgumentNullException.ThrowIfNull(root);

        MathNode? best = null;
        var narrowest = int.MaxValue;

        void Visit(MathNode node)
        {
            if (TryGet(node, out var span) && span.Contains(offset) && span.Length < narrowest)
            {
                best = node;
                narrowest = span.Length;
            }

            foreach (var child in _nodes.Children(node)) Visit(child);
        }

        Visit(root);
        return best;
    }
}

public sealed class MathNodes
{
    private readonly ChildrenVisitor _children = new();

    public IEnumerable<MathNode> Children(MathNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Accept(_children);
    }

    private sealed class ChildrenVisitor : MathNodeVisitor<IEnumerable<MathNode>>
    {
        public override IEnumerable<MathNode> VisitRow(MathRow node) => node.Children;

        public override IEnumerable<MathNode> VisitFraction(MathFraction node) => [node.Numerator, node.Denominator];

        public override IEnumerable<MathNode> VisitRoot(MathRoot node) =>
            node.Index is null ? [node.Radicand] : [node.Index, node.Radicand];

        public override IEnumerable<MathNode> VisitSuperscript(MathSuperscript node) => [node.Base, node.Exponent];

        public override IEnumerable<MathNode> VisitSubscript(MathSubscript node) => [node.Base, node.Index];

        public override IEnumerable<MathNode> VisitSubSuperscript(MathSubSuperscript node) => [node.Base, node.Index, node.Exponent];

        public override IEnumerable<MathNode> VisitFenced(MathFenced node) => [node.Content];

        public override IEnumerable<MathNode> VisitFunction(MathFunction node) => [node.Argument];

        public override IEnumerable<MathNode> VisitLargeOperator(MathLargeOperator node) =>
            new[] { node.Lower, node.Upper, node.Body }.OfType<MathNode>();

        public override IEnumerable<MathNode> VisitMatrix(MathMatrix node) => node.Rows.SelectMany(row => row);

        public override IEnumerable<MathNode> VisitAccent(MathAccent node) => [node.Base];

        protected override IEnumerable<MathNode> Otherwise(MathNode node) => [];
    }
}

public sealed record ParsedFormula(MathFormula Formula, IFormulaSpans Spans);
