using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartPreview(ChartRenderer renderer, System.Globalization.CultureInfo culture, ChartRenderMode mode = ChartRenderMode.Full) : Control
{
    private ChartDefinition? _definition;
    private ChartData _data = ChartData.Empty;

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
        context.FillRectangle(Brushes.White, area);
        using (context.PushClip(area))
            renderer.Render(context, _definition, _data, area, mode, culture);
    }
}
