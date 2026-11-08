using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Panels;

public sealed class PanelSearchBox : UserControl
{
    private readonly TextBox _box;
    private readonly Button _clear;

    public PanelSearchBox(IIconCatalog icons, DesignTokens.Metrics metrics, string placeholder)
    {
        ArgumentNullException.ThrowIfNull(icons);
        _clear = new Button
        {
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 3, 0),
            IsVisible = false,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = Icon(icons, "ui.close", 12),
        };
        _clear.Classes.Add("ribbon");
        _clear.Click += (_, _) => Clear();
        _box = new TextBox
        {
            PlaceholderText = placeholder,
            MinHeight = metrics.ControlHeight + 2,
            VerticalContentAlignment = VerticalAlignment.Center,
            InnerLeftContent = new Border { Padding = new Thickness(metrics.SpacingSmall + 2, 0, 0, 0), Child = Icon(icons, "ui.search", 14) },
            InnerRightContent = _clear,
        };
        AutomationProperties.SetName(_box, placeholder);
        _box.TextChanged += (_, _) =>
        {
            _clear.IsVisible = !string.IsNullOrEmpty(_box.Text);
            TextChanged?.Invoke(this, EventArgs.Empty);
        };
        _box.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape && !string.IsNullOrEmpty(_box.Text)) Clear();
            else if (args.Key == Key.Down) Descend?.Invoke(this, EventArgs.Empty);
            else if (args.Key == Key.Enter) Submitted?.Invoke(this, EventArgs.Empty);
            else return;
            args.Handled = true;
        };
        Content = _box;
    }

    public event EventHandler? TextChanged;

    public event EventHandler? Descend;

    public event EventHandler? Submitted;

    public string Text
    {
        get => _box.Text ?? string.Empty;
        set => _box.Text = value;
    }

    public void Clear() => _box.Text = string.Empty;

    private Control Icon(IIconCatalog icons, string key, double size) =>
        icons.Find(key) is { } geometry
            ? new IconView(geometry, size) { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
                .Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
            : new Border { Width = size, Height = size };
}
