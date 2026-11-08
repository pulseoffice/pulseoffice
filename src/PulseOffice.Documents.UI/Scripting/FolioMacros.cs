using Microsoft.Extensions.Logging;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Macros;

namespace PulseOffice.Documents.UI.Scripting;

public sealed class FolioMacroEventsSource(
    IActiveFolioContext context,
    IScriptApiProvider api,
    FolioScriptGlobals globals,
    IMacroEditorLauncher editor,
    ITextCatalog texts) : IMacroEventsContextSource
{
    public MacroEditorRequest? Request() =>
        context.Target?.Editor is { } target
            ? new MacroEditorRequest(ModuleKind.Documents, globals.Globals, api.Catalog, target.Document.Macros,
                "command.folio.macro.editor.title", target.History, "macro.sample.folio")
            : null;

    public MacroEventsContext? Create(bool readOnly)
    {
        if (context.Target?.Editor is not { } target || Request() is not { } request) return null;
        return new MacroEventsContext(
            new RecordedMacroLibrary(target.Document.Macros, target.History, texts["history.macro"]),
            new MacroEditorGateway(editor, request),
            readOnly);
    }
}

public sealed class FolioMacroCommands(
    IActiveFolioContext context,
    FolioMacroEventsSource source,
    IMacroEditorLauncher editor,
    IFolioObjectEditing objects,
    IFormControlDialogs dialogs,
    IFolioForms forms,
    ITextCatalog texts) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Macro("folio.macro.editor") with { DefaultGesture = "Ctrl+Alt+M" },
        Editing("folio.form.properties") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => FormPropertiesAsync(ct) },
        Editing("folio.form.controlProperties") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } target && !target.ReadOnly() && Control(target) is not null,
            Execute = (_, ct) => PropertiesAsync(ct),
        },
    ];

    private CommandDefinition Macro(string id) =>
        Viewing(id) with
        {
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => source.Request() is { } request ? editor.ShowAsync(request, ct) : Task.CompletedTask,
        };

    private FormControlObject? Control(FolioEditor target) =>
        objects.Selected(target) is { Content: EmbeddedContent { Value: FormControlObject control } } ? control : null;

    private async Task FormPropertiesAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var before = editor.Document.FormSettings;
        if (await forms.AskAsync("command.folio.form.properties.title", "folio.formProperties.explanation",
            [
                new PulseOffice.UI.Controls.TextField("source", texts["folio.formProperties.source"], before.DataSource ?? string.Empty),
                new PulseOffice.UI.Controls.TextField("command", texts["folio.formProperties.command"], before.Command ?? string.Empty),
                new PulseOffice.UI.Controls.CheckField("focus", texts["folio.formProperties.focus"], before.AutomaticFocus),
                new PulseOffice.UI.Controls.CheckField("design", texts["folio.formProperties.design"], before.OpenInDesignMode),
            ], ct).ConfigureAwait(true) is not { } values || Editor is not { } current) return;
        var after = new FormDocumentSettings(values.Flag("design"), values.Flag("focus"),
            values.Text("source").Trim() is { Length: > 0 } source ? source : null,
            values.Text("command").Trim() is { Length: > 0 } command ? command : null);
        if (after == before) return;
        var document = current.Document;
        document.FormSettings = after;
        current.History.Push(new DelegateAction(texts["command.folio.form.properties.title"], () => document.FormSettings = after, () => document.FormSettings = before));
    }

    private async Task PropertiesAsync(CancellationToken ct)
    {
        if (Editor is not { } target || Control(target) is not { } control) return;
        if (source.Create(readOnly: false) is not { } events) return;
        if (await dialogs.EditAsync(control, events, "command.folio.form.controlProperties.title", ct).ConfigureAwait(true) is not { } properties) return;
        if (Editor is not { } current) return;
        objects.Replace(current, content => content is FormControlObject same ? same with { Properties = properties } : content);
    }
}

public interface IFolioFormEvents
{
    bool Activate(FolioEditor editor, string objectId);
}

public sealed class FolioFormEvents(
    IFormDesignMode design,
    FolioScriptGlobals globals,
    IEnumerable<IScriptEngine> engines,
    PulseOffice.UI.Controls.Macros.IDocumentMacroGate gate,
    IScriptExecutionPermissions permissions,
    IMacroPermissionPolicy policy,
    PulseOffice.Scripting.Editor.IGuardedMacroRuns runs,
    ILogger<FolioFormEvents> logger) : IFolioFormEvents
{
    public bool Activate(FolioEditor editor, string objectId)
    {
        if (design.IsDesigning || editor.Document.Objects.Find(objectId) is not { Content: EmbeddedContent { Value: FormControlObject control } }) return false;
        if (control.Properties.MacroFor(FormControlEvent.Clicked) is not { } name) return false;
        if (editor.Document.Macros.Find(name) is not { } macro)
        {
            logger.LogWarning("Control {Control} refers to a missing macro: {Macro}", control.Properties.Name, name);
            return true;
        }
        var language = macro.Language == MacroLanguage.Python ? ScriptLanguage.Python : ScriptLanguage.JavaScript;
        if (engines.FirstOrDefault(candidate => candidate.Language == language) is not { } engine)
        {
            logger.LogWarning("No engine for {Language}, macro {Macro} was not run", language, macro.Name);
            return true;
        }
        _ = RunAsync(editor.Document, macro.Name, editor.History, engine, macro.Source, control.Properties.Name);
        return true;
    }

    private async Task RunAsync(PulseOffice.Security.IMacroSignatureHolder document, string name, UndoStack history, IScriptEngine engine, string source, string control)
    {
        if (!await gate.AllowAsync(document, name).ConfigureAwait(true)) return;

        var scope = runs.Begin(history);
        try
        {
            var allowed = policy.For(engine.Language, source, ScriptPermissions.Sandboxed);
            using var sandbox = permissions.Enter(allowed);
            var result = await engine.ExecuteAsync(source, new ScriptContext
            {
                Module = ModuleKind.Documents,
                Globals = new Dictionary<string, object>(globals.Globals)
                {
                    ["control"] = control,
                    ["event"] = FormControlEvent.Clicked.ToString(),
                },
                Permissions = allowed,
            }).ConfigureAwait(true);
            if (!result.Succeeded)
                logger.LogWarning("Macro of control {Control} failed: {Diagnostics}", control,
                    string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
        finally
        {
            scope.Dispose();
        }
    }
}
