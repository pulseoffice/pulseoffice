using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Preview;

public interface IPreviewSource
{
    Control? Render();
}

public enum PreviewBackdrop
{
    Surface,
    Checkerboard,
    Page,
}

public sealed record LivePreviewOptions
{
    public PreviewBackdrop Backdrop { get; init; } = PreviewBackdrop.Surface;

    public bool FitToFrame { get; init; } = true;

    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(60);

    public double Height { get; init; } = 160;

    public Thickness Inset { get; init; } = new(12);

    public string UnavailableText { get; init; } = string.Empty;

    public string AccessibleName { get; init; } = string.Empty;
}

public sealed class LivePreviewPane : UserControl, IDisposable
{
    private readonly IPreviewSource _source;
    private readonly IDebounceTimer _timer;
    private readonly LivePreviewOptions _options;
    private readonly ILogger? _logger;
    private readonly ContentControl _stage = new() { HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _unavailable = new();

    public LivePreviewPane(IPreviewSource source, IDebounceTimerFactory timers, LivePreviewOptions options, DesignTokens.Metrics metrics, ILogger? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(timers);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _timer = timers.Create(options.Delay, RefreshNow);

        _unavailable.Text = options.UnavailableText;
        _unavailable.FontSize = metrics.FontSizeSmall;
        _unavailable.TextWrapping = TextWrapping.Wrap;
        _unavailable.TextAlignment = TextAlignment.Center;
        _unavailable.HorizontalAlignment = HorizontalAlignment.Center;
        _unavailable.VerticalAlignment = VerticalAlignment.Center;
        _unavailable.Margin = new Thickness(metrics.SpacingMedium);
        _unavailable.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        if (options.AccessibleName.Length > 0) Avalonia.Automation.AutomationProperties.SetName(this, options.AccessibleName);

        var surface = new Panel();
        if (options.Backdrop == PreviewBackdrop.Checkerboard)
            surface.Children.Add(new CheckerboardBackdrop()
                .Themed(CheckerboardBackdrop.LightProperty, Palette.Keys.Surface)
                .Themed(CheckerboardBackdrop.DarkProperty, Palette.Keys.SurfaceSunken));
        _stage.Padding = options.Inset;
        surface.Children.Add(_stage);

        var frame = new Border
        {
            Height = options.Height,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            ClipToBounds = true,
            Child = surface,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, options.Backdrop == PreviewBackdrop.Surface ? Palette.Keys.Surface : Palette.Keys.SurfaceSunken);
        Content = frame;
        DetachedFromVisualTree += (_, _) => _timer.Dispose();
        RefreshNow();
    }

    public bool IsAvailable { get; private set; }

    public Control? Rendered { get; private set; }

    public bool IsPending => _timer.IsPending;

    public void Invalidate() => _timer.Kick();

    public void RefreshNow()
    {
        Control? rendered;
        try
        {
            rendered = _source.Render();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException or FormatException)
        {
            _logger?.LogWarning(ex, "Live preview could not be rendered");
            rendered = null;
        }

        Rendered = rendered;
        IsAvailable = rendered is not null;
        _stage.Content = rendered is null ? _unavailable : Present(rendered);
    }

    public void Dispose() => _timer.Dispose();

    private Control Present(Control rendered)
    {
        var shown = _options.Backdrop == PreviewBackdrop.Page
            ? new Border { Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 1 4 0 #33000000"), Child = rendered }
            : rendered;
        return _options.FitToFrame ? new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = shown } : shown;
    }
}

public sealed class CheckerboardBackdrop : Control
{
    public static readonly StyledProperty<IBrush?> LightProperty = AvaloniaProperty.Register<CheckerboardBackdrop, IBrush?>(nameof(Light), Brushes.White);

    public static readonly StyledProperty<IBrush?> DarkProperty = AvaloniaProperty.Register<CheckerboardBackdrop, IBrush?>(nameof(Dark), Brushes.LightGray);

    public static readonly StyledProperty<double> CellProperty = AvaloniaProperty.Register<CheckerboardBackdrop, double>(nameof(Cell), 8);

    static CheckerboardBackdrop() => AffectsRender<CheckerboardBackdrop>(LightProperty, DarkProperty, CellProperty);

    public IBrush? Light { get => GetValue(LightProperty); set => SetValue(LightProperty, value); }

    public IBrush? Dark { get => GetValue(DarkProperty); set => SetValue(DarkProperty, value); }

    public double Cell { get => GetValue(CellProperty); set => SetValue(CellProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (Light is { } light) context.FillRectangle(light, new Rect(size));
        if (Dark is not { } dark || Cell <= 0) return;
        for (var y = 0; y * Cell < size.Height; y++)
            for (var x = y % 2; x * Cell < size.Width; x += 2)
                context.FillRectangle(dark, new Rect(x * Cell, y * Cell, Cell, Cell));
    }
}
