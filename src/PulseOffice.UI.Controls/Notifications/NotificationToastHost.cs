using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Notifications;

public sealed class NotificationToastHost : Panel
{
    private readonly INotificationCenter _center;
    private readonly IIconCatalog _icons;
    private readonly ITextCatalog _texts;
    private readonly INotificationActionRunner _runner;
    private readonly StackPanel _stack;
    private readonly Dictionary<long, NotificationToastView> _views = [];

    public NotificationToastHost(
        INotificationCenter center,
        IIconCatalog icons,
        ITextCatalog texts,
        INotificationActionRunner runner,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _center = center ?? throw new ArgumentNullException(nameof(center));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));

        var metrics = DesignTokens.For(density);

        _stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = metrics.SpacingSmall,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(metrics.SpacingLarge, 0, metrics.SpacingLarge, metrics.StatusBarHeight + metrics.SpacingMedium),
        };

        Children.Add(_stack);
    }

    public IReadOnlyList<NotificationToastView> Toasts => [.. _stack.Children.OfType<NotificationToastView>()];

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _center.Changed += OnChanged;
        foreach (var entry in _center.Visible) Show(entry);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _center.Changed -= OnChanged;
        _views.Clear();
        _stack.Children.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnChanged(object? sender, NotificationChange change)
    {
        if (Dispatcher.UIThread.CheckAccess()) Apply(change);
        else Dispatcher.UIThread.Post(() => Apply(change));
    }

    private void Apply(NotificationChange change)
    {
        if (VisualRoot is null) return;

        switch (change.Kind)
        {
            case NotificationChangeKind.Added:
                Show(change.Entry);
                break;
            case NotificationChangeKind.Bumped:
                if (_views.TryGetValue(change.Entry.Id, out var bumped)) _ = bumped.ShakeAsync();
                break;
            case NotificationChangeKind.Removed:
                Hide(change.Entry.Id);
                break;
        }
    }

    private void Show(NotificationEntry entry)
    {
        if (_views.ContainsKey(entry.Id)) return;

        var view = new NotificationToastView(entry, _icons, _texts);
        view.PointerEntered += (_, _) => _center.SetHovered(entry.Id, true);
        view.PointerExited += (_, _) => _center.SetHovered(entry.Id, false);
        view.CloseRequested += (_, _) => _center.Dismiss(entry.Id);
        view.ActionRequested += (_, action) =>
        {
            _center.Dismiss(entry.Id);
            _runner.Run(action);
        };

        _views[entry.Id] = view;
        _stack.Children.Add(view);
        Dispatcher.UIThread.Post(view.Enter, DispatcherPriority.Render);
    }

    private void Hide(long id)
    {
        if (!_views.Remove(id, out var view)) return;

        view.IsHitTestVisible = false;
        view.Leave();
        DispatcherTimer.RunOnce(() => _stack.Children.Remove(view), NotificationToastView.LeaveDuration);
    }
}
