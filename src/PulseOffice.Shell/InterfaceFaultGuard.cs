using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public interface IInterfaceFaultGuard
{
    void Attach(Dispatcher dispatcher);

    bool Absorb(Exception fault);
}

public sealed class InterfaceFaultGuard(
    IDocumentWorkspace workspace,
    IDocumentRecovery recovery,
    IStatusBarService status,
    ITextCatalog texts,
    TimeProvider time,
    ILogger<InterfaceFaultGuard> logger) : IInterfaceFaultGuard
{
    private const int FaultsTolerated = 5;
    private static readonly TimeSpan FaultWindow = TimeSpan.FromSeconds(10);

    private readonly Queue<DateTimeOffset> _recent = new();

    public void Attach(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.UnhandledException += (_, args) => args.Handled = Absorb(args.Exception);
    }

    public bool Absorb(Exception fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        logger.LogError(fault, "Unhandled exception on the interface thread");
        SnapshotModifiedDocuments();

        if (fault is OutOfMemoryException or StackOverflowException or AccessViolationException) return false;

        var now = time.GetUtcNow();
        _recent.Enqueue(now);
        while (_recent.Count > 0 && now - _recent.Peek() > FaultWindow) _recent.Dequeue();
        if (_recent.Count > FaultsTolerated)
        {
            logger.LogCritical("Too many interface faults in {Window}: letting the process terminate", FaultWindow);
            return false;
        }

        status.Report(texts["status.fault.absorbed"], NotificationSeverity.Error);
        return true;
    }

    private async Task SnapshotAsync(OpenDocument document)
    {
        try
        {
            await recovery.SnapshotAsync(document).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogWarning(exception, "Emergency snapshot failed for {Title}", document.Title);
        }
    }

    private void SnapshotModifiedDocuments()
    {
        foreach (var document in workspace.Documents.Where(document => document.IsModified).ToList())
        {
            _ = SnapshotAsync(document);
        }
    }
}
