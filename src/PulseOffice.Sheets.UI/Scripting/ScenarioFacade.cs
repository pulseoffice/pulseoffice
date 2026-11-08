using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class ScenarioFacade(IScriptDispatcher dispatcher, IActiveSheetContext context, IScenarioManager scenarios, IScriptSheetEdits edits, IScriptReferences references, IScriptProtection protection) : ScriptFacade(dispatcher)
{
    public string[] Names => Read<string[]>(() =>
        Sheet() is { } sheet ? [.. sheet.Scenarios.Select(scenario => scenario.Name)] : []);

    public int Count => Read(() => Sheet()?.Scenarios.Count ?? 0);

    public bool Capture(string name, string reference, string comment = "") => Write(() => edits.Record("Scenario", () =>
    {
        var range = references.Range(reference);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The scenario name is empty.", nameof(name));
        protection.Unprotected("adding scenarios");
        if (context.Engine is not { } engine || Sheet() is not { } sheet) return false;

        var captured = scenarios.Capture(
            engine, context.SheetIndex, range, name, string.IsNullOrEmpty(comment) ? null : comment);

        sheet.Scenarios.RemoveAll(scenario => scenario.Name == name);
        sheet.Scenarios.Add(captured);

        return true;
    }, done => done));

    public bool Apply(string name) => Write(() => edits.Record("Scenario", () =>
    {
        if (context.Engine is not { } engine || Sheet() is not { } sheet) return false;
        var scenario = Existing(sheet, name);
        protection.Cells(scenario.Range);

        scenarios.Apply(engine, context.SheetIndex, scenario);

        context.RequestRefresh();

        return true;
    }, done => done));

    public bool Remove(string name) => Write(() => edits.Record("Scenario", () =>
    {
        protection.Unprotected("removing scenarios");
        if (Sheet() is not { } sheet) return false;

        var removed = sheet.Scenarios.RemoveAll(scenario => scenario.Name == name) > 0;

        if (removed) context.RequestRefresh();

        return removed;
    }, done => done));

    public string RangeOf(string name) => Read(() =>
        Sheet() is { } sheet ? Existing(sheet, name).Range.ToString() : string.Empty);

    private Scenario Existing(Worksheet sheet, string name) => scenarios.Find(sheet, name)
        ?? throw new ArgumentException(
            $"There is no scenario '{name}' on this sheet: the scenarios are {string.Join(", ", sheet.Scenarios.Select(scenario => scenario.Name))}.", nameof(name));

    private Worksheet? Sheet() =>
        context.Workbook is { } workbook ? workbook.Sheets[context.SheetIndex] : null;
}
