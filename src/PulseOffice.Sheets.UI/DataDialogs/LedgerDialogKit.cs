using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed class LedgerDialogKit(ITextCatalog texts, IIconCatalog icons, IHelpTextSplitter splitter)
{
    public ITextCatalog Texts { get; } = texts;

    public IIconCatalog Icons { get; } = icons;

    public IHelpTextSplitter Splitter { get; } = splitter;

    public DesignTokens.Metrics Metrics { get; } = DesignTokens.For(InterfaceDensity.Compact);

    public string this[string key] => Texts[key];

    public HelpTip Help(string key) => new(Texts[key], Texts, Metrics, Splitter);

    public IconChoiceGroup Choices(
        string accessibleKey, IEnumerable<(string Key, string Icon, string LabelKey)> choices, int selected = 0,
        bool showLabels = false, int columns = 0) =>
        new([.. choices.Select(choice => new IconChoice(choice.Key, choice.Icon, Texts[choice.LabelKey]))],
            Icons, Metrics, Texts[accessibleKey], selected, showLabels, columns);

    public Control Icon(string key, double scale = 1) =>
        Icons.Find(key) is { } icon
            ? new IconView(icon, Metrics.IconSize * scale) { VerticalAlignment = VerticalAlignment.Center }
            : new Panel { Width = Metrics.IconSize * scale };

    public CheckBox Check(string labelKey, bool initial = false) =>
        new() { Content = Texts[labelKey], IsChecked = initial };

    public TextBlock Note(string text = "") => new TextBlock
    {
        Text = text,
        FontSize = Metrics.FontSizeSmall,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    public TextBlock Caption(string key) => new TextBlock
    {
        Text = Texts[key],
        FontSize = Metrics.FontSizeSmall,
        FontWeight = Avalonia.Media.FontWeight.SemiBold,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
}
