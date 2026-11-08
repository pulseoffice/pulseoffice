using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ScriptCommands(
    CommandRegistry registry,
    Func<CommandExecutor> executor,
    IDocumentWorkspace workspace,
    IScriptableCommandPolicy policy) : IScriptCommands
{
    public bool Run(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("A command identifier is required, such as 'shell.file.save'.", nameof(command));
        var id = new CommandId(command.Trim());
        if (registry.Find(id) is not { } definition)
            throw new ArgumentException($"There is no command '{id.Value}'.", nameof(command));
        if (!policy.Allows(definition))
            throw new InvalidOperationException($"The command '{id.Value}' cannot be run from a macro.");
        if (!definition.IsApplicable(workspace.Context)) return false;

        _ = executor().ExecuteAsync(definition, workspace.Context);
        return true;
    }
}

public sealed class ShellMacroRecorderCommands(
    IDocumentWorkspace workspace,
    ICommandRecorder recorder,
    IMacroScriptWriter writer,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.tools.recordMacro"),
            IconKey = "shell.tools.recordMacro",
            MenuPath = "tools/macros",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => recorder.IsRecording || (workspace.Active?.Model is IMacroDocument && workspace.Active is { IsReadOnly: false }),
            Execute = (_, ct) => ToggleAsync(ct),
        },
    ];

    private async Task ToggleAsync(CancellationToken ct)
    {
        if (!recorder.IsRecording)
        {
            recorder.Start();
            status.Report(texts["macro.recording.on"]);
            return;
        }

        var recorded = recorder.Stop();
        if (recorded.Count == 0)
        {
            status.Report(texts["macro.recording.empty"], NotificationSeverity.Warning);
            return;
        }

        if (workspace.Active is not { Model: IMacroDocument macros } document) return;

        string name;
        var explanation = texts.Format("macro.recording.save", recorded.Count);

        while (true)
        {
            var values = await dialogs.ShowAsync<Dictionary<string, object>>(
                new FormPromptView(
                    explanation,
                    [new TextField("name", texts["macro.recording.name"], NextName(macros.Macros))],
                    texts),
                new DialogOptions { TitleKey = "command.shell.tools.recordMacro.title" }, ct).ConfigureAwait(true);

            if (values is null || (values["name"] as string)?.Trim() is not { Length: > 0 } chosen) return;
            if (macros.Macros.Find(chosen) is null)
            {
                name = chosen;
                break;
            }

            explanation = texts.Format("macro.recording.exists", chosen);
        }

        new RecordedMacroLibrary(macros.Macros, document.History, texts["macro.recording.history"])
            .Store(new DocumentMacro(name, MacroLanguage.JavaScript, writer.Write(recorded)));

        status.Report(texts.Format("macro.recording.saved", name), NotificationSeverity.Success);
    }

    private string NextName(IMacroLibrary library)
    {
        var stem = texts["macro.recording.stem"];
        var index = 1;
        while (library.Find($"{stem} {index}") is not null) index++;
        return $"{stem} {index}";
    }
}
