using System.Globalization;
using PulseOffice.Formats;
using PulseOffice.Localization;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellInterfaceCommands(
    ISidePanelHost panel,
    PulseOffice.UI.Controls.Shortcuts.ShortcutEditorServices shortcutEditor,
    PulseOffice.UI.Commands.Shortcuts.IShortcutCustomization shortcutCustomization,
    ISettingsService settings,
    IDialogService dialogs,
    IStatusBarService status,
    Func<IShellPresenter> presenter,
    IDocumentWorkspace workspace,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.close"),
            MenuPath = "file",
            DefaultGesture = "Ctrl+W",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            Execute = (_, _) => presenter().CloseActiveDocumentAsync(),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.quit"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            Execute = (_, _) =>
            {
                presenter().RequestQuit();
                return Task.CompletedTask;
            },
        },

        Toggle("shell.view.statusBar", ShellSettingKeys.StatusBarVisible),

        new CommandDefinition
        {
            Id = new CommandId("shell.view.sidebar"),
            MenuPath = "view",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            Execute = (_, _) =>
            {
                panel.Toggle();
                return Task.CompletedTask;
            },
        },

        Dialog("shell.view.toolbars", "view", ToolbarsAsync),
        new CommandDefinition
        {
            Id = new CommandId("shell.view.fullScreen"),
            MenuPath = "view",
            DefaultGesture = "Ctrl+Shift+J",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            CheckedWhen = _ => Window() is { WindowState: Avalonia.Controls.WindowState.FullScreen },
            Execute = (_, _) =>
            {
                if (Window() is { } window)
                    window.WindowState = window.WindowState == Avalonia.Controls.WindowState.FullScreen
                        ? Avalonia.Controls.WindowState.Normal
                        : Avalonia.Controls.WindowState.FullScreen;
                return Task.CompletedTask;
            },
        },
        new CommandDefinition
        {
            Id = new CommandId("shell.tools.customize"),
            MenuPath = "tools",
            Surface = CommandSurface.Advanced,
            Keywords = ["shortcut", "keyboard", "hotkey", "keybinding"],
            Execute = (ctx, ct) => CustomizeAsync(ctx.Module, ct),
        },
    ];

    private Avalonia.Controls.Window? Window() =>
        workspace.Active?.View is { } view ? Avalonia.Controls.TopLevel.GetTopLevel(view) as Avalonia.Controls.Window : null;

    private CommandDefinition Toggle(string id, string key) => new()
    {
        Id = new CommandId(id),
        MenuPath = "view",
        Surface = CommandSurface.Advanced,
        Execute = (_, _) =>
        {
            settings.Set(key, !settings.Get<bool>(key));
            return Task.CompletedTask;
        },
    };

    private static CommandDefinition Dialog(string id, string path, Func<CancellationToken, Task> run) => new()
    {
        Id = new CommandId(id),
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        Execute = (_, ct) => run(ct),
    };

    private async Task CustomizeAsync(PulseOffice.Foundation.Modules.ModuleKind module, CancellationToken ct)
    {
        var edits = await dialogs.ShowAsync<IReadOnlyDictionary<string, string?>>(
            new PulseOffice.UI.Controls.Shortcuts.ShortcutEditorView(module, shortcutEditor),
            new DialogOptions
            {
                TitleKey = "command.shell.tools.customize.title",
                PreferredWidth = 1040,
                PreferredHeight = 720,
                CanCancelWithEscape = false,
            },
            ct).ConfigureAwait(true);

        if (edits is null || edits.Count == 0) return;

        shortcutCustomization.Apply(edits);

        status.Report(texts.Format("customize.done", edits.Count));
    }

    private async Task ToolbarsAsync(CancellationToken ct)
    {
        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["toolbars.explanation"],
                [
                    new CheckField("ribbon", texts["toolbars.ribbon"], settings.Get<bool>(ShellSettingKeys.RibbonVisible)),
                    new CheckField("statusBar", texts["toolbars.statusBar"], settings.Get<bool>(ShellSettingKeys.StatusBarVisible)),
                    new CheckField("tabs", texts["toolbars.tabs"], settings.Get<bool>(ShellSettingKeys.DocumentTabsVisible)),
                ],
                texts),
            new DialogOptions { TitleKey = "command.shell.view.toolbars.title", PreferredWidth = 460 },
            ct).ConfigureAwait(true);

        if (answers is null) return;

        settings.Set(ShellSettingKeys.RibbonVisible, answers["ribbon"] is true);
        settings.Set(ShellSettingKeys.StatusBarVisible, answers["statusBar"] is true);
        settings.Set(ShellSettingKeys.DocumentTabsVisible, answers["tabs"] is true);

        status.Report(texts["toolbars.done"]);
    }
}
