using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Evaluation.Formats;

public interface IFormatLookup
{
    NumberFormatCatalog Formats { get; }

    FormatHint At(string? sheetName, CellAddress address);

    FormatHint LastIn(string? sheetName, CellRange range);

    PulseOffice.Sheets.Values.CellValue? Evaluate(FormulaNode node);

    FormulaNode? Expand(NameNode name);
}

public interface IFormulaFormatInference
{
    int Infer(FormulaNode formula, IFormatLookup lookup);
}

public abstract class BinaryFormatRule
{
    public abstract FormatHint Combine(FormatHint left, FormatHint right);
}

public sealed class AdditiveFormatRule : BinaryFormatRule
{
    public override FormatHint Combine(FormatHint left, FormatHint right)
    {
        if (left.Kind == FormatKind.Currency) return left;
        if (right.Kind == FormatKind.Currency) return right;

        if (left.IsTemporal || right.IsTemporal) return Temporal(left, right);
        return left.Kind == FormatKind.Percent || right.Kind == FormatKind.Percent
            ? FormatHint.OfKind(FormatKind.Percent)
            : FormatHint.None;
    }

    private FormatHint Temporal(FormatHint left, FormatHint right)
    {
        if (!left.IsTemporal) return right.AsKind();
        if (!right.IsTemporal) return left.AsKind();

        if (left.Kind == right.Kind)
            return left.Kind == FormatKind.Date ? FormatHint.None : FormatHint.OfKind(FormatKind.Duration);

        var dated = left.Kind is FormatKind.Date or FormatKind.DateTime || right.Kind is FormatKind.Date or FormatKind.DateTime;
        var timed = left.Kind == FormatKind.Time || right.Kind == FormatKind.Time;
        return dated && timed ? FormatHint.OfKind(FormatKind.DateTime) : FormatHint.None;
    }
}

public sealed class MultiplicativeFormatRule(bool division) : BinaryFormatRule
{
    public override FormatHint Combine(FormatHint left, FormatHint right)
    {
        if (division) return left.Kind == FormatKind.Currency && right.Kind != FormatKind.Currency ? left : FormatHint.None;
        if (left.Kind == FormatKind.Currency) return left;
        return right.Kind == FormatKind.Currency ? right : FormatHint.None;
    }
}

public sealed class NeutralFormatRule : BinaryFormatRule
{
    public override FormatHint Combine(FormatHint left, FormatHint right) => FormatHint.None;
}

public interface IFormatWalk
{
    FormatHint Hint(FormulaNode node);

    PulseOffice.Sheets.Values.CellValue? Evaluate(FormulaNode node);

    FormatHint At(string? sheetName, CellAddress address);
}

public abstract class FunctionFormatRule
{
    public abstract FormatHint Apply(FunctionNode function, IFormatWalk walk);

    protected FormatHint Argument(FunctionNode function, IFormatWalk walk, int index) =>
        index < function.Arguments.Count ? walk.Hint(function.Arguments[index]) : FormatHint.None;

    protected int? Index(FunctionNode function, IFormatWalk walk, int index) =>
        index < function.Arguments.Count
        && walk.Evaluate(function.Arguments[index]) is { Kind: PulseOffice.Sheets.Values.ValueKind.Number } value
        && value.AsNumber() is >= 1 and <= int.MaxValue and var number
            ? (int)number
            : null;
}

public sealed class FixedFormatRule(FormatHint hint) : FunctionFormatRule
{
    public override FormatHint Apply(FunctionNode function, IFormatWalk walk) => hint;
}

public sealed class ArgumentFormatRule(int index) : FunctionFormatRule
{
    public override FormatHint Apply(FunctionNode function, IFormatWalk walk) => Argument(function, walk, index);
}

public sealed class ArgumentKindFormatRule(int index) : FunctionFormatRule
{
    public override FormatHint Apply(FunctionNode function, IFormatWalk walk)
    {
        var hint = Argument(function, walk, index);
        return hint.Kind == FormatKind.Currency ? hint : hint.AsKind();
    }
}

public sealed class ConditionFormatRule : FunctionFormatRule
{
    public override FormatHint Apply(FunctionNode function, IFormatWalk walk)
    {
        if (function.Arguments.Count == 0 || walk.Evaluate(function.Arguments[0]) is not { } condition) return FormatHint.None;
        var truth = condition.Kind switch
        {
            PulseOffice.Sheets.Values.ValueKind.Boolean => condition.AsBoolean(),
            PulseOffice.Sheets.Values.ValueKind.Number => condition.AsNumber() != 0,
            _ => (bool?)null,
        };
        return truth is { } chosen ? Argument(function, walk, chosen ? 1 : 2) : FormatHint.None;
    }
}

public sealed class ChoiceFormatRule : FunctionFormatRule
{
    public override FormatHint Apply(FunctionNode function, IFormatWalk walk) =>
        Index(function, walk, 0) is { } index ? Argument(function, walk, index) : FormatHint.None;
}

