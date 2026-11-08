using PulseOffice.Agent.Hosting;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Agent.UI;

public interface IAgentOperationGate
{
    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct);

    Task<T> EditAsync<T>(Func<Task<T>> work, CancellationToken ct);

    Task<T> HoldingAsync<T>(OpenDocument document, Func<Task<T>> work);

    void CommitPendingEdits(OpenDocument document);
}

public sealed class AgentOperationGate(IDocumentWorkspace workspace, IDialogService dialogs) : IAgentOperationGate, IDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        await _turn.WaitAsync(ct).ConfigureAwait(true);
        try
        {
            return await work().ConfigureAwait(true);
        }
        finally
        {
            _turn.Release();
        }
    }

    public Task<T> EditAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        return RunAsync(() => dialogs.IsDialogOpen
            ? throw new AgentHostException("A dialog is open in PulseOffice: the document cannot change until the user closes it. Try again afterwards.")
            : work(), ct);
    }

    public async Task<T> HoldingAsync<T>(OpenDocument document, Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        using (workspace.Hold(document))
            return await work().ConfigureAwait(true);
    }

    public void CommitPendingEdits(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!dialogs.IsDialogOpen) document.CommitPendingEdits();
    }

    public void Dispose() => _turn.Dispose();
}
