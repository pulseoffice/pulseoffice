using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public interface IFreeAreaPlacement
{
    DocumentRect Place(DocumentSize page, DocumentSize size, IEnumerable<DocumentRect> occupied);
}

public sealed class FreeAreaPlacement : IFreeAreaPlacement
{
    private const int Steps = 24;

    public DocumentRect Place(DocumentSize page, DocumentSize size, IEnumerable<DocumentRect> occupied)
    {
        var taken = occupied.Where(rect => !rect.IsEmpty).ToList();
        var centered = At(page, size, (page.Width.Value - size.Width.Value) / 2, (page.Height.Value - size.Height.Value) / 2);
        if (taken.All(rect => !rect.IntersectsWith(centered))) return centered;

        var freeWidth = Math.Max(0, page.Width.Value - size.Width.Value);
        var freeHeight = Math.Max(0, page.Height.Value - size.Height.Value);

        var free = Enumerable.Range(0, Steps + 1)
            .SelectMany(row => Enumerable.Range(0, Steps + 1).Select(column =>
                At(page, size, freeWidth * column / Steps, freeHeight * row / Steps)))
            .Where(candidate => taken.All(rect => !rect.IntersectsWith(candidate)))
            .OrderBy(candidate => Distance(candidate, centered))
            .ToList();

        return free.Count > 0 ? free[0] : Cascade(page, size, centered, taken.Count);
    }

    private static DocumentRect At(DocumentSize page, DocumentSize size, int left, int top) =>
        new(new Twip(Math.Max(0, left)), new Twip(Math.Max(0, top)), size.Width, size.Height);

    private static double Distance(DocumentRect a, DocumentRect b) =>
        Math.Pow(a.Left.Value - b.Left.Value, 2) + Math.Pow(a.Top.Value - b.Top.Value, 2);

    private static DocumentRect Cascade(DocumentSize page, DocumentSize size, DocumentRect centered, int count)
    {
        var step = Twip.FromCentimeters(0.5).Value * (count % 8 + 1);
        return At(page, size,
            Math.Min(centered.Left.Value + step, Math.Max(0, page.Width.Value - size.Width.Value)),
            Math.Min(centered.Top.Value + step, Math.Max(0, page.Height.Value - size.Height.Value)));
    }
}
