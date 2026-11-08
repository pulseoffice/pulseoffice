using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public interface IDialogFooterOwner
{
    Control? Footer { get; }
}

public sealed class DialogFooter : UserControl
{
    private readonly DesignTokens.Metrics _metrics;
    private readonly StackPanel _left;
    private readonly StackPanel _right;

    public DialogFooter(InterfaceDensity density = InterfaceDensity.Compact)
    {
        _metrics = DesignTokens.For(density);

        _left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        _right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        Grid.SetColumn(_left, 0);
        Grid.SetColumn(_right, 2);

        layout.Children.Add(_left);
        layout.Children.Add(_right);

        Content = layout;
    }

    public DialogFooter WithCancel(string label, Action run)
    {
        _left.Children.Add(Build(label, run, primary: false, cancel: true));

        return this;
    }

    public DialogFooter WithAction(string label, Action run, bool primary = false)
    {
        _right.Children.Add(Build(label, run, primary, cancel: false));

        return this;
    }

    public DialogFooter WithAction(string label, Action run, out Button button, bool primary = false)
    {
        button = Build(label, run, primary, cancel: false);
        _right.Children.Add(button);

        return this;
    }

    public DialogFooter WithNote(string note) => WithNote(note, out _);

    public DialogFooter WithNote(string note, out TextBlock block)
    {
        block = new TextBlock
        {
            Text = note,
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _left.Children.Add(block);

        return this;
    }

    private Button Build(string label, Action run, bool primary, bool cancel)
    {
        var button = new Button
        {
            Content = label,
            FontSize = _metrics.FontSize,
            FontWeight = primary ? FontWeight.SemiBold : FontWeight.Normal,
            Padding = new Thickness(_metrics.SpacingLarge, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = primary,
            IsCancel = cancel,
        };

        button.Classes.Add(primary ? "dialogPrimary" : "dialogQuiet");

        button.Click += (_, _) => run();

        return button;
    }
}
