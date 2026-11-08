using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Scripting.Editor;

public sealed class MacroWriteAccessButton : Button
{
    public const string AutomationKey = "macro.fileWrite";

    private readonly Geometry _closedLock = Geometry.Parse("M6 11V8a6 6 0 0 1 12 0v3 M5 11h14v10H5z M12 15v3");
    private readonly Geometry _openLock = Geometry.Parse("M6 11V8a6 6 0 0 1 11.2-3 M5 11h14v10H5z M12 15v3");
    private readonly IMacroWriteAccess _access;
    private readonly IReadOnlyDictionary<string, string> _texts;
    private readonly Func<ScriptLanguage> _language;
    private readonly Func<string> _source;
    private readonly Avalonia.Controls.Shapes.Path _icon = new()
    {
        Width = 14,
        Height = 14,
        Stretch = Stretch.Uniform,
        StrokeThickness = 1.6,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _tint = new() { Opacity = 0.14, IsHitTestVisible = false };
    private bool _asking;

    protected override Type StyleKeyOverride => typeof(Button);

    public MacroWriteAccessButton(
        IMacroWriteAccess access,
        IReadOnlyDictionary<string, string> texts,
        Func<ScriptLanguage> language,
        Func<string> source,
        Thickness padding)
    {
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _source = source ?? throw new ArgumentNullException(nameof(source));

        Padding = padding;
        MinHeight = 28;
        BorderThickness = new Thickness(1);
        Background = Brushes.Transparent;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        _tint.Themed(Border.BackgroundProperty, Palette.Keys.Warning);
        _tint.Margin = new Thickness(-padding.Left, -padding.Top, -padding.Right, -padding.Bottom);
        Content = new Panel
        {
            Children =
            {
                _tint,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { _icon, _label },
                },
            },
        };
        ToolTip.SetTip(this, _texts["fileWrite.tip"]);
        AutomationProperties.SetHelpText(this, _texts["fileWrite.tip"]);
        AutomationProperties.SetAutomationId(this, AutomationKey);
        Click += async (_, _) => await ToggleAsync().ConfigureAwait(true);
        Refresh();
    }

    public bool IsGranted { get; private set; }

    public void Refresh()
    {
        IsGranted = _access.IsGranted(_language(), _source());
        var tone = IsGranted ? Palette.Keys.Warning : Palette.Keys.TextSecondary;
        _label.Text = IsGranted ? _texts["fileWrite.on"] : _texts["fileWrite.off"];
        _label.Themed(TextBlock.ForegroundProperty, tone);
        _icon.Data = IsGranted ? _openLock : _closedLock;
        _icon.Themed(Shape.StrokeProperty, tone);
        this.Themed(BorderBrushProperty, IsGranted ? Palette.Keys.Warning : Palette.Keys.Border);
        _tint.IsVisible = IsGranted;
        Classes.Set("granted", IsGranted);
        AutomationProperties.SetName(this, _label.Text);
    }

    private async Task ToggleAsync()
    {
        if (_asking) return;
        _asking = true;

        try
        {
            if (IsGranted) _access.Revoke(_language(), _source());
            else await _access.GrantAsync(_language(), _source(), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _asking = false;
            Refresh();
        }
    }
}
