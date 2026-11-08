using PulseOffice.Document.Commands;
using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellFormCommands(
    IDocumentWorkspace workspace,
    IFormDesignMode design,
    IStatusBarService status,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.form.designMode"),
            IconKey = "shell.form.designMode",
            MenuPath = "tools/forms",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Forms() is not null,
            Execute = (_, _) =>
            {
                design.Toggle();
                status.Report(texts[design.IsDesigning ? "form.design.on" : "form.design.off"]);
                return Task.CompletedTask;
            },
        },
        Setting("shell.form.automaticFocus",
            settings => settings with { AutomaticFocus = !settings.AutomaticFocus },
            settings => settings.AutomaticFocus ? "form.focus.on" : "form.focus.off"),
        Setting("shell.form.openInDesignMode",
            settings => settings with { OpenInDesignMode = !settings.OpenInDesignMode },
            settings => settings.OpenInDesignMode ? "form.openInDesign.on" : "form.openInDesign.off"),
    ];

    private IFormDocument? Forms() => workspace.Active?.Model as IFormDocument;

    private CommandDefinition Setting(string id, Func<FormDocumentSettings, FormDocumentSettings> change, Func<FormDocumentSettings, string> message) =>
        new()
        {
            Id = new CommandId(id),
            IconKey = id,
            MenuPath = "tools/forms",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Forms() is not null,
            Execute = (_, _) =>
            {
                if (workspace.Active is not { } document || Forms() is not { } forms) return Task.CompletedTask;

                var before = forms.FormSettings;
                var after = change(before);
                forms.FormSettings = after;
                document.History.Push(new DelegateAction(
                    texts[$"command.{id}.title"],
                    () => forms.FormSettings = after,
                    () => forms.FormSettings = before));
                status.Report(texts[message(after)]);
                return Task.CompletedTask;
            },
        };
}
