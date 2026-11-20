using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Forms;

public sealed class InputHelpNote
{
    private const double Gap = 4;

    private readonly TextBlock _title;
    private readonly TextBlock _message;

    public InputHelpNote(DesignTokens.Metrics metrics, string name)
    {
        _title = new TextBlock
        {
            FontSize = metrics.FontSize,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        _message = new TextBlock
        {
            FontSize = metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        Box = new Border
        {
            Name = name,
            IsHitTestVisible = false,
            IsVisible = false,
            MaxWidth = 260,
            Padding = new Thickness(metrics.SpacingSmall),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.Radius),
            Child = new StackPanel { Spacing = 2, Children = { _title, _message } },
        }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
            .Themed(Border.BorderBrushProperty, Palette.Keys.BorderStrong);
    }

    public Border Box { get; }

    public string Title => _title.Text ?? string.Empty;

    public string Message => _message.Text ?? string.Empty;

    public void Show(string title, string message, Rect cell)
    {
        _title.Text = title;
        _title.IsVisible = title.Length > 0;
        _message.Text = message;
        _message.IsVisible = message.Length > 0;
        Avalonia.Automation.AutomationProperties.SetName(Box, title.Length == 0 ? message : message.Length == 0 ? title : $"{title}: {message}");
        Canvas.SetLeft(Box, cell.Right + Gap);
        Canvas.SetTop(Box, cell.Bottom + Gap);
        Box.IsVisible = true;
    }

    public void Hide() => Box.IsVisible = false;
}
