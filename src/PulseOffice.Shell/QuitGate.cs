namespace PulseOffice.Shell;

public sealed class QuitGate(IUnsavedChangesGuard guard)
{
    private bool _asking;

    public bool Allowed { get; private set; }

    public bool MustConfirm => !Allowed && (_asking || guard.HasUnsaved);

    public async Task<bool> ConfirmAsync(CancellationToken ct = default)
    {
        if (Allowed) return true;
        if (_asking) return false;

        _asking = true;
        try
        {
            Allowed = await guard.ConfirmCloseAllAsync(ct).ConfigureAwait(true);
            return Allowed;
        }
        finally
        {
            _asking = false;
        }
    }
}
