using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IScenarioManager
{
    Scenario Capture(CalculationEngine engine, int sheetIndex, CellRange range, string name, string? comment = null);

    IReadOnlyDictionary<CellAddress, ScenarioCell> Apply(CalculationEngine engine, int sheetIndex, Scenario scenario);

    void Restore(CalculationEngine engine, int sheetIndex, IReadOnlyDictionary<CellAddress, ScenarioCell> cells);

    Scenario? Find(Worksheet sheet, string name);
}

public sealed class ScenarioManager : IScenarioManager
{
    public Scenario Capture(
        CalculationEngine engine, int sheetIndex, CellRange range, string name, string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var sheet = engine.Workbook.Sheets[sheetIndex];
        var values = new Dictionary<CellAddress, CellValue>();
        var formulas = new Dictionary<CellAddress, string>();

        foreach (var address in range.Cells())
        {
            var cell = sheet[address];
            values[address] = cell.Value;
            if (cell.HasFormula) formulas[address] = cell.Formula!;
        }

        return new Scenario
        {
            Name = name,
            Comment = comment,
            Range = range,
            Values = values,
            Formulas = formulas,
        };
    }

    public IReadOnlyDictionary<CellAddress, ScenarioCell> Apply(
        CalculationEngine engine, int sheetIndex, Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(scenario);

        var sheet = engine.Workbook.Sheets[sheetIndex];
        var previous = new Dictionary<CellAddress, ScenarioCell>();
        var target = new Dictionary<CellAddress, ScenarioCell>();

        foreach (var address in scenario.Addresses)
        {
            var cell = sheet[address];
            previous[address] = new ScenarioCell(cell.Value, cell.HasFormula ? cell.Formula : null);
            target[address] = scenario.CellAt(address);
        }

        Restore(engine, sheetIndex, target);
        return previous;
    }

    public void Restore(CalculationEngine engine, int sheetIndex, IReadOnlyDictionary<CellAddress, ScenarioCell> cells)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(cells);

        foreach (var (address, cell) in cells)
        {
            var target = new SheetCell(sheetIndex, address);
            if (cell.HasFormula) engine.SetFormula(target, cell.Formula!);
            else engine.SetValue(target, cell.Value);
        }
    }

    public Scenario? Find(Worksheet sheet, string name)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.Scenarios.FirstOrDefault(
            scenario => string.Equals(scenario.Name, name, StringComparison.CurrentCultureIgnoreCase));
    }
}
