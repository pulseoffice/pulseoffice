using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public sealed record ScenarioCell(CellValue Value, string? Formula = null)
{
    public bool HasFormula => !string.IsNullOrEmpty(Formula);
}

public sealed record Scenario
{
    public required string Name { get; init; }

    public string? Comment { get; init; }

    public required CellRange Range { get; init; }

    public required IReadOnlyDictionary<CellAddress, CellValue> Values { get; init; }

    public IReadOnlyDictionary<CellAddress, string> Formulas { get; init; } = new Dictionary<CellAddress, string>();

    public bool PreventChanges { get; init; }

    public string? BorderColorHex { get; init; }

    public ScenarioCell CellAt(CellAddress address) => new(
        Values.TryGetValue(address, out var value) ? value : CellValue.Empty,
        Formulas.TryGetValue(address, out var formula) ? formula : null);

    public IEnumerable<CellAddress> Addresses => Values.Keys.Union(Formulas.Keys);
}
