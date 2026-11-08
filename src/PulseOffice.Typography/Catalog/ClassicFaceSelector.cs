namespace PulseOffice.Typography.Catalog;

public interface IClassicFaceSelector
{
    IReadOnlyList<FontFaceRef> Select(IReadOnlyList<FontFaceRef> faces);
}

public sealed class ClassicFaceSelector : IClassicFaceSelector
{
    private static readonly (int Weight, bool Italic)[] Targets = [(400, false), (700, false), (400, true), (700, true)];

    public IReadOnlyList<FontFaceRef> Select(IReadOnlyList<FontFaceRef> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        if (faces.Count == 0) return [];

        var chosen = new List<FontFaceRef>();
        foreach (var (weight, italic) in Targets)
        {
            var match = faces
                .Where(face => face.Italic == italic)
                .OrderBy(face => Math.Abs(face.Weight - weight))
                .ThenBy(face => face.Weight)
                .FirstOrDefault();

            if (match is null) continue;
            if (Math.Abs(match.Weight - weight) > 150 && weight != 400) continue;
            if (!chosen.Any(face => Same(face, match))) chosen.Add(match);
        }

        if (chosen.Count == 0) chosen.Add(faces.OrderBy(face => Math.Abs(face.Weight - 400)).First());
        return chosen;
    }

    private bool Same(FontFaceRef left, FontFaceRef right) =>
        left == right || (left.Location.Length > 0 && left.Location == right.Location && left.CollectionIndex == right.CollectionIndex);
}
