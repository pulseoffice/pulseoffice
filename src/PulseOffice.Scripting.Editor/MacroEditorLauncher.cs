using Microsoft.Extensions.Logging;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Scripting.Editor;

public sealed record MacroEditorRequest(
    ModuleKind Module,
    IReadOnlyDictionary<string, object> Globals,
    ScriptApiCatalog Api,
    IMacroLibrary? Library,
    string TitleKey,
    PulseOffice.Document.Commands.UndoStack? History = null,
    string SampleKey = "macro.sample",
    string? OpenMacro = null);

public interface IMacroEditorLauncher
{
    Task ShowAsync(MacroEditorRequest request, CancellationToken ct);
}

public sealed class MacroEditorLauncher(
    IEnumerable<IScriptEngine> engines,
    IScriptOutput output,
    IScriptDocumentation documentation,
    IDialogService dialogs,
    ITextCatalog texts,
    IScriptExecutionPermissions permissions,
    IMacroPermissionPolicy policy,
    IMacroWriteAccess writeAccess,
    IGuardedMacroRuns runs,
    ILogger<MacroEditorLauncher> logger) : IMacroEditorLauncher
{
    public async Task ShowAsync(MacroEditorRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation(
            "Opening the macro editor for {Module} on macro {Macro} (dialog already open: {Nested})",
            request.Module, request.OpenMacro ?? "(none)", dialogs.IsDialogOpen);

        var view = new MacroEditorView(
            engines.Select(engine => new PermissionScopedScriptEngine(new LoggedScriptEngine(engine, logger), permissions)),
            (engine, source) => new ScriptContext
            {
                Module = request.Module,
                Globals = request.Globals.ToDictionary(pair => pair.Key, pair => pair.Value),
                Permissions = policy.For(engine.Language, source, ScriptPermissions.Sandboxed with { AllowsUnsandboxedEngines = true }),
            },
            request.Api,
            documentation,
            output,
            Texts(),
            request.Library is { } library && request.History is { } history
                ? new PulseOffice.Document.Commands.RecordedMacroLibrary(library, history, texts["history.macro"])
                : request.Library,
            request.History is { } undo ? () => runs.Begin(undo) : null,
            writeAccess)
        {
            Source = texts[request.SampleKey],
            Samples = new Dictionary<ScriptLanguage, string>
            {
                [ScriptLanguage.JavaScript] = texts[request.SampleKey],
                [ScriptLanguage.Python] = texts[request.SampleKey + ".python"],
            },
            Module = request.Module,
            OpenMacro = request.OpenMacro,
        };

        view.CloseRequested += (_, _) => dialogs.Close(null);

        await dialogs.ShowAsync(
            view,
            new DialogOptions
            {
                TitleKey = request.TitleKey,
                PreferredWidth = 1320,
                PreferredHeight = 760,
                CanCancelWithEscape = false,
            },
            ct).ConfigureAwait(true);
    }

    private Dictionary<string, string> Texts() => new()
    {
        ["run"] = texts["macro.run"],
        ["stop"] = texts["macro.stop"],
        [ScriptMessages.CancelledKey] = texts[ScriptMessages.CancelledKey],
        ["check"] = texts["macro.check"],
        ["close"] = texts["prompt.close"],
        ["output"] = texts["macro.output"],
        ["search"] = texts["macro.search"],
        ["guide"] = texts["macro.guide"],
        ["parameters"] = texts["macro.parameters"],
        ["guide.title"] = texts["macro.guide.title"],
        ["guide.editor"] = texts["macro.guide.editor"],
        ["guide.docs"] = texts["macro.guide.docs"],
        ["guide.language"] = texts["macro.guide.language"],
        ["guide.lists"] = texts["macro.guide.lists"],
        ["default"] = texts["macro.default"],
        ["runtime"] = texts["macro.runtime"],
        ["running"] = texts["macro.running"],
        ["runtimeUnsandboxed"] = texts["macro.runtime.unsandboxed"],
        ["done"] = texts["macro.done"],
        ["failed"] = texts["macro.failed"],
        ["noOutput"] = texts["macro.noOutput"],
        ["outputTruncated"] = texts["macro.output.truncated"],
        ["noProblems"] = texts["macro.noProblems"],
        [ScriptMessages.TimeoutKey] = texts[ScriptMessages.TimeoutKey],
        [ScriptMessages.MemoryKey] = texts[ScriptMessages.MemoryKey],
        [ScriptMessages.RecursionKey] = texts[ScriptMessages.RecursionKey],
        [ScriptMessages.PythonSandboxKey] = texts[ScriptMessages.PythonSandboxKey],
        [ScriptMessages.PythonUnavailableKey] = texts[ScriptMessages.PythonUnavailableKey],
        [ScriptMessages.CancelledKey] = texts[ScriptMessages.CancelledKey],
        [ScriptMessages.SyntaxKey] = texts[ScriptMessages.SyntaxKey],
        [ScriptMessages.FailureKey] = texts[ScriptMessages.FailureKey],
        [ScriptMessages.InternalKey] = texts[ScriptMessages.InternalKey],
        ["noTopic"] = texts["macro.noTopic"],
        ["macros"] = texts["macro.list"],
        ["name"] = texts["macro.name"],
        ["save"] = texts["macro.save"],
        ["newMacro"] = texts["macro.new"],
        ["duplicate"] = texts["macro.duplicate"],
        ["delete"] = texts["macro.delete"],
        ["saved"] = texts["macro.saved"],
        ["deleted"] = texts["macro.deleted"],
        ["unnamed"] = texts["macro.unnamed"],
        ["untitled"] = texts["macro.untitled"],
        ["empty"] = texts["macro.empty"],
        ["fileWrite.on"] = texts["macro.fileWrite.on"],
        ["fileWrite.off"] = texts["macro.fileWrite.off"],
        ["fileWrite.tip"] = texts["macro.fileWrite.tip"],
    };
}

public sealed class MacroEditorGateway(IMacroEditorLauncher launcher, MacroEditorRequest request)
    : PulseOffice.UI.Controls.Macros.IMacroEditorGateway
{
    public Task OpenAsync(string? macro, CancellationToken ct) =>
        launcher.ShowAsync(request with { OpenMacro = macro }, ct);
}

public interface IGuardedMacroRuns
{
    IDisposable Begin(PulseOffice.Document.Commands.UndoStack history);
}

public sealed class GuardedMacroRuns(PulseOffice.UI.Commands.ICommandEffectChecks effects, ITextCatalog texts) : IGuardedMacroRuns
{
    private readonly PulseOffice.UI.Commands.CommandDefinition _macroRun = new()
    {
        Id = new PulseOffice.UI.Commands.CommandId("macro.run"),
        Execute = (_, _) => Task.CompletedTask,
    };

    public IDisposable Begin(PulseOffice.Document.Commands.UndoStack history)
    {
        ArgumentNullException.ThrowIfNull(history);
        return new Run(effects.Open(_macroRun), history.BeginTransaction(texts["history.macro.run"]));
    }

    private sealed class Run(PulseOffice.UI.Commands.CommandEffectScope effects, IDisposable transaction) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                transaction.Dispose();
            }
            finally
            {
                effects.Finish();
            }
        }
    }
}
