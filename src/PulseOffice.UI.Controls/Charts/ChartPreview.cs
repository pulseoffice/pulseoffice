using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartPreview(ChartRenderer renderer, System.Globalization.CultureInfo culture, ChartRenderMode mode = ChartRenderMode.Full) : Control
{
    private ChartDefinition? _definition;
    private ChartData _data = ChartData.Empty;
    private readonly ResourceBrushes _brushes = new();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ActualThemeVariantChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    public IBrush Paper => _brushes.Find(this, Palette.Keys.Page, Brushes.White);

    public void Show(ChartDefinition definition, ChartData data)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _data = data ?? throw new ArgumentNullException(nameof(data));
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_definition is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var area = new Rect(Bounds.Size);
        context.FillRectangle(Paper, area);
        using (context.PushClip(area))
            renderer.Render(context, _definition, _data, area, mode, culture);
    }
}
