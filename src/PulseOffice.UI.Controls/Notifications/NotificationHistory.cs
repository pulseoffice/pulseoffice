namespace PulseOffice.UI.Controls.Notifications;

public sealed record NotificationRecord(Notification Notification, DateTimeOffset At, int Repeats = 1);

public interface INotificationHistory
{
    IReadOnlyList<NotificationRecord> Entries { get; }

    int Unread { get; }

    event EventHandler? Changed;

    void Record(Notification notification);

    void MarkRead();

    void Clear();
}

public sealed class NotificationHistory(TimeProvider time) : INotificationHistory
{
    public const int Capacity = 200;

    private readonly object _gate = new();
    private readonly List<NotificationRecord> _entries = [];
    private int _unread;

    public IReadOnlyList<NotificationRecord> Entries
    {
        get
        {
            lock (_gate) return [.. _entries];
        }
    }

    public int Unread
    {
        get
        {
            lock (_gate) return _unread;
        }
    }

    public event EventHandler? Changed;

    public void Record(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (string.IsNullOrWhiteSpace(notification.Message)) return;

        lock (_gate)
        {
            var now = time.GetUtcNow();

            if (_entries.Count > 0 && Same(_entries[0].Notification, notification))
                _entries[0] = _entries[0] with { Notification = notification, At = now, Repeats = _entries[0].Repeats + 1 };
            else
            {
                _entries.Insert(0, new NotificationRecord(notification, now));
                if (_entries.Count > Capacity) _entries.RemoveAt(_entries.Count - 1);
            }

            if (notification.IsProminent) _unread = Math.Min(_unread + 1, Capacity);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkRead()
    {
        lock (_gate)
        {
            if (_unread == 0) return;
            _unread = 0;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _unread = 0;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool Same(Notification left, Notification right) =>
        left.Severity == right.Severity && string.Equals(left.Message, right.Message, StringComparison.Ordinal);
}
