using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
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

        if (answer is not bool restore)
        {
            _logger.LogInformation("Recovery decision deferred: snapshots kept");
            return;
        }

        if (!restore)
        {
            _logger.LogInformation("Recovery snapshots discarded by the user");
            _recovery.DiscardAll();
            return;
        }

        var restored = 0;

        foreach (var snapshot in pending.ToList())
        {
            var document = await _recovery.RestoreAsync(snapshot, ct).ConfigureAwait(true);

            if (document is null)
            {
                _logger.LogWarning("Recovery snapshot could not be restored: {Id}", snapshot.Id);
                continue;
            }

            _workspace.Open(document);
            restored++;
        }

        _logger.LogInformation("Recovered documents: {Count}", restored);
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
        _recovery.DiscardFor(document);
    }

    public void Dispose()
    {
        _workspace.DocumentOpened -= OnDocumentOpened;
        _workspace.DocumentClosed -= OnDocumentClosed;

        foreach (var document in _workspace.Documents) document.Changed -= OnDocumentChanged;
    }
}
