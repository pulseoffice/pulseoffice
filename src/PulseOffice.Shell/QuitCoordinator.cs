using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace PulseOffice.Shell;

public interface IQuitTarget
{
    void Quit();
}

public sealed class QuitCoordinator(IUnsavedChangesGuard guard, ILogger<QuitCoordinator> logger)
{
    private readonly QuitGate _gate = new(guard);

    public bool Allowed => _gate.Allowed;

    public bool Intercept(CancelEventArgs args, IQuitTarget target)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(target);

        var confirm = MustConfirm();
        logger.LogDebug("Quit requested through {Target}; confirmation needed: {Confirm}", target.GetType().Name, confirm);
        if (!confirm) return false;

        args.Cancel = true;
        _ = ConfirmThenQuitAsync(target);
        return true;
    }

    private bool MustConfirm()
    {
        try
        {
            return _gate.MustConfirm;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogError(ex, "Unsaved-changes check failed; asking before quitting");
            return !_gate.Allowed;
        }
    }

    private async Task ConfirmThenQuitAsync(IQuitTarget target)
    {
        bool allowed;
        try
        {
            allowed = await _gate.ConfirmAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogError(ex, "Close confirmation failed");
            return;
        }

        if (allowed) target.Quit();
    }
}
