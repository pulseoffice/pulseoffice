using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.Dialogs;

public sealed record ScenarioRow(Scenario? Existing, string Name, string Comment, CellRange Range, bool Apply = false);

public sealed record ScenarioOutcome(IReadOnlyList<Scenario> Scenarios, Scenario? Apply);

public interface IScenarioPlanner
{
    string NextName(IEnumerable<string> taken, string stem);

    IReadOnlyList<int> InvalidRows(IReadOnlyList<ScenarioRow> rows);

    ScenarioOutcome Build(IReadOnlyList<ScenarioRow> rows, Func<CellRange, string, string?, Scenario> capture);
}

public sealed class ScenarioPlanner : IScenarioPlanner
{
    public string NextName(IEnumerable<string> taken, string stem)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var used = taken.Select(name => name.Trim()).ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        for (var index = 1; ; index++)
        {
            var candidate = $"{stem} {index}";
            if (!used.Contains(candidate)) return candidate;
        }
    }

    public IReadOnlyList<int> InvalidRows(IReadOnlyList<ScenarioRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var invalid = new List<int>();

        for (var index = 0; index < rows.Count; index++)
        {
            var name = rows[index].Name.Trim();
            if (name.Length == 0 || !seen.Add(name)) invalid.Add(index);
        }

        return invalid;
    }

    public ScenarioOutcome Build(IReadOnlyList<ScenarioRow> rows, Func<CellRange, string, string?, Scenario> capture)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(capture);

        var scenarios = new List<Scenario>();
        Scenario? apply = null;

        foreach (var row in rows)
        {
            var name = row.Name.Trim();
            var comment = row.Comment.Trim().Length > 0 ? row.Comment.Trim() : null;
            var scenario = row.Existing is { } existing
                ? existing with { Name = name, Comment = comment }
                : capture(row.Range, name, comment);

            scenarios.Add(scenario);
            if (row.Apply) apply = scenario;
        }

        return new ScenarioOutcome(scenarios, apply);
    }
}
