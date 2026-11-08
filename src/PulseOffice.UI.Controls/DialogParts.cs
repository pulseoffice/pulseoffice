using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public interface IDialogParts
{
    Control Field(string caption, Control editor);

    Button Quiet(string label, string? icon, Action run);

    Button Accented(string label, Action run);

    TextBlock Caption(string text);

    Control ListRow(string? icon, string title, string subtitle);
}

public sealed class DialogParts(DesignTokens.Metrics metrics, IIconCatalog? icons) : IDialogParts
{
    private double SmallFont => Math.Max(9, metrics.FontSizeSmall - 1);

    public Control Field(string caption, Control editor)
    {
        var layout = new StackPanel { Spacing = 2 };
        layout.Children.Add(new TextBlock { Text = caption, FontSize = metrics.FontSizeSmall }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        layout.Children.Add(editor);
        return layout;
    }

    public Button Quiet(string label, string? icon, Action run)
    {
        ArgumentNullException.ThrowIfNull(label);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, VerticalAlignment = VerticalAlignment.Center };
        if (Icon(icon) is { } view) content.Children.Add(view);
        if (label.Length > 0)
            content.Children.Add(new TextBlock { Text = label, FontSize = metrics.FontSize, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(metrics.SpacingSmall, metrics.SpacingSmall / 2),
            BorderThickness = default,
        };
        button.Classes.Add("ribbon");
        button.Click += (_, _) => run();
        return button;
    }

    public Button Accented(string label, Action run)
    {
        var button = new Button
        {
            Content = label,
            FontSize = metrics.FontSize,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(metrics.SpacingMedium, metrics.SpacingSmall / 2),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
        };
        button.Classes.Add("dialogPrimary");
        button.Click += (_, _) => run();
        return button;
    }

    public TextBlock Caption(string text) =>
        new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = SmallFont,
            LetterSpacing = 0.6,
            Margin = new Thickness(0, 0, 0, metrics.SpacingSmall / 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    public Control ListRow(string? icon, string title, string subtitle)
    {
        var layout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        if (Icon(icon) is { } view) layout.Children.Add(view);
        var names = new StackPanel { Spacing = 0 };
        names.Children.Add(new TextBlock { Text = title, FontSize = metrics.FontSize });
        names.Children.Add(new TextBlock { Text = subtitle, FontSize = SmallFont }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        layout.Children.Add(names);
        return layout;
    }

    private IconView? Icon(string? key) =>
        key is not null && icons?.Find(key) is { } geometry
            ? new IconView(geometry, metrics.IconSize) { VerticalAlignment = VerticalAlignment.Center }
            : null;
}
