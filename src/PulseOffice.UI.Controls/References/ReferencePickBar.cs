using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.References;

public sealed class ReferencePickBar : UserControl, IReferencePickListener
{
    private readonly ReferencePickRequest _request;
    private readonly TextBox _entry;
    private readonly TextBlock _error;
    private readonly Button _done;
    private bool _finished;

    public event EventHandler<string?>? Finished;

    public ReferencePickBar(string dialogTitle, ReferencePickRequest request, ITextCatalog texts, IIconCatalog icons)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(icons);

        var metrics = DesignTokens.For(InterfaceDensity.Compact);

        var heading = new TextBlock
        {
            Text = Heading(dialogTitle, request.Label),
            FontSize = Math.Max(9, metrics.FontSizeSmall - 1),
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 280,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _entry = new TextBox
        {
            Text = request.Current,
            Width = 260,
            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _entry.TextChanged += (_, _) => Check();
        _entry.KeyDown += OnEntryKeyDown;
        AutomationProperties.SetName(_entry, request.Label);

        _error = new TextBlock
        {
            Text = texts["range.invalid"],
            FontSize = metrics.FontSizeSmall,
            IsVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        _done = Action(icons, "ui.check", texts["referencePick.done"], metrics, primary: true);
        _done.Click += (_, _) => Commit();

        var cancel = Action(icons, "ui.close", texts["referencePick.cancel"], metrics, primary: false);
        cancel.Click += (_, _) => Cancel();

        var hint = new TextBlock
        {
            Text = texts["referencePick.hint"],
            FontSize = metrics.FontSizeSmall,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingMedium,
            Children = { heading, _entry, _done, cancel },
        };

        var accent = new Border { Height = 3 };
        accent.Bind(Border.BackgroundProperty, accent.GetResourceObservable("PulseAccent"));

        var body = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(metrics.SpacingLarge, metrics.SpacingMedium, metrics.SpacingMedium, metrics.SpacingMedium),
            Children = { row, _error, hint },
        };

        var frame = new Border
        {
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { accent, body } },
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceOverlay)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        Content = new Border
        {
            Child = frame,
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            BoxShadow = BoxShadows.Parse("0 12 32 -8 #3D000000"),
        };

        Check();
    }

    public string Text => (_entry.Text ?? string.Empty).Trim();

    public bool IsValid => Text.Length > 0 && _request.IsValid(Text);

    public void FocusEntry() =>
        Dispatcher.UIThread.Post(() =>
        {
            _entry.Focus(NavigationMethod.Tab);
            _entry.SelectAll();
        }, DispatcherPriority.Loaded);

    public void SelectionChanged(string reference)
    {
        if (_finished) return;
        _entry.Text = reference;
    }

    public void Commit()
    {
        if (_finished) return;

        if (Text.Length > 0 && !IsValid)
        {
            _error.IsVisible = true;
            return;
        }

        Finish(Text);
    }

    public void Cancel()
    {
        if (_finished) return;
        Finish(null);
    }

    private void Finish(string? result)
    {
        _finished = true;
        Finished?.Invoke(this, result);
    }

    private void Check()
    {
        _error.IsVisible = Text.Length > 0 && !_request.IsValid(Text);
        _done.IsEnabled = Text.Length == 0 || _request.IsValid(Text);
    }

    private void OnEntryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Commit();
                e.Handled = true;
                break;

            case Key.Escape:
                Cancel();
                e.Handled = true;
                break;
        }
    }

    private string Heading(string title, string label)
    {
        var clean = title.TrimEnd('.', '…', ' ').ToUpper(CultureInfo.CurrentCulture);
        if (label.Length == 0) return clean;
        return clean.Length == 0 ? label : clean + " · " + label;
    }

    private Button Action(IIconCatalog icons, string iconKey, string tip, DesignTokens.Metrics metrics, bool primary)
    {
        var button = new Button
        {
            Width = metrics.ControlHeight + 4,
            Height = metrics.ControlHeight + 4,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = icons.Find(iconKey) is { } geometry
                ? new IconView(geometry, metrics.IconSize)
                : new TextBlock { Text = primary ? "✓" : "✕" },
        };

        button.Classes.Add(primary ? "dialogPrimary" : "dialogQuiet");
        ToolTip.SetTip(button, tip);
        AutomationProperties.SetName(button, tip);
        return button;
    }
}
