namespace PulseOffice.Foundation.Review;

public interface IAuthorPalette
{
    IReadOnlyDictionary<string, uint> For(IEnumerable<string> authors);
}

public sealed class AuthorPalette : IAuthorPalette
{
    private readonly uint[] _colors =
    [
        0xFFC69200, 0xFF0646A2, 0xFF579D1C, 0xFF692B9D, 0xFFC5000B,
        0xFF008080, 0xFF8C8400, 0xFF35556B, 0xFFD17600,
    ];

    public IReadOnlyDictionary<string, uint> For(IEnumerable<string> authors)
    {
        var colors = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var author in authors)
        {
            if (!colors.ContainsKey(author)) colors[author] = _colors[colors.Count % _colors.Length];
        }
        return colors;
    }
}
