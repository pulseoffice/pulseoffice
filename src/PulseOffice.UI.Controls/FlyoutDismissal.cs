using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace PulseOffice.UI.Controls;

public interface IFlyoutDismissal
{
    void PassThrough(PopupFlyoutBase flyout);

    void Toggle(PopupFlyoutBase flyout, Control target);
}

public sealed class FlyoutDismissal : IFlyoutDismissal
{
    private const long ReopenGuardMilliseconds = 500;

    private readonly ConditionalWeakTable<PopupFlyoutBase, StrongBox<long?>> _closedAt = [];

    public void PassThrough(PopupFlyoutBase flyout)
    {
        ArgumentNullException.ThrowIfNull(flyout);
        flyout.OverlayDismissEventPassThrough = true;
        var closedAt = _closedAt.GetValue(flyout, _ => new StrongBox<long?>());
        flyout.Closed += (_, _) => closedAt.Value = Environment.TickCount64;
    }

    public void Toggle(PopupFlyoutBase flyout, Control target)
    {
        ArgumentNullException.ThrowIfNull(flyout);
        ArgumentNullException.ThrowIfNull(target);
        if (flyout.IsOpen)
        {
            flyout.Hide();
            return;
        }
        if (_closedAt.TryGetValue(flyout, out var closedAt) && closedAt.Value is { } closed && Environment.TickCount64 - closed < ReopenGuardMilliseconds) return;
        flyout.ShowAt(target);
    }
}
