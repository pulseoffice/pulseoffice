using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Formats;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class TransitionStage(ITransitionEffectFactory effects, SmilTransitionMapper smil)
{
    public TransitionStage() : this(new TransitionEffectFactory(), new SmilTransitionMapper())
    {
    }

    public ITransitionEffect EffectFor(SlideTransition transition) => effects.Create(smil.ToSmil(transition));

    public void Apply(Control layer, SlideLayerState state, Size size)
    {
        layer.Opacity = Math.Clamp(state.Opacity, 0, 1);
        layer.RenderTransformOrigin = RelativePoint.Center;
        layer.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(state.ScaleX, state.ScaleY),
                new RotateTransform(state.Rotation),
                new TranslateTransform(state.OffsetX * size.Width, state.OffsetY * size.Height),
            },
        };
        layer.Clip = state.Clip is { } shapes ? Clip(shapes, size) : null;
    }

    public void Reset(Control layer)
    {
        layer.Opacity = 1;
        layer.RenderTransform = null;
        layer.Clip = null;
    }

    private Geometry Clip(IReadOnlyList<TransitionShape> shapes, Size size)
    {
        var group = new GeometryGroup { FillRule = FillRule.NonZero };
        foreach (var shape in shapes)
            group.Children.Add(shape switch
            {
                TransitionRectangle rectangle => new RectangleGeometry(new Rect(rectangle.X * size.Width, rectangle.Y * size.Height,
                    Math.Max(0, rectangle.Width * size.Width), Math.Max(0, rectangle.Height * size.Height))),
                TransitionEllipse ellipse => new EllipseGeometry(new Rect(
                    (ellipse.CenterX - ellipse.RadiusX) * size.Width, (ellipse.CenterY - ellipse.RadiusY) * size.Height,
                    Math.Max(0, ellipse.RadiusX * 2 * size.Width), Math.Max(0, ellipse.RadiusY * 2 * size.Height))),
                TransitionPolygon polygon => new PolylineGeometry(
                    [.. polygon.Points.Select(point => new Point(point.X * size.Width, point.Y * size.Height))], true),
                _ => new RectangleGeometry(),
            });
        return group;
    }
}
