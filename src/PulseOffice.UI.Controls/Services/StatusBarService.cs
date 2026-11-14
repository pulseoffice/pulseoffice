using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseOffice.UI.Controls.Notifications;

namespace PulseOffice.UI.Controls.Services;

public enum StatusSegmentKind
{
    Transient,

    Contextual,

    Permanent,
}

public sealed class StatusSegment(string id, StatusSegmentKind kind) : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private string? _tooltip;
    private bool _isVisible = true;

    public string Id { get; } = id;
    public StatusSegmentKind Kind { get; } = kind;

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public string? Tooltip
    {
        get => _tooltip;
        set => Set(ref _tooltip, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public string? CommandId { get; set; }

    public Func<string, bool>? Accept { get; set; }

    public string? AccessibleName { get; set; }

    public event EventHandler? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class BackgroundActivity(string id, string description, CancellationTokenSource? cancellation)
    : INotifyPropertyChanged
{
    private double _progress;
    private string _description = description;

    public string Id { get; } = id;

    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
        }
    }

    public bool CanCancel => cancellation is not null;

    public void Cancel() => cancellation?.Cancel();

    public event PropertyChangedEventHandler? PropertyChanged;
}

public interface IStatusBarService
{
    IReadOnlyList<StatusSegment> Segments { get; }
    IReadOnlyList<BackgroundActivity> Activities { get; }

    event EventHandler? Changed;

    StatusSegment Register(string id, StatusSegmentKind kind);
    void Remove(string id);

    void Report(string message, TimeSpan? duration = null);

    void DismissTransient();

    void Report(Notification notification);

    void Report(string message, NotificationSeverity severity, NotificationAction? action = null);

    BackgroundActivity BeginActivity(string description, CancellationTokenSource? cancellation = null);
    void EndActivity(BackgroundActivity activity);
}

public sealed class StatusBarService(TimeProvider time, INotificationCenter notifications, INotificationHistory history) : IStatusBarService, IDisposable
{
    public void Dispose()
    {
        lock (_gate)
        {
            ++_transientGeneration;
            _transientTimer?.Dispose();
            _transientTimer = null;
        }
    }

    private const string TransientSegmentId = "shell.transient";
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(6);

    private readonly List<StatusSegment> _segments = [];
    private readonly List<BackgroundActivity> _activities = [];
    private readonly Lock _gate = new();
    private ITimer? _transientTimer;
    private long _transientGeneration;

    public event EventHandler? Changed;

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Announce();

    private void Announce() => Changed?.Invoke(this, EventArgs.Empty);

    public IReadOnlyList<StatusSegment> Segments
    {
        get
        {
            lock (_gate) return [.. _segments];
        }
    }

    public IReadOnlyList<BackgroundActivity> Activities
    {
        get
        {
            lock (_gate) return [.. _activities];
        }
    }

    public StatusSegment Register(string id, StatusSegmentKind kind)
    {
        StatusSegment segment;
        lock (_gate)
        {
            if (_segments.FirstOrDefault(s => s.Id == id) is { } existing) return existing;

            segment = new StatusSegment(id, kind);
            segment.PropertyChanged += OnItemChanged;
            _segments.Add(segment);
        }

        Announce();
        return segment;
    }

    public void Remove(string id)
    {
        lock (_gate)
        {
            if (_segments.FirstOrDefault(s => s.Id == id) is not { } segment) return;

            _segments.Remove(segment);
            segment.PropertyChanged -= OnItemChanged;
        }

        Announce();
    }

    public void Report(string message, NotificationSeverity severity, NotificationAction? action = null) =>
        Report(new Notification(message, severity, action));

    public void Report(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (string.IsNullOrWhiteSpace(notification.Message)) return;

        Show(notification.Message, null);
        history.Record(notification);
        if (notification.IsProminent) notifications.Post(notification);
    }

    public void Report(string message, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        Show(message, duration);
        history.Record(new Notification(message, NotificationSeverity.Info));
    }

    private void Show(string message, TimeSpan? duration)
    {
        var segment = Register(TransientSegmentId, StatusSegmentKind.Transient);
        segment.Text = message;
        segment.IsVisible = true;

        var due = duration is { } requested && requested > TimeSpan.Zero ? requested : DefaultDuration;

        lock (_gate)
        {
            var generation = ++_transientGeneration;
            _transientTimer?.Dispose();
            _transientTimer = time.CreateTimer(
                _ => Expire(segment, generation),
                state: null,
                dueTime: due,
                period: Timeout.InfiniteTimeSpan);
        }
    }

    public void DismissTransient()
    {
        StatusSegment? segment;
        lock (_gate)
        {
            ++_transientGeneration;
            _transientTimer?.Dispose();
            _transientTimer = null;
            segment = _segments.FirstOrDefault(s => s.Id == TransientSegmentId);
        }

        if (segment is not null) segment.IsVisible = false;
    }

    private void Expire(StatusSegment segment, long generation)
    {
        lock (_gate)
        {
            if (generation != _transientGeneration) return;
        }

        segment.IsVisible = false;
    }

    public BackgroundActivity BeginActivity(string description, CancellationTokenSource? cancellation = null)
    {
        var activity = new BackgroundActivity(Guid.NewGuid().ToString("N"), description, cancellation);
        activity.PropertyChanged += OnItemChanged;

        lock (_gate) _activities.Add(activity);
        Announce();

        return activity;
    }

    public void EndActivity(BackgroundActivity activity)
    {
        lock (_gate)
        {
            if (!_activities.Remove(activity)) return;
        }

        activity.PropertyChanged -= OnItemChanged;
        Announce();
    }
}
