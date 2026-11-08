namespace PulseOffice.UI.Controls.Services;

public interface IRibbonStateNotifier
{
    event EventHandler? StateChanged;

    void Notify();
}

public sealed class RibbonStateNotifier : IRibbonStateNotifier
{
    public event EventHandler? StateChanged;

    public void Notify() => StateChanged?.Invoke(this, EventArgs.Empty);
}
