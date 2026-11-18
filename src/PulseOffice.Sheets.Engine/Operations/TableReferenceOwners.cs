namespace PulseOffice.Sheets.Engine.Operations;

public sealed class TableReferenceOwners
{
    private readonly ChartTableSources _charts = new();

    public void Rename(Workbook workbook, IReadOnlyList<TableRename> renames)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(renames);
        if (renames.Count == 0) return;

        foreach (var sheet in workbook.Sheets)
        {
            for (var index = 0; index < sheet.ConditionalRules.Count; index++)
            {
                var rule = sheet.ConditionalRules[index];
                var renamed = rule.WithFormulas(formula => Renamed(formula, renames));
                if (!renamed.SameFormulas(rule)) sheet.ConditionalRules[index] = renamed;
            }
            for (var index = 0; index < sheet.Validations.Count; index++)
            {
                var validation = sheet.Validations[index];
                var renamed = validation with
                {
                    FirstFormula = Renamed(validation.FirstFormula, renames),
                    SecondFormula = Renamed(validation.SecondFormula, renames),
                };
                if (renamed != validation) sheet.Validations[index] = renamed;
            }
            for (var index = 0; index < sheet.Objects.Count; index++)
            {
                if (sheet.Objects[index] is not { Content: PulseOffice.Charts.ChartDefinition chart } placed) continue;
                var renamed = _charts.Rename(chart, renames);
                if (!ReferenceEquals(renamed, chart)) sheet.Objects[index] = placed with { Content = renamed };
            }
        }

        foreach (var name in workbook.DefinedNames.ToList())
        {
            var expression = Renamed(name.Expression, renames);
            if (string.Equals(expression, name.Expression, StringComparison.Ordinal)) continue;
            workbook.RemoveName(name.Name, name.Scope);
            workbook.DefineName(name with { Expression = expression! });
        }
    }

    private string? Renamed(string? formula, IReadOnlyList<TableRename> renames)
    {
        if (string.IsNullOrEmpty(formula) || !formula.Contains('[', StringComparison.Ordinal)) return formula;
        var prefixed = formula.StartsWith('=');
        var renamer = new TableReferenceRenamer(renames);
        if (renamer.Rewrite(prefixed ? formula : "=" + formula) is not { } rewritten || !renamer.Changed) return formula;
        return prefixed ? rewritten : rewritten[1..];
    }
}
