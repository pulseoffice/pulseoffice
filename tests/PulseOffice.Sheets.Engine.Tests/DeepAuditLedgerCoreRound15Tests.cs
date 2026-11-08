using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditLedgerCoreRound15Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell At(int row, int column) => new(0, new CellAddress(row, column));

    [Fact]
    public void ANestedEvaluationInsideALambdaDoesNotWidenTheRecursionCap()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.DefineName(new DefinedName("DEPTH", "=LAMBDA(n;IF(n=0;0;1+DEPTH(n-1)))"));
        var evaluator = fixture.Evaluator;
        var parser = new PulseOffice.Sheets.Engine.Parsing.FormulaParser();
        var context = new ReentrantContext(engine.ContextAt(At(0, 0)), () => evaluator.EvaluateRaw(parser.Parse("=1"), engine.ContextAt(At(0, 5))));
        var capped = evaluator.Evaluate(parser.Parse("=LET(f;LAMBDA(g;n;IF(n=0;0;1+g(g;n-1)));f(f;200))"), context);
        Assert.True(capped.IsError, capped.ToString());
        Assert.Equal(CellValue.Number(50), evaluator.Evaluate(parser.Parse("=LET(f;LAMBDA(g;n;IF(n=0;0;1+g(g;n-1)));f(f;50))"), context));

        CellValue? result = null;
        var thread = new Thread(() =>
            result = evaluator.Evaluate(parser.Parse("=LET(f;LAMBDA(g;n;IF(n=0;C1+0;1+g(g;n-1)));a;f(f;60);b;f(f;120);c;f(f;240);c)"), context),
            16 * 1024 * 1024) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)));
        Assert.True(result is { IsError: true }, result?.ToString());
    }

    [Fact]
    public void ARecursiveLambdaEvaluatesOnlyTheChosenBranchOfIf()
    {
        var (_, engine) = fixture.CreateWorkbook();
        CellValue? result = null;
        var thread = new Thread(() =>
        {
            engine.SetFormula(At(0, 0), "=LET(fib;LAMBDA(f;n;IF(n<2;n;f(f;n-1)+f(f;n-2)));fib(fib;15))");
            result = engine.GetValue(At(0, 0));
        }, 16 * 1024 * 1024) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        Assert.Equal(CellValue.Number(610), result);
    }

    [Theory]
    [InlineData("=IFERROR(1/0;LET(f;LAMBDA(g;n;IF(n=0;7;g(g;n-1)));f(f;40)))", 7)]
    [InlineData("=IFS(FALSE;1/0;TRUE;2)", 2)]
    [InlineData("=IFNA(NA();3)", 3)]
    [InlineData("=IF(TRUE;4;1/0)", 4)]
    public void ErrorHandlingFunctionsStillGiveTheirResults(string formula, double expected)
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(At(0, 0), formula);
        Assert.Equal(CellValue.Number(expected), engine.GetValue(At(0, 0)));
    }

    private sealed class ReentrantContext(PulseOffice.Sheets.Engine.Evaluation.IEvaluationContext inner, Action nested)
        : PulseOffice.Sheets.Engine.Evaluation.IEvaluationContext
    {
        public SheetCell CurrentCell => inner.CurrentCell;
        public PulseOffice.Sheets.Engine.Evaluation.Functions.NumberToText NumberText => inner.NumberText;
        public PulseOffice.Sheets.Engine.Evaluation.Functions.NumericText TextNumbers => inner.TextNumbers;
        public bool ImplicitIntersection => inner.ImplicitIntersection;
        public PulseOffice.Sheets.Engine.Evaluation.Functions.TextRules TextRules => inner.TextRules;
        public void NoteArrayUse() => inner.NoteArrayUse();
        public CellValue GetCellValue(string? sheetName, CellAddress address)
        {
            nested();
            return inner.GetCellValue(sheetName, address);
        }
        public IEnumerable<CellValue> GetRangeValues(string? sheetName, CellRange range) => inner.GetRangeValues(sheetName, range);
        public CellRange? ContentWithin(string? sheetName, CellRange range) => inner.ContentWithin(sheetName, range);
        public string? ResolveName(string name) => inner.ResolveName(name);
        public bool SheetExists(string sheetName) => inner.SheetExists(sheetName);
        public string CurrentSheetName => inner.CurrentSheetName;
        public int SheetCount => inner.SheetCount;
        public int SheetIndex(string? sheetName) => inner.SheetIndex(sheetName);
        public CellRange? ArrayArea => inner.ArrayArea;
        public IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet) => inner.SheetsBetween(firstSheet, lastSheet);
        public string? GetFormulaText(string? sheetName, CellAddress address) => inner.GetFormulaText(sheetName, address);
        public int GetStyle(string? sheetName, CellAddress address) => inner.GetStyle(sheetName, address);
        public PulseOffice.Sheets.Engine.Evaluation.RowVisibility RowVisibilityAt(string? sheetName, int row) => inner.RowVisibilityAt(sheetName, row);
        public CellRange? GetSpillRange(string? sheetName, CellAddress anchor) => inner.GetSpillRange(sheetName, anchor);
        public CellValue EvaluateWith(SheetCell formula, IReadOnlyList<(SheetCell Cell, CellValue Value)> inputs) => inner.EvaluateWith(formula, inputs);
        public void RequestStyle(string styleName) => inner.RequestStyle(styleName);
        public CellValue LookupPivot(string? sheetName, CellAddress target, string dataField, IReadOnlyList<(string Field, string Item)> filters) =>
            inner.LookupPivot(sheetName, target, dataField, filters);
        public string? FetchResource(string url) => inner.FetchResource(url);

        public CellValue EvaluateOffset(string? sheetName, CellAddress formula, int rowDelta, int columnDelta) =>
            inner.EvaluateOffset(sheetName, formula, rowDelta, columnDelta);
    }

    [Fact]
    public void AnOperatorOverTheWholeSheetIsRefusedInsteadOfAllocatingEveryCell()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(At(0, 0), CellValue.Number(2));

        CellValue? result = null;
        var thread = new Thread(() =>
        {
            engine.SetFormula(At(1200, 3), "=SUM((A1:XFD1100)*1)");
            result = engine.GetValue(At(1200, 3));
        }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        Assert.True(result is { IsError: true }, result?.ToString());
    }
}
