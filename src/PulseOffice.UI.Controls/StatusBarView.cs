using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class StatusBarView : ContentControl
{
    private readonly IStatusBarService _status;
    private readonly DesignTokens.Metrics _metrics;
    private readonly StackPanel _module;
    private readonly DockPanel _left;
    private readonly StackPanel _right;
    private readonly Dictionary<string, TextBox> _inputs = new(StringComparer.Ordinal);
    private readonly ContentControl _historySlot = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _zoomSlot = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };

    public event EventHandler<string>? CommandRequested;

    public StatusBarView(IStatusBarService status, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _metrics = DesignTokens.For(density);

        _module = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };

        _left = new DockPanel { LastChildFill = false, ClipToBounds = true };
        _right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingMedium };

        Height = _metrics.StatusBarHeight;
        BorderThickness = new Thickness(0, 1, 0, 0);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
        Grid.SetColumn(_zoomSlot, 3);
        grid.Children.Add(_zoomSlot);
        Grid.SetColumn(_historySlot, 4);
        grid.Children.Add(_historySlot);
        Grid.SetColumn(_module, 0);
        Grid.SetColumn(_left, 1);
        Grid.SetColumn(_right, 2);
        grid.Children.Add(_module);
        grid.Children.Add(_left);
        grid.Children.Add(_right);

        Content = new Border
        {
            Child = grid,
            Padding = new Thickness(_metrics.SpacingMedium, 0),
            BorderThickness = new Thickness(0, 1, 0, 0),
        }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        _status.Changed += OnStatusChanged;
        AttachedToVisualTree += (_, _) =>
        {
            _status.Changed -= OnStatusChanged;
            _status.Changed += OnStatusChanged;
            Refresh();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _status.Changed -= OnStatusChanged;
            _pendingRefresh?.Abort();
            _pendingRefresh = null;
        };

        Refresh();
    }

    private Avalonia.Threading.DispatcherOperation? _pendingRefresh;

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        if (_pendingRefresh is { Status: Avalonia.Threading.DispatcherOperationStatus.Pending }) return;
        _pendingRefresh = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(Refresh);
    }

    public void UseHistory(Control button) => _historySlot.Content = button;

    public void UseZoom(Control button) => _zoomSlot.Content = button;

    public void ShowModule(string? brandName, string? accentHex)
    {
        _module.Children.Clear();
        _module.IsVisible = brandName is { Length: > 0 };

        if (!_module.IsVisible || accentHex is null) return;

        var accent = new SolidColorBrush(Color.Parse(accentHex));

        _module.Children.Add(new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Background = accent,
            VerticalAlignment = VerticalAlignment.Center,
        });

        _module.Children.Add(new TextBlock
        {
            Text = brandName,
            FontSize = _metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
        });
    }

    public void Refresh()
    {
        _left.Children.Clear();
        _right.Children.Clear();

        foreach (var segment in _status.Segments.Where(s => s.IsVisible))
        {
            if (segment.Accept is not null)
            {
                var input = InputFor(segment);
                DockPanel.SetDock(input, Dock.Left);
                if (segment.Kind == StatusSegmentKind.Permanent) _right.Children.Add(input);
                else _left.Children.Add(input);
                continue;
            }

            var block = new TextBlock
            {
                Text = segment.Text,
                FontSize = _metrics.FontSizeSmall,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
            }.Themed(
                TextBlock.ForegroundProperty,

                segment.Kind == StatusSegmentKind.Transient
                    ? Palette.Keys.TextPrimary
                    : Palette.Keys.TextSecondary);

            ToolTip.SetTip(block, segment.Tooltip ?? segment.Text);
            Control shown = block;
            if (segment.CommandId is { Length: > 0 } commandId)
            {
                block.Margin = default;
                var link = new Button
                {
                    Content = block,
                    Padding = new Thickness(6, 0),
                    Margin = new Thickness(0, 0, _metrics.SpacingSmall, 0),
                    Height = Math.Max(16, _metrics.StatusBarHeight - 4),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                link.Classes.Add("ribbon");
                ToolTip.SetTip(link, segment.Tooltip ?? segment.Text);
                Avalonia.Automation.AutomationProperties.SetName(link, segment.Text);
                link.Click += (_, _) => CommandRequested?.Invoke(this, commandId);
                shown = link;
            }
            DockPanel.SetDock(shown, Dock.Left);

            if (segment.Kind == StatusSegmentKind.Permanent) _right.Children.Add(shown);
            else _left.Children.Add(shown);
        }

        foreach (var activity in _status.Activities)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = _metrics.SpacingSmall,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(panel, Dock.Left);

            panel.Children.Add(new TextBlock
            {
                Text = activity.Description,
                FontSize = _metrics.FontSize - 1,
                VerticalAlignment = VerticalAlignment.Center,
            });

            panel.Children.Add(new ProgressBar
            {
                Width = 90,
                Height = 4,
                Minimum = 0,
                Maximum = 1,
                Value = System.Math.Max(activity.Progress, 0),

                IsIndeterminate = activity.Progress < 0,
                VerticalAlignment = VerticalAlignment.Center,
            });

            _left.Children.Add(panel);
        }
    }

    public TextBox? InputOf(string id) => _inputs.TryGetValue(id, out var input) ? input : null;

    private TextBox InputFor(StatusSegment segment)
    {
        if (!_inputs.TryGetValue(segment.Id, out var input))
        {
            input = new TextBox
            {
                MinWidth = 110,
                MinHeight = 0,
                Height = _metrics.StatusBarHeight - 4,
                Padding = new Thickness(_metrics.SpacingSmall, 0),
                FontSize = _metrics.FontSizeSmall,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
            };
            input.Themed(TextBox.ForegroundProperty, Palette.Keys.TextSecondary);
            input.GotFocus += (_, _) =>
            {
                input.BorderThickness = new Thickness(1);
                input.SelectAll();
            };
            input.LostFocus += (_, _) =>
            {
                input.BorderThickness = new Thickness(0);
                input.Text = segment.Text;
            };
            input.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Escape)
                {
                    input.Text = segment.Text;
                    e.Handled = true;
                }
                else if (e.Key == Avalonia.Input.Key.Enter)
                {
                    if (segment.Accept?.Invoke(input.Text ?? string.Empty) == true) input.Text = segment.Text;
                    else input.SelectAll();
                    e.Handled = true;
                }
            };
            segment.FocusRequested += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(
                () => input.Focus(Avalonia.Input.NavigationMethod.Tab), Avalonia.Threading.DispatcherPriority.Background);
            _inputs[segment.Id] = input;
        }
        if (!input.IsFocused) input.Text = segment.Text;
        Avalonia.Automation.AutomationProperties.SetName(input, segment.AccessibleName ?? segment.Text);
        ToolTip.SetTip(input, segment.Tooltip ?? segment.Text);
        (input.Parent as Panel)?.Children.Remove(input);
        return input;
    }
}
