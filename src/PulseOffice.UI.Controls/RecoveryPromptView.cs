using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class RecoveryPromptView : UserControl, IDialogContent, IDialogFooterOwner
{
    public sealed record Entry(string Id, string Title, DateTimeOffset SavedAt, string? OriginalPath);

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public RecoveryPromptView(
        IReadOnlyList<Entry> entries, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(texts);

        var metrics = DesignTokens.For(density);
        var layout = new StackPanel { Spacing = metrics.SpacingMedium };

        layout.Children.Add(new TextBlock
        {
            Text = texts["recovery.explanation"],
            FontSize = metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        foreach (var entry in entries)
        {
            var row = new StackPanel { Spacing = 2 };

            row.Children.Add(new TextBlock
            {
                Text = entry.Title,
                FontSize = metrics.FontSize,
                FontWeight = FontWeight.SemiBold,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

            row.Children.Add(new TextBlock
            {
                Text = entry.OriginalPath is { } path
                    ? texts.Format("recovery.item.from", entry.SavedAt.ToString("g"), path)
                    : texts.Format("recovery.item.unsaved", entry.SavedAt.ToString("g")),
                FontSize = metrics.FontSizeSmall,
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

            layout.Children.Add(new Border
            {
                Child = row,
                Padding = new Thickness(metrics.SpacingMedium),
                CornerRadius = new CornerRadius(metrics.RadiusTight),
            }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken));
        }

        Content = new ScrollViewer { Content = layout, MaxHeight = 420 };

        Footer = new DialogFooter(density)
            .WithCancel(texts["recovery.discard"], () => Completed?.Invoke(this, false))
            .WithAction(texts["recovery.restore"], () => Completed?.Invoke(this, true), primary: true);
    }
}
