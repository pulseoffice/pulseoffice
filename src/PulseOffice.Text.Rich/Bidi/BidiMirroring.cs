namespace PulseOffice.Text.Rich.Bidi;

public enum BracketKind
{
    None,
    Opening,
    Closing,
}

public interface IBidiMirroring
{
    int Mirror(int codePoint);

    BracketKind Bracket(int codePoint);

    int Canonical(int codePoint);
}

public sealed class BidiMirroring : IBidiMirroring
{
    private readonly Dictionary<int, int> _mirrors = [];
    private readonly HashSet<int> _opening = [];
    private readonly HashSet<int> _closing = [];

    public BidiMirroring()
    {
        int[][] brackets =
        [
            [0x0028, 0x0029], [0x005B, 0x005D], [0x007B, 0x007D], [0x0F3A, 0x0F3B], [0x0F3C, 0x0F3D], [0x169B, 0x169C],
            [0x2045, 0x2046], [0x207D, 0x207E], [0x208D, 0x208E], [0x2308, 0x2309], [0x230A, 0x230B], [0x2329, 0x232A],
            [0x2768, 0x2769], [0x276A, 0x276B], [0x276C, 0x276D], [0x276E, 0x276F], [0x2770, 0x2771], [0x2772, 0x2773],
            [0x2774, 0x2775], [0x27C5, 0x27C6], [0x27E6, 0x27E7], [0x27E8, 0x27E9], [0x27EA, 0x27EB], [0x2983, 0x2984],
            [0x2985, 0x2986], [0x2987, 0x2988], [0x2989, 0x298A], [0x298B, 0x298C], [0x3008, 0x3009], [0x300A, 0x300B],
            [0x300C, 0x300D], [0x300E, 0x300F], [0x3010, 0x3011], [0x3014, 0x3015], [0x3016, 0x3017], [0x3018, 0x3019],
            [0x301A, 0x301B], [0xFE59, 0xFE5A], [0xFE5B, 0xFE5C], [0xFE5D, 0xFE5E], [0xFF08, 0xFF09], [0xFF3B, 0xFF3D],
            [0xFF5B, 0xFF5D], [0xFF5F, 0xFF60], [0xFF62, 0xFF63],
        ];
        int[][] others =
        [
            [0x003C, 0x003E], [0x00AB, 0x00BB], [0x2039, 0x203A], [0x2264, 0x2265], [0x2266, 0x2267], [0x226A, 0x226B],
            [0x2282, 0x2283], [0x2286, 0x2287], [0x22A2, 0x22A3], [0x2208, 0x220B], [0x2209, 0x220C], [0x220A, 0x220D],
            [0x2243, 0x22CD], [0x2272, 0x2273], [0x2276, 0x2277], [0x227A, 0x227B], [0x227C, 0x227D], [0x22B0, 0x22B1],
            [0xFF1C, 0xFF1E],
        ];
        foreach (var pair in brackets)
        {
            Pair(pair[0], pair[1]);
            _opening.Add(pair[0]);
            _closing.Add(pair[1]);
        }
        foreach (var pair in others) Pair(pair[0], pair[1]);
    }

    public int Mirror(int codePoint) => _mirrors.GetValueOrDefault(codePoint, codePoint);

    public BracketKind Bracket(int codePoint) =>
        _opening.Contains(codePoint) ? BracketKind.Opening : _closing.Contains(codePoint) ? BracketKind.Closing : BracketKind.None;

    public int Canonical(int codePoint) => codePoint switch
    {
        0x2329 => 0x3008,
        0x232A => 0x3009,
        _ => codePoint,
    };

    private void Pair(int left, int right)
    {
        _mirrors[left] = right;
        _mirrors[right] = left;
    }
}
