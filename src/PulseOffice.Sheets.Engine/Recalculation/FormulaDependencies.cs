using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed record CollectedDependencies(
    IReadOnlyList<SheetCell> Cells, IReadOnlyList<(int, CellRange)> Ranges, bool Volatile, bool VisibilitySensitive, bool ReadsTables);

internal sealed class FormulaDependencies(CalculationEngine engine, INameExpansion names, ITableReferenceResolution tables)
{
    private readonly CalculationEngine _engine = engine;

    private readonly INameExpansion _names = names;

    private readonly ITableReferenceResolution _tables = tables;

    private readonly HashSet<string> _geometryFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROW", "COLUMN", "ROWS", "COLUMNS", "AREAS", "SHEET", "ISREF",
    };

    private readonly HashSet<string> _volatileFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NOW", "TODAY", "RAND", "RANDBETWEEN", "INDIRECT", "OFFSET", "INFO", "CELL",
    };

    private readonly HashSet<string> _visibilitySensitiveFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUBTOTAL", "AGGREGATE",
    };

    public CollectedDependencies Collect(FormulaNode node, SheetCell owner)
    {
        var collector = new Collector(this, owner);
        node.Accept(collector);
        return new CollectedDependencies(collector.Cells, collector.Ranges, collector.Volatile, collector.VisibilitySensitive, collector.ReadsTables);
    }

    private sealed class Collector(FormulaDependencies owner, SheetCell cell) : FormulaNodeVisitor<bool>
    {
        private readonly CalculationEngine _engine = owner._engine;

        private readonly int _defaultSheet = cell.SheetIndex;

        private readonly HashSet<string> _expanding = new(StringComparer.OrdinalIgnoreCase);

        public List<SheetCell> Cells { get; } = [];

        public List<(int, CellRange)> Ranges { get; } = [];

        public bool Volatile { get; private set; }

        public bool VisibilitySensitive { get; private set; }

        public bool ReadsTables { get; private set; }

        public override bool VisitReference(ReferenceNode node) => Cell(node.SheetName, node.Address);

        public override bool VisitSpillReference(SpillReferenceNode node) => Cell(node.SheetName, node.Anchor);

        public override bool VisitRange(RangeNode node)
        {
            if (node.SpansSheets)
            {
                foreach (var name in _engine.SheetsBetween(node.SheetName!, node.LastSheetName!))
                    Ranges.Add((_engine.ResolveSheet(name, _defaultSheet), node.Range));
                return true;
            }
            var sheet = _engine.ResolveSheet(node.SheetName, _defaultSheet);
            if (sheet >= 0) Ranges.Add((sheet, node.Range));
            return true;
        }

        public override bool VisitTableReference(TableReferenceNode node)
        {
            ReadsTables = true;
            if (owner._tables.Resolve(_engine.Workbook, node, cell) is { } target) Ranges.Add((target.SheetIndex, target.Range));
            return true;
        }

        public override bool VisitUnary(UnaryNode node) => node.Operand.Accept(this);

        public override bool VisitBinary(BinaryNode node) => node.Left.Accept(this) & node.Right.Accept(this);

        public override bool VisitFunction(FunctionNode node)
        {
            if (owner._volatileFunctions.Contains(node.Name)) Volatile = true;
            if (owner._visibilitySensitiveFunctions.Contains(node.Name)) VisibilitySensitive = true;
            if (owner._geometryFunctions.Contains(node.Name))
                return All(node.Arguments.Where(argument => argument is not (ReferenceNode or RangeNode or SpillReferenceNode or TableReferenceNode)));
            return All(node.Arguments);
        }

        public override bool VisitArray(ArrayNode node) => All(node.Rows.SelectMany(row => row));

        public override bool VisitInvocation(InvocationNode node) => node.Callee.Accept(this) & All(node.Arguments);

        public override bool VisitName(NameNode node)
        {
            if (_engine.Workbook.ResolveName(node.Name, _engine.SheetAt(_defaultSheet)?.Name)?.Expression is not { } expression) return true;
            if (!_expanding.Add(node.Name)) return true;
            try { return owner._names.Expand(expression, cell.Address).Accept(this); }
            catch (FormulaSyntaxException) { return true; }
            finally { _expanding.Remove(node.Name); }
        }

        protected override bool Otherwise(FormulaNode node) => true;

        private bool Cell(string? sheetName, CellAddress address)
        {
            var sheet = _engine.ResolveSheet(sheetName, _defaultSheet);
            if (sheet >= 0) Cells.Add(new SheetCell(sheet, address));
            return true;
        }

        private bool All(IEnumerable<FormulaNode> nodes)
        {
            foreach (var node in nodes) node.Accept(this);
            return true;
        }
    }
}
