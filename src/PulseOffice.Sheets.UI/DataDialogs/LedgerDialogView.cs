using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public abstract class LedgerDialogView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private Button? _confirm;
    private TextBlock? _problem;

    protected LedgerDialogView(LedgerDialogKit kit, string explanationKey)
    {
        Kit = kit ?? throw new ArgumentNullException(nameof(kit));
        Layout = new TwoPaneDialogLayout(kit.Metrics) { Header = kit.Help(explanationKey) };
        Content = Layout;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; private set; }

    public virtual double? PreferredWidth => Layout.SuggestedWidth;

    public TwoPaneDialogLayout Layout { get; }

    public bool CanConfirm => _confirm?.IsEnabled ?? false;

    public string Problem => _problem?.IsVisible == true ? _problem.Text ?? string.Empty : string.Empty;

    protected LedgerDialogKit Kit { get; }

    public void Confirm()
    {
        if (_confirm?.IsEnabled == true && Result() is { } result) Completed?.Invoke(this, result);
    }

    public void Cancel() => Completed?.Invoke(this, null);

    public Button? SecondaryButton { get; private set; }

    protected abstract object? Result();

    protected void Complete(object result) => Completed?.Invoke(this, result);

    protected void Finish(string confirmKey, DialogSection problemSection, string? secondaryKey = null, Action? secondary = null)
    {
        ArgumentNullException.ThrowIfNull(problemSection);
        _problem = Kit.Note();
        _problem.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        _problem.IsVisible = false;
        problemSection.Add(null, _problem, DialogRowKind.Wide);
        var footer = new DialogFooter().WithCancel(Kit["dialog.cancel"], Cancel);
        if (secondaryKey is not null && secondary is not null)
        {
            footer.WithAction(Kit[secondaryKey], secondary, out var extra);
            SecondaryButton = extra;
        }

        Footer = footer.WithAction(Kit[confirmKey], Confirm, out _confirm, primary: true);
    }

    protected void Report(string? problem)
    {
        if (_problem is not null)
        {
            _problem.Text = problem ?? string.Empty;
            _problem.IsVisible = problem is { Length: > 0 };
        }

        if (_confirm is not null) _confirm.IsEnabled = problem is null;
    }

    protected void Hold(bool waiting)
    {
        if (_problem is not null) _problem.IsVisible = false;
        if (_confirm is not null) _confirm.IsEnabled = !waiting;
    }

    protected StackPanel PreviewPane(string captionKey, double width, params Control[] children)
    {
        var panel = new StackPanel { Width = width, Spacing = Kit.Metrics.SpacingSmall, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Kit.Caption(captionKey));
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    protected TextBox NumberBox(double value, double width = 110) => new()
    {
        Text = value.ToString(System.Globalization.CultureInfo.CurrentCulture),
        Width = width,
        HorizontalAlignment = HorizontalAlignment.Left,
        HorizontalContentAlignment = HorizontalAlignment.Right,
    };

    protected double? ReadNumber(TextBox box) =>
        double.TryParse(box?.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out var number)
            && double.IsFinite(number)
            ? number
            : null;

    protected Thickness Gap => new(0, Kit.Metrics.SpacingSmall, 0, 0);
}
