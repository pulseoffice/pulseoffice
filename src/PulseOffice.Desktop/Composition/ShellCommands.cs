using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.UI;
using PulseOffice.Presentations.UI;
using PulseOffice.Shell;
using PulseOffice.Settings;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellCommands(
    IStatusBarService status,
    PulseOffice.UI.Controls.Settings.ISettingsDialog settingsDialog,
    PulseOffice.Shell.About.IAboutDialog about,
    PulseOffice.Shell.Updates.IUpdateCoordinator updates,
    IDocumentWorkspace workspace,
    ILedgerDocumentFactory ledgerDocuments,
    IDeckDocumentFactory deckDocuments,
    PulseOffice.Documents.UI.Shell.IFolioDocumentFactory folioDocuments,
    IDocumentOpenRouter openRouter,
    IDocumentFileServices files,
    Func<IShellPresenter> presenter,
    Func<CommandExecutor> executor,
    Func<IUnsavedChangesGuard> unsaved,
    ITextCatalog texts,
    PulseOffice.Platform.IHostSystem host)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        New(ModuleKind.Documents), New(ModuleKind.Sheets), New(ModuleKind.Presentations),

        new CommandDefinition
        {
            Id = new CommandId("shell.view.commandPalette"),
            MenuPath = "view",
            DefaultGesture = "Ctrl+Shift+P",
            Surface = CommandSurface.Menu | CommandSurface.Palette,
            Execute = (_, _) => presenter().ShowCommandPaletteAsync(),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.help.shortcuts"),
            MenuPath = "help",
            DefaultGesture = "F1",
            Surface = CommandSurface.Menu | CommandSurface.Palette,
            Execute = (_, _) => presenter().ShowShortcutsAsync(),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.open"),
            MenuPath = "file",
            DefaultGesture = "Ctrl+O",
            Surface = CommandSurface.Common,
            Execute = async (_, ct) => await openRouter.OpenDialogAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.save"),
            MenuPath = "file",
            DefaultGesture = "Ctrl+S",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Savable(workspace.Active),
            Execute = async (_, ct) =>
            {
                if (workspace.Active is not { } document || files.For(document) is not { } service) return;
                await service.SaveAsync(document, ct);
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.saveAs"),
            MenuPath = "file",
            DefaultGesture = "Ctrl+Shift+S",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Savable(workspace.Active),
            Execute = async (_, ct) =>
            {
                if (workspace.Active is not { } document || files.For(document) is not { } service) return;
                await service.SaveAsAsync(document, ct);
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.saveAsOffice"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Savable(workspace.Active),
            Execute = async (_, ct) =>
            {
                if (workspace.Active is not { } document || files.For(document) is not { } service) return;
                await service.SaveInterchangeAsync(document, ct);
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.saveCopy"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Savable(workspace.Active),
            Execute = async (_, ct) =>
            {
                if (workspace.Active is not { } document || files.For(document) is not { } service) return;
                if (await service.SaveCopyAsync(document, ct))
                    status.Report(texts["status.file.copySaved"], NotificationSeverity.Success);
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.saveAll"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Documents.Any(document => document.IsModified && Savable(document)),
            Execute = async (_, ct) =>
            {
                var saved = 0;
                foreach (var document in workspace.Documents.Where(open => open.IsModified && Savable(open)).ToList())
                {
                    await SaveByModule(document, ct);
                    if (!document.IsModified) saved++;
                }
                status.Report(texts.Format("status.file.allSaved", saved), NotificationSeverity.Success);
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.reload"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active?.Path is { Length: > 0 },
            Execute = async (_, ct) =>
            {
                if (workspace.Active is not { Path: { Length: > 0 } path } document) return;
                if (!await unsaved().ConfirmCloseAsync([document], ct)) return;
                workspace.Close(document);
                await openRouter.OpenAsync(path, ct);
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.edit.undo"),
            MenuPath = "edit",
            DefaultGesture = "Ctrl+Z",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.CanUndo,
            Execute = (_, _) =>
            {
                if (workspace.Active is { } document && document.History.CanUndo)
                {
                    var what = document.History.UndoDescription;
                    document.History.Undo();
                    status.Report(texts.Format("status.undone", Described(what)));
                }

                return Task.CompletedTask;
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.edit.redo"),
            MenuPath = "edit",

            DefaultGesture = host.IsMacOS ? "Ctrl+Shift+Z" : "Ctrl+Y",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.CanRedo,
            Execute = (_, _) =>
            {
                if (workspace.Active is { } document && document.History.CanRedo)
                {
                    var what = document.History.RedoDescription;
                    document.History.Redo();
                    status.Report(texts.Format("status.redone", Described(what)));
                }

                return Task.CompletedTask;
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.edit.repeat"),
            MenuPath = "edit",
            DefaultGesture = "Ctrl+Shift+Y",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = context =>
                executor().LastRepeatable is { } last && last.IsApplicable(context),
            Execute = async (context, ct) =>
            {
                if (executor().LastRepeatable is not { } last) return;

                await executor().ExecuteAsync(last, context, ct).ConfigureAwait(true);
            },
        },


        new CommandDefinition
        {
            Id = new CommandId("shell.tools.options"),
            MenuPath = "tools",
            DefaultGesture = "Ctrl+Comma",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => settingsDialog.ShowAsync(ct),
        },

        Cycle("shell.window.nextDocument", "Ctrl+Alt+PageDown", 1),

        Cycle("shell.window.previousDocument", "Ctrl+Alt+PageUp", -1),

        new CommandDefinition
        {
            Id = new CommandId("shell.help.about"),
            MenuPath = "help",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => about.ShowAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId(PulseOffice.Shell.Updates.UpdateCoordinator.CommandId),
            MenuPath = "help",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => updates.CheckNowAsync(ct),
        },
    ];

    private string? Described(string? what) => what is not null && texts.Contains("history.folio." + what) ? texts["history.folio." + what] : what;

    private bool Savable(PulseOffice.Shell.OpenDocument? document) => files.For(document) is not null;

    private Task SaveByModule(PulseOffice.Shell.OpenDocument document, CancellationToken ct) =>
        files.For(document)?.SaveAsync(document, ct) ?? Task.CompletedTask;

    private CommandDefinition Cycle(string id, string gesture, int step) => new()
    {
        Id = new CommandId(id),
        MenuPath = "view",
        DefaultGesture = gesture,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => workspace.Documents.Count > 1,
        Execute = (_, _) =>
        {
            var documents = workspace.Documents;
            if (documents.Count > 1 && workspace.Active is { } active)
            {
                var index = documents.ToList().IndexOf(active);
                workspace.Activate(documents[(index + step + documents.Count) % documents.Count]);
            }
            return Task.CompletedTask;
        },
    };

    private CommandDefinition New(ModuleKind module)
    {
        var identity = ModuleIdentity.For(module);

        return new CommandDefinition
        {
            Id = new CommandId($"shell.file.new.{module.ToString().ToLowerInvariant()}"),
            MenuPath = "file/new",

            Surface = CommandSurface.Advanced,
            Execute = (_, _) =>
            {
                if (module == ModuleKind.Sheets)
                {
                    workspace.Open(ledgerDocuments.CreateEmpty());
                    status.Report(texts.Format("status.document.created", identity.BrandName));
                }
                else if (module == ModuleKind.Presentations)
                {
                    workspace.Open(deckDocuments.CreateEmpty());
                    status.Report(texts.Format("status.document.created", identity.BrandName));
                }
                else
                {
                    workspace.Open(folioDocuments.CreateEmpty());
                    status.Report(texts.Format("status.document.created", identity.BrandName));
                }

                return Task.CompletedTask;
            },
        };
    }

    private CommandDefinition Simple(
        string id, string menuGroup, string? ribbonPath, string? gesture,
        CommandSurface surface,
        DocumentState required = DocumentState.None,
        DocumentState forbidden = DocumentState.None) =>
        new()
        {
            Id = new CommandId(id),
            MenuPath = menuGroup,
            RibbonPath = ribbonPath,
            DefaultGesture = gesture,
            Surface = surface,
            RequiredState = required,
            ForbiddenState = forbidden,
            Execute = (_, _) =>
            {
                status.Report(texts.Format("status.notImplemented", texts[$"command.{id}.title"]), NotificationSeverity.Warning);
                return Task.CompletedTask;
            },
        };
}
