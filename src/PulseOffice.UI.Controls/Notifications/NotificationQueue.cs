namespace PulseOffice.UI.Controls.Notifications;

public sealed record NotificationPolicy(int Capacity, TimeSpan Lifetime, TimeSpan ErrorLifetime)
{
    public static NotificationPolicy Standard { get; } =
        new(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7));

    public TimeSpan LifetimeOf(NotificationSeverity severity) =>
        severity == NotificationSeverity.Error ? ErrorLifetime : Lifetime;
}

public sealed class NotificationEntry(long id, Notification notification)
{
    public long Id { get; } = id;
    public Notification Notification { get; } = notification;
    public DateTimeOffset? Expires { get; internal set; }
    public bool IsHovered { get; internal set; }
}

public enum NotificationPostKind
{
    Added,
    Bumped,
}

public sealed record NotificationPost(
    NotificationPostKind Kind, NotificationEntry Entry, IReadOnlyList<NotificationEntry> Evicted);

public sealed class NotificationQueue(NotificationPolicy policy)
{
    private readonly List<NotificationEntry> _entries = [];
    private long _nextId;

    public IReadOnlyList<NotificationEntry> Entries => _entries;

    public NotificationPost Post(Notification notification, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (_entries.FirstOrDefault(entry => entry.Notification.SameAs(notification)) is { } existing)
        {
            Restart(existing, now);
            return new NotificationPost(NotificationPostKind.Bumped, existing, []);
        }

        var entry = new NotificationEntry(++_nextId, notification);
        Restart(entry, now);
        _entries.Add(entry);

        var evicted = new List<NotificationEntry>();
        while (_entries.Count > Math.Max(1, policy.Capacity))
        {
            evicted.Add(_entries[0]);
            _entries.RemoveAt(0);
        }

        return new NotificationPost(NotificationPostKind.Added, entry, evicted);
    }

    public NotificationEntry? Dismiss(long id)
    {
        if (_entries.FirstOrDefault(entry => entry.Id == id) is not { } entry) return null;
        _entries.Remove(entry);
        return entry;
    }

    public bool Hover(long id, bool hovered, DateTimeOffset now)
    {
        if (_entries.FirstOrDefault(entry => entry.Id == id) is not { } entry) return false;
        if (entry.IsHovered == hovered) return false;

        entry.IsHovered = hovered;
        if (hovered) entry.Expires = null;
        else Restart(entry, now);
        return true;
    }

    public IReadOnlyList<NotificationEntry> Expire(DateTimeOffset now)
    {
        var expired = _entries.Where(entry => entry.Expires is { } at && at <= now).ToList();
        foreach (var entry in expired) _entries.Remove(entry);
        return expired;
    }

    public DateTimeOffset? NextDeadline =>
        _entries.Where(entry => entry.Expires is not null).Select(entry => entry.Expires).Min();

    private void Restart(NotificationEntry entry, DateTimeOffset now) =>
        entry.Expires = entry.IsHovered ? null : now + policy.LifetimeOf(entry.Notification.Severity);
}
