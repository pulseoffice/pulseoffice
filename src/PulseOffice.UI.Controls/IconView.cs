using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls;

public sealed class IconView : Control
{
    private readonly Geometry _geometry;
    private readonly double _scale;
    private readonly bool _filled;

    private const double StrokeOnGrid = 1.6;

    private Pen? _pen;

    public IconView(IconGeometry icon, double size)
    {
        ArgumentNullException.ThrowIfNull(icon);

        _geometry = Geometry.Parse(icon.Path);
        _filled = icon.Filled;
        _scale = size / IconGeometry.Grid;

        Width = size;
        Height = size;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground ?? Brushes.Black;

        using var _ = context.PushTransform(Matrix.CreateScale(_scale, _scale));
        context.DrawGeometry(_filled ? brush : null, _filled ? null : PenFor(brush), _geometry);
    }

    private Pen PenFor(IBrush brush)
    {
        if (_pen is null || !ReferenceEquals(_pen.Brush, brush))
            _pen = new Pen(brush, StrokeOnGrid) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

        return _pen;
    }

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
    TextElement.ForegroundProperty.AddOwner<IconView>();

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    static IconView()
    {
        AffectsRender<IconView>(ForegroundProperty);
    }
}
