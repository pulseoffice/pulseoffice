using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using Path = Avalonia.Controls.Shapes.Path;

namespace PulseOffice.UI.Controls;

public sealed class CaptionButtons : StackPanel
{
    private const double ButtonWidth = 46;
    private const double GlyphSize = 10;

    private readonly ITextCatalog _texts;
    private readonly Button _minimize;
    private readonly Button _maximize;
    private readonly Button _close;
    private readonly Path _maximizeGlyph;
    private Window? _window;

    public CaptionButtons(ITextCatalog texts)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Stretch;

        _minimize = Caption(Glyph("M0,5 L10,5"), WindowDecorationsElementRole.MinimizeButton);
        _maximizeGlyph = Glyph(MaximizeData);
        _maximize = Caption(_maximizeGlyph, WindowDecorationsElementRole.MaximizeButton);
        _close = Caption(Glyph("M0,0 L10,10 M10,0 L0,10"), WindowDecorationsElementRole.CloseButton);
        _close.Classes.Add("close");
        _minimize.Click += (_, _) =>
        {
            if (_window is not null) _window.WindowState = WindowState.Minimized;
        };
        _maximize.Click += (_, _) =>
        {
            if (_window is not null)
                _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        };
        _close.Click += (_, _) => _window?.Close();

        Children.Add(_minimize);
        Children.Add(_maximize);
        Children.Add(_close);

        AttachedToVisualTree += (_, _) =>
        {
            _window = TopLevel.GetTopLevel(this) as Window;
            if (_window is not null) _window.PropertyChanged += OnWindowPropertyChanged;
            _texts.LanguageChanged += OnLanguageChanged;
            Refresh();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_window is not null) _window.PropertyChanged -= OnWindowPropertyChanged;
            _texts.LanguageChanged -= OnLanguageChanged;
            _window = null;
        };
    }

    private const string MaximizeData = "M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z";

    private const string RestoreData = "M2.5,0.5 L9.5,0.5 L9.5,7.5 M0.5,2.5 L7.5,2.5 L7.5,9.5 L0.5,9.5 Z";

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty) Refresh();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var maximized = _window?.WindowState == WindowState.Maximized;
        _maximizeGlyph.Data = Geometry.Parse(maximized ? RestoreData : MaximizeData);
        Describe(_minimize, "titlebar.minimize");
        Describe(_maximize, maximized ? "titlebar.restore" : "titlebar.maximize");
        Describe(_close, "titlebar.close");
    }

    private void Describe(Button button, string key)
    {
        var text = _texts[key];
        Avalonia.Automation.AutomationProperties.SetName(button, text);
        ToolTip.SetTip(button, text);
    }

    private Path Glyph(string data)
    {
        var glyph = new Path
        {
            Data = Geometry.Parse(data),
            Width = GlyphSize,
            Height = GlyphSize,
            StrokeThickness = 1,
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        glyph.Bind(Shape.StrokeProperty, new Avalonia.Data.Binding(nameof(Avalonia.Controls.Presenters.ContentPresenter.Foreground))
        {
            RelativeSource = new Avalonia.Data.RelativeSource(Avalonia.Data.RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Avalonia.Controls.Presenters.ContentPresenter),
            },
        });
        return glyph;
    }

    private Button Caption(Path glyph, WindowDecorationsElementRole role)
    {
        var button = new Button
        {
            Content = glyph,
            Width = ButtonWidth,
            Padding = default,
            Margin = default,
            CornerRadius = default,
            BorderThickness = default,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("caption");
        WindowDecorationProperties.SetElementRole(button, role);
        return button;
    }
}
