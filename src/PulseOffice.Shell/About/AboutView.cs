using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Drawing.Assets;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Shell.About;

public sealed record AboutFacts(string Version, string Runtime, string System, IReadOnlyList<AssetPack> Credits);

public sealed class AboutView : UserControl, IDialogContent, IDialogFooterOwner
{
    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public AboutView(AboutFacts facts, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(texts);

        var metrics = DesignTokens.For(density);
        var layout = new StackPanel { Spacing = metrics.SpacingMedium };

        layout.Children.Add(new TextBlock
        {
            Text = "PulseOffice",
            FontSize = metrics.FontSize * 2,
            FontWeight = FontWeight.SemiBold,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        layout.Children.Add(Line(texts.Format("about.version", facts.Version), metrics, Palette.Keys.TextPrimary));
        layout.Children.Add(Line(texts.Format("about.runtime", facts.Runtime, facts.System), metrics, Palette.Keys.TextSecondary));

        layout.Children.Add(new TextBlock
        {
            Text = texts["about.credits"],
            FontSize = metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, metrics.SpacingMedium, 0, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var credits = new StackPanel { Spacing = 2 };
        foreach (var pack in facts.Credits)
            credits.Children.Add(Line(pack.License.AttributionText ?? $"{pack.DisplayName} — {pack.License.Name}", metrics, Palette.Keys.TextSecondary));
        layout.Children.Add(new Border
        {
            Child = credits,
            Padding = new Thickness(metrics.SpacingMedium),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken));

        Content = new ScrollViewer { Content = layout, MaxHeight = 460 };

        Footer = new DialogFooter(density)
            .WithAction(texts["about.close"], () => Completed?.Invoke(this, null), primary: true);
    }

    private TextBlock Line(string text, DesignTokens.Metrics metrics, string brush) =>
        new TextBlock
        {
            Text = text,
            FontSize = metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, brush);
}
