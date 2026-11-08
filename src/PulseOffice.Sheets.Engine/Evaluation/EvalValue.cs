using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation;

public abstract record EvalValue
{
    public static EvalValue Scalar(CellValue value) => new ScalarValue(value);
    public static EvalValue Error(CellError error) => new ScalarValue(CellValue.FromError(error));

    public abstract T Accept<T>(IEvalValueVisitor<T> visitor, IEvaluationContext context);
}

public interface IEvalValueVisitor<out T>
{
    T VisitScalar(ScalarValue value, IEvaluationContext context);

    T VisitRange(RangeValue value, IEvaluationContext context);

    T VisitArray(ArrayValue value, IEvaluationContext context);

    T VisitLambda(LambdaValue value, IEvaluationContext context);

    T VisitMissing(MissingValue value, IEvaluationContext context);

    T VisitReferenceList(ReferenceListValue value, IEvaluationContext context);
}

public abstract class EvalValueVisitor<T> : IEvalValueVisitor<T>
{
    public virtual T VisitScalar(ScalarValue value, IEvaluationContext context) => Otherwise(value, context);

    public virtual T VisitRange(RangeValue value, IEvaluationContext context) => Otherwise(value, context);

    public virtual T VisitArray(ArrayValue value, IEvaluationContext context) => Otherwise(value, context);

    public virtual T VisitLambda(LambdaValue value, IEvaluationContext context) => Otherwise(value, context);

    public virtual T VisitMissing(MissingValue value, IEvaluationContext context) => Otherwise(value, context);

    public virtual T VisitReferenceList(ReferenceListValue value, IEvaluationContext context) => Otherwise(value, context);

    protected abstract T Otherwise(EvalValue value, IEvaluationContext context);
}

public sealed record ScalarValue(CellValue Value) : EvalValue
{
    public override T Accept<T>(IEvalValueVisitor<T> visitor, IEvaluationContext context) => visitor.VisitScalar(this, context);
}

public sealed record RangeValue(string? SheetName, CellRange Range) : EvalValue
{
    public override T Accept<T>(IEvalValueVisitor<T> visitor, IEvaluationContext context) => visitor.VisitRange(this, context);
}

public sealed record ReferenceListValue(IReadOnlyList<RangeValue> Areas) : EvalValue
{
    public override T Accept<T>(IEvalValueVisitor<T> visitor, IEvaluationContext context) => visitor.VisitReferenceList(this, context);
}

public sealed record ArrayValue(IReadOnlyList<IReadOnlyList<CellValue>> Rows) : EvalValue
{
    public override T Accept<T>(IEvalValueVisitor<T> visitor, IEvaluationContext context) => visitor.VisitArray(this, context);

    public int RowCount => Rows.Count;
    public int ColumnCount => Rows.Count == 0 ? 0 : Rows[0].Count;
}

public sealed record LambdaValue(
    IReadOnlyList<string> Parameters,
    Parsing.FormulaNode Body,
    EvaluationScope Captured) : EvalValue
{
    public override T Accept<T>(IEvalValueVisitor<T> visitor, IEvaluationContext context) => visitor.VisitLambda(this, context);

    public int Arity => Parameters.Count;

    public int Required { get; init; } = -1;

    public int MinimumArguments => Required < 0 ? Parameters.Count : Required;
}

public sealed record MissingValue : EvalValue
{
    public override T Accept<T>(IEvalValueVisitor<T> visitor, IEvaluationContext context) => visitor.VisitMissing(this, context);

    public static readonly MissingValue Instance = new();
}

public interface IEvaluationContext
{
    SheetCell CurrentCell { get; }

    Functions.NumberToText NumberText { get; }

    Functions.NumericText TextNumbers { get; }

    bool ImplicitIntersection { get; }

    Functions.TextRules TextRules { get; }

    void NoteArrayUse();

    CellValue GetCellValue(string? sheetName, CellAddress address);

    IEnumerable<CellValue> GetRangeValues(string? sheetName, CellRange range);

    CellRange? ContentWithin(string? sheetName, CellRange range);

    string? ResolveName(string name);

    bool SheetExists(string sheetName);

    string CurrentSheetName { get; }

    int SheetCount { get; }

    int SheetIndex(string? sheetName);

    IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet);

    CellRange? ArrayArea { get; }

    string? GetFormulaText(string? sheetName, CellAddress address);

    int GetStyle(string? sheetName, CellAddress address);

    RowVisibility RowVisibilityAt(string? sheetName, int row);

    CellRange? GetSpillRange(string? sheetName, CellAddress anchor);

    CellValue EvaluateWith(SheetCell formula, IReadOnlyList<(SheetCell Cell, CellValue Value)> inputs);

    void RequestStyle(string styleName);

    CellValue LookupPivot(
        string? sheetName, CellAddress target, string dataField,
        IReadOnlyList<(string Field, string Item)> filters);

    string? FetchResource(string url);

    CellValue EvaluateOffset(string? sheetName, CellAddress formula, int rowDelta, int columnDelta);
}
