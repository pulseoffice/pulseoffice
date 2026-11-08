namespace PulseOffice.UI.Controls.Notifications;

public enum NotificationChangeKind
{
    Added,
    Bumped,
    Removed,
}

public sealed record NotificationChange(NotificationChangeKind Kind, NotificationEntry Entry);

public interface INotificationCenter
{
    IReadOnlyList<NotificationEntry> Visible { get; }

    event EventHandler<NotificationChange>? Changed;

    void Post(Notification notification);
    void Dismiss(long id);
    void SetHovered(long id, bool hovered);
}

public sealed class NotificationCenter : INotificationCenter, IDisposable
{
    private readonly TimeProvider _time;
    private readonly NotificationQueue _queue;
    private readonly Lock _gate = new();
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromMilliseconds(1);

    private ITimer? _timer;

    public NotificationCenter(TimeProvider time)
        : this(time, NotificationPolicy.Standard)
    {
    }

    public NotificationCenter(TimeProvider time, NotificationPolicy policy)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _queue = new NotificationQueue(policy ?? throw new ArgumentNullException(nameof(policy)));
    }

    public event EventHandler<NotificationChange>? Changed;

    public IReadOnlyList<NotificationEntry> Visible
    {
        get
        {
            lock (_gate) return [.. _queue.Entries];
        }
    }

    public void Post(Notification notification)
    {
        var changes = new List<NotificationChange>();
        lock (_gate)
        {
            var post = _queue.Post(notification, _time.GetUtcNow());
            changes.AddRange(post.Evicted.Select(entry => new NotificationChange(NotificationChangeKind.Removed, entry)));
            changes.Add(new NotificationChange(
                post.Kind == NotificationPostKind.Added ? NotificationChangeKind.Added : NotificationChangeKind.Bumped,
                post.Entry));
            Schedule();
        }

        Announce(changes);
    }

    public void Dismiss(long id)
    {
        NotificationEntry? removed;
        lock (_gate)
        {
            removed = _queue.Dismiss(id);
            Schedule();
        }

        if (removed is not null) Announce([new NotificationChange(NotificationChangeKind.Removed, removed)]);
    }

    public void SetHovered(long id, bool hovered)
    {
        lock (_gate)
        {
            if (_queue.Hover(id, hovered, _time.GetUtcNow())) Schedule();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnTimer()
    {
        IReadOnlyList<NotificationEntry> expired;
        lock (_gate)
        {
            expired = _queue.Expire(_time.GetUtcNow());
            Schedule();
        }

        Announce([.. expired.Select(entry => new NotificationChange(NotificationChangeKind.Removed, entry))]);
    }

    private void Schedule()
    {
        _timer?.Dispose();
        _timer = null;

        if (_queue.NextDeadline is not { } deadline) return;

        var due = deadline - _time.GetUtcNow();
        if (due < MinimumDelay) due = MinimumDelay;
        _timer = _time.CreateTimer(_ => OnTimer(), null, due, Timeout.InfiniteTimeSpan);
    }

    private void Announce(IEnumerable<NotificationChange> changes)
    {
        foreach (var change in changes) Changed?.Invoke(this, change);
    }
}
