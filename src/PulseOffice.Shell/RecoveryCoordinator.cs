using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public sealed class RecoveryCoordinator : IDisposable
{
    private readonly IDocumentRecovery _recovery;
    private readonly IDocumentWorkspace _workspace;
    private readonly IDialogService _dialogs;
    private readonly IStatusBarService _status;
    private readonly ITextCatalog _texts;
    private readonly ILogger<RecoveryCoordinator> _logger;

    public RecoveryCoordinator(
        IDocumentRecovery recovery,
        IDocumentWorkspace workspace,
        IDialogService dialogs,
        IStatusBarService status,
        ITextCatalog texts,
        ILogger<RecoveryCoordinator> logger)
    {
        _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentOpened += OnDocumentOpened;
        _workspace.DocumentClosed += OnDocumentClosed;
    }

    public async Task OfferPendingAsync(CancellationToken ct = default)
    {
        var pending = _recovery.Pending;
        if (pending.Count == 0) return;

        _logger.LogInformation("Recovery snapshots awaiting a decision: {Count}", pending.Count);

        var view = new RecoveryPromptView(
            [.. pending.Select(s => new RecoveryPromptView.Entry(s.Id, s.Title, s.SavedAt, s.OriginalPath))],
            _texts);

        var answer = await _dialogs.ShowAsync<object>(view, new DialogOptions
        {
            TitleKey = "dialog.recovery.title",
            PreferredWidth = 520,
            CanCancelWithEscape = false,
        }, ct).ConfigureAwait(true);

        await ApplyAsync(answer, pending, ct).ConfigureAwait(true);
    }

    public async Task ApplyAsync(object? answer, IReadOnlyList<RecoverySnapshot> pending, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pending);

        if (answer is false)
        {
            _logger.LogInformation("Recovery snapshots discarded by the user");
            _recovery.DiscardAll();
            return;
        }

        if (answer is not RecoveryPromptView.Selection selection)
        {
            _logger.LogInformation("Recovery decision deferred: snapshots kept");
            return;
        }

        var restored = 0;
        var failed = new List<string>();

        foreach (var snapshot in pending.ToList())
        {
            if (!selection.Restore.Contains(snapshot.Id))
            {
                _recovery.Discard(snapshot.Id);
                continue;
            }

            var document = await _recovery.RestoreAsync(snapshot, ct).ConfigureAwait(true);

            if (document is null)
            {
                _logger.LogWarning("Recovery snapshot could not be restored: {Id}", snapshot.Id);
                failed.Add(snapshot.Title);
                continue;
            }

            _workspace.Open(document);
            restored++;
        }

        _logger.LogInformation("Recovered documents: {Count}, failed: {Failed}", restored, failed.Count);
        if (failed.Count > 0)
            _status.Report(_texts.Format("status.recovery.failed", restored, string.Join(", ", failed)), NotificationSeverity.Warning);
        else
            _status.Report(_texts.Format("status.recovery.restored", restored));
    }

    private void OnDocumentOpened(object? sender, OpenDocument document) =>
        document.Changed += OnDocumentChanged;

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (sender is OpenDocument document && !document.IsModified) _recovery.DiscardFor(document);
    }

    private void OnDocumentClosed(object? sender, OpenDocument document)
    {
        document.Changed -= OnDocumentChanged;
        if (_workspace.Documents.Any(other => ReferenceEquals(other.History, document.History))) return;
        _recovery.DiscardFor(document);
    }

    public void Dispose()
    {
        _workspace.DocumentOpened -= OnDocumentOpened;
        _workspace.DocumentClosed -= OnDocumentClosed;

        foreach (var document in _workspace.Documents) document.Changed -= OnDocumentChanged;
    }
}
