using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Tabs;

public sealed class TabOverlays(DesignTokens.Metrics metrics)
{
    public Popup? Open { get; private set; }

    public TextBox? Editor { get; private set; }

    public TextBlock? EditorProblem { get; private set; }

    public Task<string?> EditAsync(Control strip, Rect bounds, string current, Func<string, string?> problem)
    {
        ArgumentNullException.ThrowIfNull(strip);
        ArgumentNullException.ThrowIfNull(problem);
        Close();
        var result = new TaskCompletionSource<string?>();
        var box = new TextBox
        {
            Text = current,
            MinWidth = Math.Max(bounds.Width + metrics.SpacingLarge * 2, 90),
            Height = bounds.Height,
            Padding = new Thickness(metrics.SpacingSmall, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var note = new TextBlock { FontSize = metrics.FontSizeSmall, IsVisible = false, Margin = new Thickness(metrics.SpacingSmall, 2), TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 260 }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        var popup = Popup(strip, bounds, new StackPanel { Children = { box, note } }, PopupAnchor.TopLeft, PopupGravity.BottomRight);
        Editor = box;
        EditorProblem = note;

        void Finish(string? value)
        {
            if (result.Task.IsCompleted) return;
            result.TrySetResult(value);
            popup.IsOpen = false;
        }

        box.TextChanged += (_, _) =>
        {
            var found = problem(box.Text ?? string.Empty);
            note.Text = found ?? string.Empty;
            note.IsVisible = found is not null;
            box.Classes.Set("error", found is not null);
        };
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Finish(null);
            else if (args.Key == Key.Enter && problem(box.Text ?? string.Empty) is null) Finish(box.Text);
            else return;
            args.Handled = true;
        };
        box.LostFocus += (_, _) => Finish(problem(box.Text ?? string.Empty) is null ? box.Text : null);
        popup.Closed += (_, _) =>
        {
            Finish(null);
            Editor = null;
            EditorProblem = null;
        };
        popup.IsOpen = true;
        box.Focus();
        box.SelectAll();
        return result.Task;
    }

    public Task<T?> ShowAsync<T>(Control strip, Rect bounds, Func<Action<T?>, Control> content) where T : class
    {
        ArgumentNullException.ThrowIfNull(strip);
        ArgumentNullException.ThrowIfNull(content);
        Close();
        var result = new TaskCompletionSource<T?>();
        Popup? popup = null;
        void Finish(T? value)
        {
            if (result.Task.IsCompleted) return;
            result.TrySetResult(value);
            if (popup is not null) popup.IsOpen = false;
        }

        var body = new Border
        {
            Padding = new Thickness(metrics.SpacingMedium),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            Child = content(Finish),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceOverlay).Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        body.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape) return;
            Finish(null);
            args.Handled = true;
        };
        popup = Popup(strip, bounds, body, PopupAnchor.TopLeft, PopupGravity.TopRight);
        popup.IsLightDismissEnabled = true;
        popup.Closed += (_, _) => Finish(null);
        body.Focusable = true;
        body.FocusAdorner = null;
        popup.Opened += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var first = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(body).OfType<InputElement>()
                .FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled);
            (first ?? body).Focus(NavigationMethod.Tab);
        });
        popup.IsOpen = true;
        return result.Task;
    }

    public void Close()
    {
        if (Open is { } open) open.IsOpen = false;
        Open = null;
    }

    private Popup Popup(Control strip, Rect bounds, Control child, PopupAnchor anchor, PopupGravity gravity)
    {
        var popup = new Popup
        {
            PlacementTarget = strip,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementRect = bounds,
            PlacementAnchor = anchor,
            PlacementGravity = gravity,
            Child = child,
            ShouldUseOverlayLayer = true,
        };
        if (strip.Parent is not Panel panel) throw new InvalidOperationException("The tab strip must be hosted in a panel.");
        panel.Children.Add(popup);
        popup.Closed += (_, _) =>
        {
            if (popup.Parent is Panel owner) owner.Children.Remove(popup);
        };
        Open = popup;
        return popup;
    }
}
