using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.UI;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellRemoteCommands(
    IDocumentWorkspace workspace,
    IDocumentOpenRouter openRouter,
    IEnumerable<IDocumentSaver> savers,
    IDocumentOperationsCatalog operations,
    IRemoteConnectionDirectory connections,
    IRemoteStorageProvider storages,
    IRemoteDocuments tracked,
    IRemoteSignIn signIn,
    PulseOffice.UI.Icons.IIconCatalog icons,
    IRemoteCredentials credentials,
    IRemoteFileNames fileNames,
    IRemoteUploads uploads,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.openRemote"),
            MenuPath = "file/remote",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => OpenAsync(ct),
        },
        Active("shell.file.saveAsRemote", _ => true, SaveAsync),
        Active("shell.file.checkOut", document => document is { IsCheckedOut: false }, CheckOutAsync),
        Active("shell.file.cancelCheckOut", document => document is { IsCheckedOut: true }, CancelAsync),
        Active("shell.file.checkIn", document => document is { IsCheckedOut: true }, CheckInAsync),
    ];

    private CommandDefinition Active(
        string id, Func<RemoteDocument?, bool> applies, Func<CancellationToken, Task> run) => new()
    {
        Id = new CommandId(id),
        MenuPath = "file/remote",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => workspace.Active is not null && Applies(id, applies),
        Execute = (_, ct) => run(ct),
    };

    private bool Applies(string id, Func<RemoteDocument?, bool> applies)
    {
        if (id == "shell.file.saveAsRemote") return true;

        return Current() is { } document
            && storages.For(document.Connection.Protocol).SupportsCheckOut
            && applies(document);
    }

    private RemoteDocument? Current() =>
        workspace.Active?.Path is { } path ? tracked.For(path) : null;

    private Task<bool> SaveActiveAsync(OpenDocument document, CancellationToken ct) =>
        savers.FirstOrDefault(saver => saver.CanSave(document)) is { } saver
            ? saver.SaveAsync(document, ct)
            : Task.FromResult(false);

    private async Task<RemoteChoice?> ChooseAsync(bool saving, string? suggested, CancellationToken ct) =>
        await dialogs.ShowAsync<RemoteChoice>(
            new RemoteBrowserView(connections, storages, credentials, fileNames, texts, diagnostics, saving, suggested, signIn, icons),
            new DialogOptions
            {
                TitleKey = saving ? "remote.save.title" : "remote.open.title",
                PreferredWidth = 620,
            },
            ct).ConfigureAwait(true);

    private async Task OpenAsync(CancellationToken ct)
    {
        if (await ChooseAsync(saving: false, null, ct).ConfigureAwait(true) is not { } choice) return;

        if (tracked.Following(choice.Connection, choice.Path) is { } known
            && workspace.Documents.FirstOrDefault(document => string.Equals(document.Path, known.LocalPath, StringComparison.Ordinal)) is { } open)
        {
            workspace.Activate(open);
            return;
        }

        var storage = storages.For(choice.Connection.Protocol);
        var activity = status.BeginActivity(texts.Format("remote.downloading", choice.Path));

        try
        {
            var read = await storage.ReadAsync(choice.Connection, choice.Path, ct).ConfigureAwait(true);

            if (!read.Succeeded)
            {
                status.Report(diagnostics.Describe(read.Diagnostics[0]), NotificationSeverity.Error);
                return;
            }

            var folder = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(), "pulseoffice-remote", Guid.NewGuid().ToString("N")));

            var name = choice.Path.TrimEnd('/', '\\').Split('/', '\\').LastOrDefault(part => part.Length > 0) ?? string.Empty;
            name = string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            if (name.Length == 0 || name is "." or "..") name = "document";
            var local = Path.Combine(folder.FullName, name);

            await File.WriteAllBytesAsync(local, read.Value!.Bytes, ct).ConfigureAwait(true);

            tracked.Follow(new RemoteDocument
            {
                LocalPath = local,
                Connection = choice.Connection,
                RemotePath = choice.Path,
                Version = read.Value!.Version,
            });

            await openRouter.OpenAsync(local, ct).ConfigureAwait(true);

            status.Report(texts.Format("remote.opened", choice.Path));
        }
        finally
        {
            status.EndActivity(activity);
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        if (workspace.Active is not { } active) return;

        var extension = operations.For(active)?.NativeExtension ?? "odf";
        var suggested = Path.GetFileName(active.Path ?? $"{active.Title}.{extension}");

        if (await ChooseAsync(saving: true, suggested, ct).ConfigureAwait(true) is not { } choice) return;

        var saved = await SaveActiveAsync(active, ct).ConfigureAwait(true);

        if (!saved || active.IsModified || active.Path is not { } local || !File.Exists(local))
        {
            status.Report(texts["remote.saveFirst"], NotificationSeverity.Warning, NotificationRemedies.Save);
            return;
        }

        var known = tracked.For(local);
        var sameTarget = known is not null
            && string.Equals(tracked.Following(choice.Connection, choice.Path)?.LocalPath, local, StringComparison.OrdinalIgnoreCase);

        var uploaded = await UploadAsync(new RemoteUploadRequest(
            local, choice.Connection, choice.Path, await File.ReadAllBytesAsync(local, ct).ConfigureAwait(true),
            sameTarget ? known!.Held : null, sameTarget ? known!.Version : null), ct).ConfigureAwait(true);

        if (uploaded is not null) tracked.Follow(uploaded);
    }

    private async Task<RemoteDocument?> UploadAsync(RemoteUploadRequest request, CancellationToken ct)
    {
        var result = await uploads.UploadAsync(request, new BrowserCopyTarget(this), ct).ConfigureAwait(true);

        switch (result.Status)
        {
            case RemoteUploadStatus.Uploaded:
                status.Report(texts.Format("remote.uploaded", result.Document!.RemotePath), NotificationSeverity.Success);
                return result.Document;

            case RemoteUploadStatus.Failed:
                status.Report(diagnostics.Describe(result.Failure!), NotificationSeverity.Error);
                return null;

            default:
                status.Report(texts["remote.conflict.cancelled"], NotificationSeverity.Warning);
                return null;
        }
    }

    private sealed class BrowserCopyTarget(ShellRemoteCommands owner) : IRemoteCopyTarget
    {
        public Task<RemoteChoice?> ChooseAsync(RemoteConnection connection, string suggestedName, CancellationToken ct = default) =>
            owner.ChooseAsync(saving: true, suggestedName, ct);
    }

    private async Task CheckOutAsync(CancellationToken ct)
    {
        if (Current() is not { } document) return;

        var locked = await storages.For(document.Connection.Protocol)
            .LockAsync(document.Connection, document.RemotePath, ct).ConfigureAwait(true);

        if (!locked.Succeeded)
        {
            status.Report(diagnostics.Describe(locked.Diagnostics[0]), NotificationSeverity.Error);
            return;
        }

        tracked.Follow(document with { Held = locked.Value });

        status.Report(texts.Format("remote.checkedOut", document.RemotePath), NotificationSeverity.Success);
    }

    private async Task CancelAsync(CancellationToken ct)
    {
        if (Current() is not { Held: { } held } document) return;

        var released = await storages.For(document.Connection.Protocol)
            .UnlockAsync(document.Connection, held, ct).ConfigureAwait(true);

        tracked.Follow(document with { Held = null });

        status.Report(released.Succeeded
            ? texts.Format("remote.checkOutCancelled", document.RemotePath)
            : diagnostics.Describe(released.Diagnostics[0]));
    }

    private async Task CheckInAsync(CancellationToken ct)
    {
        if (workspace.Active is not { Path: { } path } active) return;
        if (tracked.For(path) is not { Held: { } held } document) return;

        if (!await SaveActiveAsync(active, ct).ConfigureAwait(true) || active.IsModified)
        {
            status.Report(texts["remote.saveFirst"], NotificationSeverity.Warning, NotificationRemedies.Save);
            return;
        }

        var uploaded = await UploadAsync(new RemoteUploadRequest(
            document.LocalPath, document.Connection, document.RemotePath,
            await File.ReadAllBytesAsync(document.LocalPath, ct).ConfigureAwait(true), held, document.Version), ct).ConfigureAwait(true);

        if (uploaded is null) return;

        var released = await storages.For(document.Connection.Protocol)
            .UnlockAsync(document.Connection, held, ct).ConfigureAwait(true);

        tracked.Follow(uploaded with { Held = null });

        status.Report(released.Succeeded
            ? texts.Format("remote.checkedIn", document.RemotePath)
            : diagnostics.Describe(released.Diagnostics[0]), NotificationSeverity.Success);
    }
}
