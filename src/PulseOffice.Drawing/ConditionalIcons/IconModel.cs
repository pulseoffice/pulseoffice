using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing.ConditionalIcons;

public sealed record IconLayer(ShapeOutline Outline, string? Fill, string? Stroke, double StrokeWidth = 0);

public sealed record ConditionalIcon(IReadOnlyList<IconLayer> Layers);

public sealed record IconTone(string Fill, string Stroke);

public sealed class IconPalette
{
    public IconTone Green { get; } = new("#A1DDAA", "#309048");
    public IconTone Yellow { get; } = new("#F8DB8F", "#ED8733");
    public IconTone Red { get; } = new("#FF9198", "#D42314");
    public IconTone Gray { get; } = new("#797774", "#3A3A38");
    public IconTone LightGray { get; } = new("#C8C6C4", "#797774");
    public IconTone DarkGray { get; } = new("#3A3A38", "#3A3A38");
    public IconTone StrongRed { get; } = new("#ED3D3B", "#D42314");
    public IconTone DarkRed { get; } = new("#D42314", "#D42314");
    public IconTone LampGreen { get; } = new("#18AB50", "#3A3A38");
    public IconTone LampYellow { get; } = new("#FB983B", "#3A3A38");
    public IconTone LampRed { get; } = new("#ED3D3B", "#3A3A38");
    public string Ink => "#3A3A38";
    public string Paper => "#FAFAFA";
    public string Blue => "#0063B1";
}

public interface IIconGlyph
{
    ConditionalIcon Draw();
}

public abstract class IconGlyph : IIconGlyph
{
    protected const double Stroke = 1.0 / 16;

    public ConditionalIcon Draw() => new([.. Layers()]);

    protected abstract IEnumerable<IconLayer> Layers();

    protected IconLayer Filled(ShapeOutline outline, IconTone tone) => new(outline, tone.Fill, tone.Stroke, Stroke);

    protected ShapeOutline Rotated(ShapeOutline outline, double degrees) =>
        new SegmentMapper(point => Turn(point, degrees)).Map(outline);

    private ShapePoint Turn(ShapePoint point, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var (dx, dy) = (point.X - 0.5, point.Y - 0.5);
        return new ShapePoint(0.5 + dx * Math.Cos(radians) - dy * Math.Sin(radians), 0.5 + dx * Math.Sin(radians) + dy * Math.Cos(radians));
    }
}
