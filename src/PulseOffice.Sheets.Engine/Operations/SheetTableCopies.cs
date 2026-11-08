using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class SheetTableCopies
{
    public IReadOnlyDictionary<string, CopiedTable> Copies(Workbook workbook, int sourceIndex)
    {
        var taken = workbook.DatabaseRanges.All.Select(table => table.Name)
            .Concat(workbook.DefinedNames.Select(name => name.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copies = new Dictionary<string, CopiedTable>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in workbook.DatabaseRanges.All.Where(table => table.SheetIndex == sourceIndex).OrderBy(table => table.Name, StringComparer.Ordinal))
        {
            var fresh = Unique(table.Name, taken);
            taken.Add(fresh);
            var criteriaOnCopy = table.RangeFilter?.Criteria is { } criteria && criteria.SheetIndex == sourceIndex;
            copies[table.Name] = new CopiedTable(table with { Name = fresh, QueryBindingId = null }, criteriaOnCopy);
        }

        return copies;
    }

    public void Rename(Worksheet copy, IReadOnlyDictionary<string, CopiedTable> tables)
    {
        if (tables.Count == 0) return;

        var renamed = tables.ToDictionary(pair => pair.Key, pair => pair.Value.Table.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var (address, cell) in copy.NonEmptyCells().ToList())
        {
            if (!cell.HasFormula) continue;
            var rewriter = new TableNameRewriter(renamed);
            if (rewriter.Rewrite(Prefixed(cell.Formula!)) is not { } rewritten || !rewriter.Changed) continue;
            copy[address] = cell with { Formula = cell.Formula!.StartsWith('=') ? rewritten : rewritten[1..] };
        }
    }

    public void Define(Workbook workbook, SheetCopy copy)
    {
        var position = workbook.IndexOf(copy.Sheet);
        foreach (var (table, criteriaOnCopy) in copy.Tables)
        {
            if (workbook.DatabaseRanges.Find(table.Name) is not null) continue;
            workbook.DatabaseRanges.Define(table with
            {
                SheetIndex = position,
                RangeFilter = table.RangeFilter?.Reindexed(old => criteriaOnCopy ? position : old >= position ? old + 1 : old),
            });
        }
    }

    private string Prefixed(string formula) => formula.StartsWith('=') ? formula : "=" + formula;

    private string Unique(string name, HashSet<string> taken)
    {
        for (var suffix = 2; ; suffix++)
            if (!taken.Contains($"{name}_{suffix}")) return $"{name}_{suffix}";
    }
}

internal sealed class TableNameRewriter(IReadOnlyDictionary<string, string> renamed) : FormulaRewriter
{
    public bool Changed { get; private set; }

    public override FormulaNode VisitName(NameNode node)
    {
        if (!renamed.TryGetValue(node.Name, out var fresh)) return node;
        Changed = true;
        return node with { Name = fresh };
    }

    public override FormulaNode VisitReference(ReferenceNode node) => node;

    public override FormulaNode VisitRange(RangeNode node) => node;
}
