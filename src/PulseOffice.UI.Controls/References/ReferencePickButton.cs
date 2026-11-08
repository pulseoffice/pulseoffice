using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.References;

public sealed class ReferencePickButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public ReferencePickButton(IReferencePicker picker, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(texts);

        Picker = picker;
        Width = metrics.ControlHeight + 4;
        MinHeight = metrics.ControlHeight;
        Padding = new Thickness(0);
        VerticalAlignment = VerticalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Content = picker.Icon is { } geometry
            ? new IconView(geometry, metrics.IconSize).Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
            : new TextBlock { Text = "▦" };

        Classes.Add("dialogQuiet");
        ToolTip.SetTip(this, texts["referencePick.tip"]);
        AutomationProperties.SetName(this, texts["referencePick.tip"]);
    }

    public IReferencePicker Picker { get; }
}
