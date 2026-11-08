using PulseOffice.Formats;
using PulseOffice.Formats.Intake;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Typography.Embedding;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public interface IModuleWriter<in TModel>
{
    FidelityReport CheckFidelity(TModel model);

    OperationResult<bool> Write(TModel model, Stream stream);
}

public sealed class DelegatingModuleWriter<TModel>(
    Func<TModel, FidelityReport> fidelity,
    Func<TModel, Stream, OperationResult<bool>> write) : IModuleWriter<TModel>
{
    public FidelityReport CheckFidelity(TModel model) => fidelity(model);

    public OperationResult<bool> Write(TModel model, Stream stream) => write(model, stream);
}

public interface IModuleFormats<TModel>
    where TModel : class
{
    IReadOnlyList<FileFilter> ReadableFilters { get; }

    IReadOnlyList<FileFilter> WritableFilters { get; }

    string NativeExtension { get; }

    string InterchangeExtension { get; }

    bool CanRead(string path);

    IModuleWriter<TModel>? WriterFor(string path);

    IModuleWriter<TModel>? NativeWriter { get; }

    OperationResult<TModel> Read(string path, Stream stream);
}

public sealed record DocumentServiceContext(
    IFileDialogService Dialogs,
    IAtomicFileWriter Files,
    IDocumentBuffer Buffers,
    IDocumentWorkspace Workspace,
    IStatusBarService Status,
    ITextCatalog Texts,
    IDiagnosticTexts DiagnosticTexts,
    IRecentDocuments Recent,
    ISignedSaveGuard SignedSave,
    IDocumentFontEmbedder FontEmbedder,
    IDocumentPasswordGate Passwords,
    IInterfaceLock Lock,
    PulseOffice.Foundation.Identity.IUserIdentity Identity);

