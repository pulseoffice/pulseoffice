using PulseOffice.Scripting.Editor.Facades;
namespace PulseOffice.Sheets.UI.Scripting;

public interface IScriptGlobals : PulseOffice.Scripting.IModuleScriptGlobals
{
    SheetFacade Sheet { get; }
}

public sealed class ScriptGlobals(
    IScriptDispatcher dispatcher,
    PulseOffice.Scripting.IScriptOutput output,
    PulseOffice.UI.Controls.Services.IStatusBarService status,
    LedgerScriptHost host,
    IScriptCommands commands,
    DocumentFacade document,
    SheetFacade sheet,
    PivotFacade pivot,
    PrintFacade print,
    FormatRuleFacade rules,
    ValidationFacade validation,
    FilterFacade filter,
    ScenarioFacade scenarios,
    PulseOffice.Scripting.ScriptFilesFacade files) : IScriptGlobals
{
    public PulseOffice.Foundation.Modules.ModuleKind Module => PulseOffice.Foundation.Modules.ModuleKind.Sheets;

    public SheetFacade Sheet { get; } = sheet;

    public IReadOnlyDictionary<string, object> Globals { get; } = new Dictionary<string, object>
    {
        ["app"] = new AppFacade(dispatcher, output, status, host, commands),
        ["doc"] = document,
        ["sheet"] = sheet,
        ["pivot"] = pivot,
        ["page"] = print,
        ["rules"] = rules,
        ["validation"] = validation,
        ["filter"] = filter,
        ["scenarios"] = scenarios,
        ["files"] = files,
    };
}