public sealed class IndexFormatRule : FunctionFormatRule
{
    public override FormatHint Apply(FunctionNode function, IFormatWalk walk)
    {
        if (function.Arguments.Count < 2 || function.Arguments[0] is not RangeNode range) return FormatHint.None;
        var first = Index(function, walk, 1);
        var second = function.Arguments.Count > 2 ? Index(function, walk, 2) : 1;
        if (first is not { } row || second is not { } column) return FormatHint.None;

        if (range.Range.RowCount == 1 && function.Arguments.Count == 2) (row, column) = (1, row);
        if (row > range.Range.RowCount || column > range.Range.ColumnCount) return FormatHint.None;
        return walk.At(range.SheetName, new CellAddress(range.Range.Start.Row + row - 1, range.Range.Start.Column + column - 1));
    }
}

public interface IFunctionFormatRules
{
    FunctionFormatRule? For(string name);
}

public sealed class FunctionFormatRules : IFunctionFormatRules
{
    private readonly Dictionary<string, FunctionFormatRule> _rules = new(StringComparer.OrdinalIgnoreCase);

    public FunctionFormatRules()
    {
        var date = new FixedFormatRule(FormatHint.OfKind(FormatKind.Date));
        foreach (var name in new[] { "TODAY", "DATE", "EASTERSUNDAY" }) _rules[name] = date;
        _rules["NOW"] = new FixedFormatRule(FormatHint.OfKind(FormatKind.DateTime));
        var time = new FixedFormatRule(FormatHint.OfKind(FormatKind.Time));
        foreach (var name in new[] { "TIME", "TIMEVALUE" }) _rules[name] = time;

        var first = new ArgumentFormatRule(0);
        foreach (var name in new[] { "SUM", "MIN", "MAX", "MINA", "MAXA" }) _rules[name] = first;
        var kind = new ArgumentKindFormatRule(0);
        foreach (var name in new[] { "AVERAGE", "AVERAGEA" }) _rules[name] = kind;
        _rules["IF"] = new ConditionFormatRule();
        _rules["CHOOSE"] = new ChoiceFormatRule();
        _rules["INDEX"] = new IndexFormatRule();
    }

    public FunctionFormatRule? For(string name) => _rules.GetValueOrDefault(name);
}

public sealed class FormulaFormatInference(IFormatHints hints, IFunctionFormatRules functions) : IFormulaFormatInference
{
    private const int MaxDepth = 64;

    private readonly IFormatHints _hints = hints;
    private readonly IFunctionFormatRules _functions = functions;

    private readonly Dictionary<BinaryOperator, BinaryFormatRule> _binary = new()
    {
        [BinaryOperator.Add] = new AdditiveFormatRule(),
        [BinaryOperator.Subtract] = new AdditiveFormatRule(),
        [BinaryOperator.Multiply] = new MultiplicativeFormatRule(false),
        [BinaryOperator.Divide] = new MultiplicativeFormatRule(true),
    };

    private readonly NeutralFormatRule _neutral = new();

    public int Infer(FormulaNode formula, IFormatLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentNullException.ThrowIfNull(lookup);
        return _hints.Resolve(formula.Accept(new Walker(this, lookup, 0)), lookup.Formats);
    }

    private sealed class Walker(FormulaFormatInference owner, IFormatLookup lookup, int depth)
        : FormulaNodeVisitor<FormatHint>, IFormatWalk
    {
        public FormatHint Hint(FormulaNode node) => node.Accept(this);

        public PulseOffice.Sheets.Values.CellValue? Evaluate(FormulaNode node) => lookup.Evaluate(node);

        public FormatHint At(string? sheetName, CellAddress address) => lookup.At(sheetName, address);

        public override FormatHint VisitReference(ReferenceNode node) => lookup.At(node.SheetName, node.Address);

        public override FormatHint VisitRange(RangeNode node) => lookup.LastIn(node.SheetName, node.Range);

        public override FormatHint VisitSpillReference(SpillReferenceNode node) => lookup.At(node.SheetName, node.Anchor);

        public override FormatHint VisitName(NameNode node) =>
            depth < MaxDepth && lookup.Expand(node) is { } expression
                ? expression.Accept(new Walker(owner, lookup, depth + 1))
                : FormatHint.None;

        public override FormatHint VisitUnary(UnaryNode node) => node.Operator == UnaryOperator.Percent
            ? FormatHint.OfKind(FormatKind.Percent)
            : FormatHint.None;

        public override FormatHint VisitBinary(BinaryNode node) =>
            owner._binary.GetValueOrDefault(node.Operator, owner._neutral).Combine(node.Left.Accept(this), node.Right.Accept(this));

        public override FormatHint VisitFunction(FunctionNode node) =>
            owner._functions.For(node.Name) is { } rule ? rule.Apply(node, this) : FormatHint.None;

        protected override FormatHint Otherwise(FormulaNode node) => FormatHint.None;
    }
}
