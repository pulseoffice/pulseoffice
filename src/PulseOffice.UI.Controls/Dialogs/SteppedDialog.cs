using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dialogs;

public sealed class StepStrip : UserControl
{
    public StepStrip(IReadOnlyList<string> steps, int current, DesignTokens.Metrics metrics, string accessibleFormat)
    {
        ArgumentNullException.ThrowIfNull(steps);
        Steps = steps;
        Current = Math.Clamp(current, 0, steps.Count - 1);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        for (var index = 0; index < steps.Count; index++)
        {
            if (index > 0)
                row.Children.Add(new Border { Width = 24, Height = 1, VerticalAlignment = VerticalAlignment.Center }
                    .Themed(Border.BackgroundProperty, index <= Current ? "PulseAccent" : Palette.Keys.Border));

            var done = index < Current;
            var active = index == Current;
            var dot = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = done ? "✓" : (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
                    FontSize = metrics.FontSizeSmall,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }.Themed(TextBlock.ForegroundProperty, active || done ? "PulseAccentText" : Palette.Keys.TextSecondary),
            }.Themed(Border.BorderBrushProperty, active || done ? "PulseAccent" : Palette.Keys.Border)
             .Themed(Border.BackgroundProperty, active ? "PulseAccentSubtle" : Palette.Keys.Surface);

            var label = new TextBlock
            {
                Text = steps[index],
                FontSize = metrics.FontSize,
                FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, active ? Palette.Keys.TextPrimary : Palette.Keys.TextSecondary);

            row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { dot, label } });
        }

        AutomationProperties.SetName(this, string.Format(System.Globalization.CultureInfo.CurrentCulture, accessibleFormat, Current + 1, steps.Count, steps[Current]));
        Content = new Border { Padding = new Thickness(0, 0, 0, metrics.SpacingMedium), Child = row };
    }

    public IReadOnlyList<string> Steps { get; }

    public int Current { get; }
}

public sealed class SteppedDialog : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly Control _inner;

    public SteppedDialog(StepStrip strip, Control inner, string? nextLabel = null)
    {
        Strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (nextLabel is not null && strip.Current < strip.Steps.Count - 1 && inner is FormPromptView form) form.ConfirmLabel = nextLabel;
        if (inner is IDialogContent content) content.Completed += (_, value) => Completed?.Invoke(this, value);
        Content = new StackPanel { Children = { strip, inner } };
    }

    public event EventHandler<object?>? Completed;

    public StepStrip Strip { get; }

    public Control? Footer => (_inner as IDialogFooterOwner)?.Footer;

    public double? PreferredWidth => (_inner as IDialogSizeHint)?.PreferredWidth;

    public bool CanClose => _inner is not IDialogContent content || content.CanClose;
}
