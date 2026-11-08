namespace PulseOffice.UI.Controls.Forms;

public interface IFormDesignMode
{
    bool IsDesigning { get; }

    void Toggle();

    void Set(bool designing);

    event EventHandler? Changed;
}

public sealed class FormDesignMode : IFormDesignMode
{
    public bool IsDesigning { get; private set; }

    public event EventHandler? Changed;

    public void Toggle() => Set(!IsDesigning);

    public void Set(bool designing)
    {
        if (IsDesigning == designing) return;
        IsDesigning = designing;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
