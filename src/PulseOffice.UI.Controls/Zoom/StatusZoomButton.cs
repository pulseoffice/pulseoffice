using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Zoom;

public sealed class StatusZoomButton : Button
{
    private readonly TextBlock _value;
    private readonly string _name;

    public StatusZoomButton(IIconCatalog? icons, string accessibleName, DesignTokens.Metrics metrics)
    {
        _name = accessibleName;
        _value = new TextBlock
        {
            FontSize = metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 34,
            TextAlignment = Avalonia.Media.TextAlignment.Right,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        if (icons?.Find("ledger.view.zoomIn") is { } glyph)
            row.Children.Add(new IconView(glyph, 12) { VerticalAlignment = VerticalAlignment.Center }
                .Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary));
        row.Children.Add(_value);

        Content = row;
        Padding = new Thickness(6, 0);
        Height = Math.Max(16, metrics.StatusBarHeight - 4);
        VerticalAlignment = VerticalAlignment.Center;
        Focusable = true;
        IsVisible = false;
        Classes.Add("ribbon");
        ToolTip.SetTip(this, accessibleName);
    }

    public double? Percent { get; private set; }

    public string ShownText => _value.Text ?? string.Empty;

    public void Show(double? percent)
    {
        Percent = percent;
        IsVisible = percent is not null;
        if (percent is not { } value) return;

        _value.Text = $"{Math.Round(value).ToString(CultureInfo.CurrentCulture)} %";
        AutomationProperties.SetName(this, $"{_name}: {_value.Text}");
    }
}
