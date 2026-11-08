using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Lists;

public interface IChoiceListPopup
{
    bool IsOpen { get; }

    void Show(Control host, Rect anchor, IReadOnlyList<string> choices, string? current, Action<string> chosen, Action? closed = null);

    void Close();

    bool HandleKey(Key key);
}

public sealed class ChoiceListPopup : IChoiceListPopup
{
    private const double MaximumHeight = 260;
    private const double MinimumWidth = 140;

    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private Popup? _popup;
    private ListBox? _list;
    private Action? _commit;

    public bool IsOpen => _popup?.IsOpen == true;

    public void Show(Control host, Rect anchor, IReadOnlyList<string> choices, string? current, Action<string> chosen, Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(chosen);
        Close();
        if (choices.Count == 0) return;

        var list = new ListBox
        {
            ItemsSource = choices,
            SelectedIndex = current is null ? 0 : Math.Max(0, IndexOf(choices, current)),
            MaxHeight = MaximumHeight,
            MinWidth = Math.Max(MinimumWidth, anchor.Width),
            FontSize = _metrics.FontSize,
            Background = Brushes.Transparent,
        };
        list.Classes.Add("choiceList");

        var surface = new Border
        {
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            BoxShadow = BoxShadows.Parse("0 8 22 -6 #40101828"),
            Child = list,
        };
        surface.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceOverlay);
        surface.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var popup = new Popup
        {
            PlacementTarget = host,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.BottomLeft,
            PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.BottomRight,
            PlacementRect = anchor,
            IsLightDismissEnabled = true,
            Child = surface,
        };

        void Commit()
        {
            if (list.SelectedItem is not string value) return;
            Close();
            chosen(value);
        }

        list.Tapped += (_, _) => Commit();
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_popup, popup))
            {
                _popup = null;
                _list = null;
                _commit = null;
            }
            ((ISetLogicalParent)popup).SetParent(null);
            closed?.Invoke();
        };

        ((ISetLogicalParent)popup).SetParent(host);
        _popup = popup;
        _list = list;
        _commit = Commit;
        popup.IsOpen = true;
        if (list.SelectedItem is { } selected) list.ScrollIntoView(selected);
    }

    public void Close()
    {
        if (_popup is { } popup) popup.IsOpen = false;
    }

    public bool HandleKey(Key key)
    {
        if (!IsOpen || _list is not { } list) return false;

        switch (key)
        {
            case Key.Down:
                list.SelectedIndex = Math.Min(list.SelectedIndex + 1, list.ItemCount - 1);
                break;
            case Key.Up:
                list.SelectedIndex = Math.Max(list.SelectedIndex - 1, 0);
                break;
            case Key.PageDown:
                list.SelectedIndex = Math.Min(list.SelectedIndex + 8, list.ItemCount - 1);
                break;
            case Key.PageUp:
                list.SelectedIndex = Math.Max(list.SelectedIndex - 8, 0);
                break;
            case Key.Home:
                list.SelectedIndex = 0;
                break;
            case Key.End:
                list.SelectedIndex = list.ItemCount - 1;
                break;
            case Key.Enter or Key.Tab:
                _commit?.Invoke();
                return true;
            case Key.Escape:
                Close();
                return true;
            default:
                return false;
        }

        if (list.SelectedItem is { } selected) list.ScrollIntoView(selected);
        return true;
    }

    private int IndexOf(IReadOnlyList<string> choices, string value)
    {
        for (var index = 0; index < choices.Count; index++)
            if (string.Equals(choices[index], value, StringComparison.CurrentCultureIgnoreCase)) return index;
        return -1;
    }
}
