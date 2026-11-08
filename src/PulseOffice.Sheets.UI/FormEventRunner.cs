using Microsoft.Extensions.Logging;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;

namespace PulseOffice.Sheets.UI;

public interface IFormEventRunner
{
    Task<bool> RaiseAsync(
        FormControlProperties control, FormControlEvent raised, CancellationToken ct = default);
}

public sealed class FormEventRunner(
    Scripting.IScriptGlobals globals,
    IActiveSheetContext context,
    IEnumerable<IScriptEngine> engines,
    PulseOffice.UI.Controls.Macros.IDocumentMacroGate gate,
    IScriptExecutionPermissions permissions,
    IMacroPermissionPolicy policy,
    ILogger<FormEventRunner> logger) : IFormEventRunner
{
    public async Task<bool> RaiseAsync(
        FormControlProperties control, FormControlEvent raised, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (control.MacroFor(raised) is not { } name) return true;
        if (context.Workbook is not { } workbook) return true;
        if (workbook.Macros.Find(name) is not { } macro)
        {
            logger.LogWarning("Control {Control} refers to a missing macro: {Macro}", control.Name, name);
            return true;
        }

        if (!await gate.AllowAsync(workbook, macro.Name, ct).ConfigureAwait(true))
            return raised != FormControlEvent.Approve;

        var language = macro.Language == MacroLanguage.Python ? ScriptLanguage.Python : ScriptLanguage.JavaScript;
        var engine = engines.FirstOrDefault(candidate => candidate.Language == language);

        if (engine is null)
        {
            logger.LogWarning("No engine for {Language}, macro {Macro} was not run", language, macro.Name);
            return true;
        }

        var facade = globals.Sheet;

        var allowed = policy.For(language, macro.Source, ScriptPermissions.Sandboxed);
        using var scope = permissions.Enter(allowed);
        var result = await engine.ExecuteAsync(
            macro.Source,
            new ScriptContext
            {
                Module = ModuleKind.Sheets,
                Globals = new Dictionary<string, object>
                {
                    ["sheet"] = facade,
                    ["files"] = globals.Globals["files"],
                    ["control"] = control.Name,
                    ["event"] = raised.ToString(),
                },
                Permissions = allowed,
            },
            ct).ConfigureAwait(true);

        if (!result.Succeeded)
            logger.LogWarning(
                "Macro {Macro} failed: {Error}",
                macro.Name,
                string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return raised != FormControlEvent.Approve || (result.Succeeded && result.Value is not false);
    }
}
