using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface ITableReferenceFlattening
{
    string Flatten(string formula, Workbook workbook, SheetCell at, Func<string, bool> keeps);
}

public sealed class TableReferenceFlattening(ITableReferenceResolution resolution) : ITableReferenceFlattening
{
    public TableReferenceFlattening() : this(new TableReferenceResolution()) { }

    public string Flatten(string formula, Workbook workbook, SheetCell at, Func<string, bool> keeps)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(keeps);
        if (!formula.Contains('[', StringComparison.Ordinal)) return formula;
        return new Flattener(resolution, workbook, at, keeps).Rewrite(formula) ?? formula;
    }

    private sealed class Flattener(ITableReferenceResolution resolution, Workbook workbook, SheetCell at, Func<string, bool> keeps) : FormulaRewriter
    {
        public override FormulaNode VisitReference(ReferenceNode node) => node;

        public override FormulaNode VisitRange(RangeNode node) => node;

        public override FormulaNode VisitTableReference(TableReferenceNode node)
        {
            if (keeps(node.Table)) return node;
            if (resolution.Reference(workbook, node, at) is not { } resolved) return Broken();
            var sheet = workbook.Sheets[workbook.DatabaseRanges.Find(node.Table)!.SheetIndex];
            if (at.SheetIndex != workbook.IndexOf(sheet)) return resolved;
            return resolved switch
            {
                ReferenceNode reference => reference with { SheetName = null },
                RangeNode range => range with { SheetName = null },
                _ => resolved,
            };
        }
    }
}

public sealed record TableRename(string Table, string? NewTable, string? Column, string? NewColumn);

public sealed class TableReferenceRenamer(IReadOnlyList<TableRename> renames) : FormulaRewriter
{
    public override FormulaNode VisitReference(ReferenceNode node) => node;

    public override FormulaNode VisitRange(RangeNode node) => node;

    public override FormulaNode VisitTableReference(TableReferenceNode node)
    {
        var renamed = node;
        foreach (var rename in renames)
        {
            if (!string.Equals(node.Table, rename.Table, StringComparison.OrdinalIgnoreCase)) continue;
            if (rename.NewTable is { } table) renamed = renamed with { Table = table };
            if (rename.Column is { } column && rename.NewColumn is { } newColumn)
                renamed = renamed with
                {
                    FirstColumn = Same(node.FirstColumn, column) ? newColumn : renamed.FirstColumn,
                    LastColumn = Same(node.LastColumn, column) ? newColumn : renamed.LastColumn,
                };
        }
        if (renamed != node) Changed = true;
        return renamed;
    }

    public bool Changed { get; private set; }

    private bool Same(string? name, string column) => name is not null && string.Equals(name, column, StringComparison.OrdinalIgnoreCase);
}
