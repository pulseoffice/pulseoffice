namespace PulseOffice.UI.Controls.Services;

public interface IInterfaceLock
{
    bool IsLocked { get; }

    event EventHandler? Changed;

    IDisposable Acquire();
}

public sealed class InterfaceLock : IInterfaceLock
{
    private int _holders;

    public bool IsLocked => _holders > 0;

    public event EventHandler? Changed;

    public IDisposable Acquire()
    {
        _holders++;
        if (_holders == 1) Changed?.Invoke(this, EventArgs.Empty);
        return new Holder(this);
    }

    private void Release()
    {
        _holders--;
        if (_holders == 0) Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Holder(InterfaceLock owner) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            owner.Release();
        }
    }
}
