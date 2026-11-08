using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public interface ISlideGroupRotation
{
    IReadOnlyDictionary<string, SlideElement> InGroupFrames(Slide slide);
}

public sealed class SlideGroupRotation(ISlideGroupUnits units, ISlideGroupDecomposition decomposition) : ISlideGroupRotation
{
    public SlideGroupRotation() : this(new SlideGroupUnits(), new SlideGroupDecomposition())
    {
    }

    public IReadOnlyDictionary<string, SlideElement> InGroupFrames(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var placed = new Dictionary<string, SlideElement>(StringComparer.Ordinal);
        var outermostFirst = decomposition.InnermostFirst(slide.Groups).Reverse().Where(group => Math.Abs(group.RotationDegrees) > 1e-9);
        foreach (var group in outermostFirst)
        {
            var leaves = units.Leaves(slide, group)
                .Select(id => placed.GetValueOrDefault(id) ?? slide.Find(id)).OfType<SlideElement>().ToList();
            if (leaves.Count == 0) continue;
            var radians = group.RotationDegrees * Math.PI / 180;
            var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
            var back = leaves.Select(leaf => (Leaf: leaf, Centre: Turned(Centre(leaf), cos, -sin))).ToList();
            var left = back.Min(item => item.Centre.X - item.Leaf.Bounds.Width.Value / 2.0);
            var right = back.Max(item => item.Centre.X + item.Leaf.Bounds.Width.Value / 2.0);
            var top = back.Min(item => item.Centre.Y - item.Leaf.Bounds.Height.Value / 2.0);
            var bottom = back.Max(item => item.Centre.Y + item.Leaf.Bounds.Height.Value / 2.0);
            var pivot = Turned(((left + right) / 2, (top + bottom) / 2), cos, sin);
            foreach (var leaf in leaves)
            {
                var centre = Centre(leaf);
                var local = Turned((centre.X - pivot.X, centre.Y - pivot.Y), cos, -sin);
                var (x, y) = (local.X + pivot.X, local.Y + pivot.Y);
                placed[leaf.Id] = leaf with
                {
                    Bounds = new DocumentRect(
                        new Twip((int)Math.Round(x - leaf.Bounds.Width.Value / 2.0)),
                        new Twip((int)Math.Round(y - leaf.Bounds.Height.Value / 2.0)),
                        leaf.Bounds.Width, leaf.Bounds.Height),
                    RotationDegrees = Normalized(leaf.RotationDegrees - group.RotationDegrees),
                };
            }
        }
        return placed;
    }

    private (double X, double Y) Centre(SlideElement element) =>
        (element.Bounds.Left.Value + element.Bounds.Width.Value / 2.0, element.Bounds.Top.Value + element.Bounds.Height.Value / 2.0);

    private (double X, double Y) Turned((double X, double Y) point, double cos, double sin) =>
        (point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);

    private double Normalized(double degrees)
    {
        var angle = degrees % 360;
        if (angle < 0) angle += 360;
        return Math.Abs(angle - 360) < 1e-9 ? 0 : angle;
    }
}