public abstract class ModuleDocumentService<TModel>(IModuleFormats<TModel> formats, DocumentServiceContext context)
    : IWorkspaceOpener, IDocumentFileService
    where TModel : class
{
    public abstract ModuleKind Module { get; }

    protected PulseOffice.Foundation.Identity.IUserIdentity Identity => context.Identity;

    public IReadOnlyList<FileFilter> ReadableFilters => formats.ReadableFilters;

    public bool Handles(string path) => formats.CanRead(path);

    protected abstract OpenDocument CreateDocument(TModel model, string name);

    protected abstract void Stamp(TModel model, string fileName);

    protected virtual void Opened(TModel model, string path)
    {
    }

    public async Task<TModel?> ReadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            await using var file = File.OpenRead(path);
            var buffered = await context.Buffers.ReadAsync(file, ct).ConfigureAwait(true);
            if (buffered.Value is null)
            {
                Fail("status.open.failed", buffered.Diagnostics);
                return null;
            }

            using var buffer = buffered.Value;
            var read = formats.Read(path, buffer);
            if (read.Succeeded) return read.Value;
            Fail("status.open.failed", read.Diagnostics);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Status.Report(context.Texts.Format("status.open.failed", ex.Message), NotificationSeverity.Error);
            return null;
        }
    }

    public async Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default)
    {
        if (await ReadAsync(path, ct).ConfigureAwait(true) is not { } model) return false;
        Opened(model, path);
        context.Workspace.Open(CreateDocument(model, Path.GetFileNameWithoutExtension(path)));
        Loaded(model);
        return true;
    }

    public async Task OpenAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            context.Status.Report(context.Texts.Format("status.recent.missing", Path.GetFileName(path)), NotificationSeverity.Warning);
            context.Recent.Forget(path);
            return;
        }

        await OpenStreamAsync(_ => Task.FromResult<Stream>(File.OpenRead(path)), path, Path.GetFileName(path), ct).ConfigureAwait(true);
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        var file = await context.Dialogs.OpenFileAsync(context.Texts["dialog.open.title"], formats.ReadableFilters, ct).ConfigureAwait(true);
        if (file is null) return;

        await using (file.ConfigureAwait(true))
            await OpenStreamAsync(file.OpenRead, file.Path, file.Name, ct).ConfigureAwait(true);
    }

    public async Task SaveAsync(OpenDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.CommitPendingEdits();

        if (document.Path is null || formats.WriterFor(document.Path) is not { } writer)
        {
            await SaveAsAsync(document, ct).ConfigureAwait(true);
            return;
        }

        if (document.Model is not TModel model) return;
        if (!await context.SignedSave.ConfirmOverwriteAsync(document.Path, ct).ConfigureAwait(true)) return;

        var target = document.Path;
        await GuardedAsync(() => WriteAndReportAsync(writer, model, buffer => context.Files.ReplaceAsync(target, buffer, ct), document, target, Path.GetFileName(target))).ConfigureAwait(true);
    }

    public Task SaveAsAsync(OpenDocument document, CancellationToken ct = default) =>
        SaveAsAsync(document, formats.NativeExtension, "dialog.save.title", ct);

    public Task SaveInterchangeAsync(OpenDocument document, CancellationToken ct = default) =>
        SaveAsAsync(document, formats.InterchangeExtension, "dialog.save.interchange.title", ct);

    public Task<bool> SaveCopyAsync(OpenDocument document, CancellationToken ct = default) =>
        SaveAsAsync(document, formats.NativeExtension, "dialog.save.copy.title", ct, adopt: false);

    private async Task<bool> SaveAsAsync(OpenDocument document, string extension, string titleKey, CancellationToken ct, bool adopt = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.CommitPendingEdits();
        if (document.Model is not TModel model) return false;

        var filters = formats.WritableFilters
            .OrderByDescending(filter => filter.Extensions.Any(candidate => Matches(candidate, extension)))
            .ToList();
        var file = await context.Dialogs.SaveFileAsync(
            context.Texts[titleKey],
            Path.GetFileNameWithoutExtension(document.Title) + extension,
            filters,
            ct).ConfigureAwait(true);

        if (file is null) return false;

        await using (file.ConfigureAwait(true))
        {
            if ((formats.WriterFor(file.Path) ?? formats.NativeWriter) is not { } writer) return false;
            if (!await context.SignedSave.ConfirmOverwriteAsync(file.Path, ct).ConfigureAwait(true)) return false;

            return await GuardedAsync(() => WriteAndReportAsync(writer, model, buffer => context.Files.WriteAsync(file, buffer, ct), document, file.Path, file.Name, adopt)).ConfigureAwait(true);
        }
    }

    private bool Matches(string candidate, string extension) =>
        string.Equals(candidate.TrimStart('*', '.'), extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase);

    public async Task<bool> SaveToAsync(OpenDocument document, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(path);
        document.CommitPendingEdits();
        if (document.Model is not TModel model) return false;
        var writer = formats.WriterFor(path) ?? (Path.HasExtension(path) ? null : formats.NativeWriter);
        if (writer is null)
        {
            context.Status.Report(context.Texts.Format("status.save.unknownFormat", Path.GetFileName(path)), NotificationSeverity.Error);
            return false;
        }
        if (!await context.SignedSave.ConfirmOverwriteAsync(path, ct).ConfigureAwait(true)) return false;

        await GuardedAsync(() => WriteAndReportAsync(writer, model, buffer => context.Files.ReplaceAsync(path, buffer, ct), document, path, Path.GetFileName(path))).ConfigureAwait(true);
        return string.Equals(document.Path, path, StringComparison.Ordinal) && !document.IsModified;
    }

    private async Task OpenStreamAsync(Func<CancellationToken, Task<Stream>> open, string path, string name, CancellationToken ct)
    {
        var activity = context.Status.BeginActivity(context.Texts.Format("status.opening", name));
        try
        {
            await using var stream = await open(ct).ConfigureAwait(true);
            var buffered = await context.Buffers.ReadAsync(stream, ct).ConfigureAwait(true);
            if (buffered.Value is null)
            {
                Fail("status.open.failed", buffered.Diagnostics);
                return;
            }

            using var buffer = buffered.Value;
            await LoadAsync(buffer, path, name, ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Status.Report(context.Texts.Format("status.open.failed", ex.Message), NotificationSeverity.Error);
        }
        finally
        {
            context.Status.EndActivity(activity);
        }
    }

    private async Task LoadAsync(MemoryStream buffer, string path, string name, CancellationToken ct)
    {
        var (outcome, unlocked) = await context.Passwords.UnlockAsync(buffer, name, ct).ConfigureAwait(true);
        if (outcome == UnlockOutcome.Cancelled) return;
        if (outcome == UnlockOutcome.Unsupported || unlocked is null)
        {
            context.Status.Report(context.Texts.Format("status.open.failed", context.Texts["password.unsupported"]), NotificationSeverity.Error);
            return;
        }

        using var package = unlocked.Package;
        if (await ReaderAsync(path, package, ct).ConfigureAwait(true) is not { } read) return;
        var result = await Task.Run(() => read(package), ct).ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            Fail("status.open.failed", result.Diagnostics);
            return;
        }

        Opened(result.Value, path);
        var document = CreateDocument(result.Value, name);
        document.Protection = unlocked.Protection;
        document.MarkSaved(path, name);
        context.Workspace.Open(document);
        context.Recent.Remember(path, name);

        context.Status.Report(result.HasWarnings
            ? context.Texts.Format("status.opened.warnings", name, result.Diagnostics.Count)
            : context.Texts.Format("status.opened", name),
            result.HasWarnings ? NotificationSeverity.Warning : NotificationSeverity.Info);
        Loaded(result.Value);
    }

    protected virtual void Loaded(TModel model)
    {
    }

    protected virtual Task<Func<Stream, OperationResult<TModel>>?> ReaderAsync(string path, Stream content, CancellationToken ct) =>
        Task.FromResult<Func<Stream, OperationResult<TModel>>?>(stream => formats.Read(path, stream));

    protected IStatusBarService Status => context.Status;

    protected ITextCatalog Texts => context.Texts;

    private async Task<bool> WriteAndReportAsync(
        IModuleWriter<TModel> writer, TModel model, Func<MemoryStream, Task> commit,
        OpenDocument document, string path, string fileName, bool adopt = true)
    {
        Stamp(model, fileName);
        var point = document.CapturePoint();
        var fonts = await context.FontEmbedder.PrepareAsync(model).ConfigureAwait(true);
        using var buffer = new MemoryStream();
        var activity = context.Status.BeginActivity(context.Texts.Format("status.saving", fileName));
        activity.Progress = -1;
        FidelityReport fidelity;
        OperationResult<bool> result;
        using (context.Lock.Acquire())
        {
            try
            {
                (fidelity, result) = await Task.Run(() => (writer.CheckFidelity(model), writer.Write(model, buffer))).ConfigureAwait(true);
            }
            finally
            {
                context.Status.EndActivity(activity);
            }
        }

        if (!result.Succeeded)
        {
            Fail("status.save.failed", result.Diagnostics);
            return false;
        }

        var protectedAsAsked = await context.Passwords.CommitAsync(buffer, document.Protection, commit).ConfigureAwait(true);
        if (adopt) document.MarkSaved(path, fileName, point);
        context.Recent.Remember(path, fileName);
        if (!protectedAsAsked)
        {
            context.Status.Report(context.Texts.Format("status.save.passwordOdfOnly", fileName), NotificationSeverity.Warning);
            return true;
        }

        context.Status.Report(fonts.HasRefusals
            ? context.Texts.Format("status.saved.fontsRestricted", fileName, string.Join(", ", fonts.Restricted))
            : fidelity.IsPerfect
                ? context.Texts.Format("status.saved", fileName)
                : context.Texts.Format("status.saved.losses", fileName, fidelity.Losses.Count,
                    string.Join("; ", fidelity.Losses.Select(context.DiagnosticTexts.Describe).Select(text => text.TrimEnd('.', ' ')).Distinct(StringComparer.Ordinal)) + "."),
            fonts.HasRefusals || !fidelity.IsPerfect ? NotificationSeverity.Warning : NotificationSeverity.Info);
        return true;
    }

    private async Task<bool> GuardedAsync(Func<Task<bool>> save)
    {
        try
        {
            return await save().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Status.Report(context.Texts.Format("status.save.failed", ex.Message), NotificationSeverity.Error);
            return false;
        }
    }

    private void Fail(string key, IReadOnlyList<Diagnostic> diagnostics) =>
        context.Status.Report(context.Texts.Format(key,
            diagnostics.FirstOrDefault() is { } diagnostic ? context.DiagnosticTexts.Describe(diagnostic) : string.Empty), NotificationSeverity.Error);
}

public sealed class ModuleDocumentSaver(IDocumentFileServices services) : IDocumentSaver
{
    public bool CanSave(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return services.For(document) is not null;
    }

    public async Task<bool> SaveAsync(OpenDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (services.For(document) is not { } service) return false;

        if (document.Path is { Length: > 0 }) await service.SaveAsync(document, ct).ConfigureAwait(true);
        else await service.SaveAsAsync(document, ct).ConfigureAwait(true);

        return !document.IsModified;
    }
}
