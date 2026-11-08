using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Macros;

public sealed class MacroEventsChrome(ITextCatalog texts, IIconCatalog icons)
    : DialogChrome(icons, DesignTokens.For(InterfaceDensity.Compact))
{
    public const string MacroIcon = "shell.tools.macros";

    public ITextCatalog Texts { get; } = texts ?? throw new ArgumentNullException(nameof(texts));

    public TextBlock Title(string text) => new TextBlock
    {
        Text = text,
        FontSize = Metrics.FontSize + 1,
        FontWeight = FontWeight.Medium,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

    public Button Quiet(string iconKey, string label, string tip, Action run)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(Icon(iconKey, 14));
        content.Children.Add(new TextBlock { Text = label, FontSize = Metrics.FontSizeSmall + 0.5, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Content = content,
            MinHeight = 30,
            Padding = new Thickness(Metrics.SpacingSmall + 4, Metrics.SpacingSmall / 2 + 1),
            CornerRadius = new CornerRadius(Metrics.RadiusTight),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("dialogQuiet");
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => run();
        return button;
    }

    public Button Primary(string label, Action run)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 30,
            Padding = new Thickness(Metrics.SpacingMedium, Metrics.SpacingSmall / 2 + 1),
            CornerRadius = new CornerRadius(Metrics.RadiusTight),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("dialogPrimary");
        button.Click += (_, _) => run();
        return button;
    }

    public Border LanguagePill(MacroLanguage language)
    {
        var pill = new Border
        {
            Padding = new Thickness(Metrics.SpacingSmall, 0),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = Texts[LanguageKey(language)],
                FontSize = Math.Max(9, Metrics.FontSizeSmall - 1),
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        return pill;
    }

    public string LanguageKey(MacroLanguage language) =>
        language == MacroLanguage.Python ? "macroEvents.language.python" : "macroEvents.language.javascript";
}
