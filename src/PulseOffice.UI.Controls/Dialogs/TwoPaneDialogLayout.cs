using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dialogs;

public sealed class TwoPaneDialogLayout : UserControl
{
    private readonly DesignTokens.Metrics _metrics;
    private readonly Grid _grid;
    private readonly StackPanel _settings;
    private readonly Border _previewHost;
    private readonly TextBlock _explanation;
    private readonly ContentControl _header;
    private readonly List<DialogSection> _sections = [];
    private bool? _previewBelow;

    public TwoPaneDialogLayout(DesignTokens.Metrics metrics)
    {
        _metrics = metrics;

        _explanation = new TextBlock
        {
            FontSize = metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _header = new ContentControl { Content = _explanation, Margin = new Thickness(0, 0, 0, metrics.SpacingLarge) };

        _settings = new StackPanel { Spacing = metrics.SpacingLarge };
        Grid.SetIsSharedSizeScope(_settings, true);

        _previewHost = new Border
        {
            IsVisible = false,
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            Padding = new Thickness(metrics.SpacingMedium),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Top,
        }
        .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken)
        .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        _grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        Grid.SetColumnSpan(_header, 2);
        Grid.SetRow(_settings, 1);
        _grid.Children.Add(_header);
        _grid.Children.Add(_settings);
        _grid.Children.Add(_previewHost);

        PlacePreview(below: false);
        ShowHeader();
        Content = _grid;
    }

    private void ShowHeader() => _header.IsVisible = _header.Content is Control content && (content != _explanation || _explanation.IsVisible);

    public string? Explanation
    {
        get => _explanation.Text;
        set
        {
            _explanation.Text = value;
            _explanation.IsVisible = !string.IsNullOrEmpty(value);
            ShowHeader();
        }
    }

    public Control? Header
    {
        get => _header.Content as Control;
        set
        {
            _header.Content = value ?? _explanation;
            ShowHeader();
        }
    }

    public Control? Preview
    {
        get => _previewHost.Child;
        set
        {
            _previewHost.Child = value;
            _previewHost.IsVisible = value is not null;
            _grid.ColumnSpacing = value is null || IsPreviewBelow ? 0 : _metrics.SpacingLarge;
        }
    }

    public Border PreviewHost => _previewHost;

    public IReadOnlyList<DialogSection> Sections => _sections;

    public bool IsPreviewBelow => _previewBelow == true;

    public DialogSection AddSection(string? title)
    {
        var section = new DialogSection(title, _metrics);
        return AddSection(section);
    }

    public DialogSection AddSection(DialogSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        _sections.Add(section);
        _settings.Children.Add(section);
        return section;
    }

    public double SuggestedWidth
    {
        get
        {
            var label = _sections.Count == 0 ? 0 : _sections.Max(section => section.LabelWidth);
            var settings = _sections.Count == 0 ? 0 : _sections.Max(section => section.SuggestedWidth(label));
            var width = settings + (Preview is null ? 0 : _metrics.SpacingLarge + PreviewWidth);
            return Math.Clamp(width + DialogLayoutMeasures.ChromeAllowance, DialogLayoutMeasures.MinimumDialogWidth, DialogLayoutMeasures.MaximumDialogWidth);
        }
    }

    private double PreviewWidth
    {
        get
        {
            if (Preview is not { } preview) return 0;
            if (!double.IsNaN(preview.Width)) return preview.Width + 2 * _metrics.SpacingMedium;
            preview.Measure(new Size(DialogLayoutMeasures.PreviewWidth * 1.5, double.PositiveInfinity));
            return Math.Max(DialogLayoutMeasures.PreviewWidth, preview.DesiredSize.Width + 2 * _metrics.SpacingMedium);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        PlacePreview(!double.IsInfinity(availableSize.Width) && availableSize.Width < DialogLayoutMeasures.PreviewBelowUnder);
        return base.MeasureOverride(availableSize);
    }

    private void PlacePreview(bool below)
    {
        if (_previewBelow == below) return;
        _previewBelow = below;

        Grid.SetRow(_previewHost, below ? 2 : 1);
        Grid.SetColumn(_previewHost, below ? 0 : 1);
        Grid.SetColumnSpan(_settings, below ? 2 : 1);
        Grid.SetColumnSpan(_previewHost, below ? 2 : 1);
        _grid.ColumnSpacing = below || Preview is null ? 0 : _metrics.SpacingLarge;
        _previewHost.Margin = below ? new Thickness(0, _metrics.SpacingLarge, 0, 0) : default;
        _previewHost.MaxWidth = below ? double.PositiveInfinity : DialogLayoutMeasures.PreviewWidth * 1.5;
    }
}
