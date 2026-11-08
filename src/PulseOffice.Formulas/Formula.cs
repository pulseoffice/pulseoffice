namespace PulseOffice.Formulas;

public abstract record MathNode
{
    public abstract TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor);
}

public sealed record MathNumber(string Text) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitNumber(this);
}

public sealed record MathIdentifier(string Text) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitIdentifier(this);
}

public sealed record MathOperator(string Text) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitOperator(this);
}

public sealed record MathText(string Content) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitText(this);
}

public sealed record MathRow(IReadOnlyList<MathNode> Children) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitRow(this);

    public static readonly MathRow Empty = new([]);
}

public sealed record MathFraction(MathNode Numerator, MathNode Denominator) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitFraction(this);
}

public sealed record MathRoot(MathNode Radicand, MathNode? Index = null) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitRoot(this);
}

public sealed record MathSuperscript(MathNode Base, MathNode Exponent) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitSuperscript(this);
}

public sealed record MathSubscript(MathNode Base, MathNode Index) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitSubscript(this);
}

public sealed record MathSubSuperscript(MathNode Base, MathNode Index, MathNode Exponent) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitSubSuperscript(this);
}

public sealed record MathFenced(MathNode Content, string Open, string Close) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitFenced(this);
}

public sealed record MathFunction(string Name, MathNode Argument) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitFunction(this);
}

public sealed record MathLargeOperator(
    string Symbol, MathNode Body, MathNode? Lower = null, MathNode? Upper = null) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitLargeOperator(this);
}

public sealed record MathMatrix(IReadOnlyList<IReadOnlyList<MathNode>> Rows) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitMatrix(this);
}

public sealed record MathAccent(MathNode Base, string Accent) : MathNode
{
    public override TResult Accept<TResult>(IMathNodeVisitor<TResult> visitor) => visitor.VisitAccent(this);
}

public sealed record MathFormula(MathNode Root)
{
    public static readonly MathFormula Empty = new(MathRow.Empty);

    public string Source { get; init; } = string.Empty;

    public bool IsEmpty => Root is MathRow { Children.Count: 0 };
}

public interface IMathNodeVisitor<out TResult>
{
    TResult VisitNumber(MathNumber node);

    TResult VisitIdentifier(MathIdentifier node);

    TResult VisitOperator(MathOperator node);

    TResult VisitText(MathText node);

    TResult VisitRow(MathRow node);

    TResult VisitFraction(MathFraction node);

    TResult VisitRoot(MathRoot node);

    TResult VisitSuperscript(MathSuperscript node);

    TResult VisitSubscript(MathSubscript node);

    TResult VisitSubSuperscript(MathSubSuperscript node);

    TResult VisitFenced(MathFenced node);

    TResult VisitFunction(MathFunction node);

    TResult VisitLargeOperator(MathLargeOperator node);

    TResult VisitMatrix(MathMatrix node);

    TResult VisitAccent(MathAccent node);
}

public abstract class MathNodeVisitor<TResult> : IMathNodeVisitor<TResult>
{
    public virtual TResult VisitNumber(MathNumber node) => Otherwise(node);

    public virtual TResult VisitIdentifier(MathIdentifier node) => Otherwise(node);

    public virtual TResult VisitOperator(MathOperator node) => Otherwise(node);

    public virtual TResult VisitText(MathText node) => Otherwise(node);

    public virtual TResult VisitRow(MathRow node) => Otherwise(node);

    public virtual TResult VisitFraction(MathFraction node) => Otherwise(node);

    public virtual TResult VisitRoot(MathRoot node) => Otherwise(node);

    public virtual TResult VisitSuperscript(MathSuperscript node) => Otherwise(node);

    public virtual TResult VisitSubscript(MathSubscript node) => Otherwise(node);

    public virtual TResult VisitSubSuperscript(MathSubSuperscript node) => Otherwise(node);

    public virtual TResult VisitFenced(MathFenced node) => Otherwise(node);

    public virtual TResult VisitFunction(MathFunction node) => Otherwise(node);

    public virtual TResult VisitLargeOperator(MathLargeOperator node) => Otherwise(node);

    public virtual TResult VisitMatrix(MathMatrix node) => Otherwise(node);

    public virtual TResult VisitAccent(MathAccent node) => Otherwise(node);

    protected abstract TResult Otherwise(MathNode node);
}
