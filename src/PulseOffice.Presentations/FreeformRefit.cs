using PulseOffice.Drawing.Freeform;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public interface IFreeformRefit
{
    ObjectElement Refit(ObjectElement element, FreeformObject freeform, FreeformPath path);
}

public sealed class FreeformRefit(IPathNormalizer normalizer) : IFreeformRefit
{
    public FreeformRefit() : this(new PathNormalizer()) { }

    public ObjectElement Refit(ObjectElement element, FreeformObject freeform, FreeformPath path)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(freeform);

        var normalized = normalizer.ToUnitBox(path);
        var box = normalized.Bounds;
        if (box.Width <= 0 && box.Height <= 0) return element with { Content = freeform with { Path = path } };

        var bounds = element.Bounds;
        var width = bounds.Width.Value;
        var height = bounds.Height.Value;

        return element with
        {
            Bounds = new DocumentRect(
                new Twip((int)Math.Round(bounds.Left.Value + box.X * width)),
                new Twip((int)Math.Round(bounds.Top.Value + box.Y * height)),
                new Twip((int)Math.Max(1, Math.Round(box.Width * width))),
                new Twip((int)Math.Max(1, Math.Round(box.Height * height)))),
            Content = freeform with { Path = normalized.Path },
        };
    }
}
