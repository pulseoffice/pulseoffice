using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dialogs;

public sealed class ChoiceOptionCard : Button
{
    public ChoiceOptionCard(PromptChoice choice, DesignTokens.Metrics metrics, IIconCatalog? icons)
    {
        ArgumentNullException.ThrowIfNull(choice);

        Choice = choice;
        IsDefault = choice.IsDefault;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Padding = new Thickness(metrics.SpacingMedium);
        CornerRadius = new CornerRadius(metrics.RadiusTight + 2);
        BorderThickness = new Thickness(choice.IsDefault ? 2 : 1);
        Classes.Add("choice-card");
        if (choice.IsDefault) Classes.Add("default");

        this.Themed(BackgroundProperty, Palette.Keys.SurfaceRaised);
        if (choice.IsDefault) this.Bind(BorderBrushProperty, this.GetResourceObservable("PulseAccent"));
        else this.Themed(BorderBrushProperty, Palette.Keys.Border);

        var badgeSize = metrics.IconSize * 2;
        var badge = new Border
        {
            Width = badgeSize,
            Height = badgeSize,
            CornerRadius = new CornerRadius(badgeSize / 2),
            VerticalAlignment = VerticalAlignment.Top,
            Child = Glyph(choice, metrics, icons),
        };
        badge.Bind(Border.BackgroundProperty, badge.GetResourceObservable("PulseAccentSubtle"));

        var title = new TextBlock
        {
            Text = choice.Label,
            FontSize = metrics.FontSize,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        var names = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(title);
        if (!string.IsNullOrEmpty(choice.Description))
            names.Children.Add(new TextBlock
            {
                Text = choice.Description,
                FontSize = metrics.FontSizeSmall,
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingMedium };
        Grid.SetColumn(names, 1);
        layout.Children.Add(badge);
        layout.Children.Add(names);
        Content = layout;

        AutomationProperties.SetName(this, choice.Label);
        if (!string.IsNullOrEmpty(choice.Description)) AutomationProperties.SetHelpText(this, choice.Description);
    }

    public PromptChoice Choice { get; }

    private static Control Glyph(PromptChoice choice, DesignTokens.Metrics metrics, IIconCatalog? icons)
    {
        if (choice.Icon is { } key && icons?.Find(key) is { } geometry)
            return new IconView(geometry, metrics.IconSize) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        var monogram = new TextBlock
        {
            Text = choice.Label.TrimStart().Length > 0 ? choice.Label.TrimStart()[..1].ToUpperInvariant() : "?",
            FontSize = metrics.FontSize,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        monogram.Bind(TextBlock.ForegroundProperty, monogram.GetResourceObservable("PulseAccent"));
        return monogram;
    }
}
