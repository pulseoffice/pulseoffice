using System.Collections.Concurrent;

namespace PulseOffice.Drawing.ConditionalIcons;

public interface IConditionalIconCatalog
{
    IReadOnlyList<string> Names { get; }

    int Count(string set);

    ConditionalIcon Icon(string set, int index);
}

public sealed class ConditionalIconCatalog : IConditionalIconCatalog
{
    private const string Fallback = "3Arrows";
    private readonly Dictionary<string, IReadOnlyList<IIconGlyph>> _families;
    private readonly ConcurrentDictionary<(string, int), ConditionalIcon> _cache = new();

    public ConditionalIconCatalog() : this(new IconPalette()) { }

    public ConditionalIconCatalog(IconPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var p = palette;
        var gray = p.Gray;

        _families = new Dictionary<string, IReadOnlyList<IIconGlyph>>(StringComparer.Ordinal)
        {
            ["3Arrows"] = [new ArrowGlyph(180, p.Red), new ArrowGlyph(90, p.Yellow), new ArrowGlyph(0, p.Green)],
            ["3ArrowsGray"] = [new ArrowGlyph(180, gray), new ArrowGlyph(90, gray), new ArrowGlyph(0, gray)],
            ["3Flags"] = [new FlagGlyph(p.Red, p.Ink), new FlagGlyph(p.Yellow, p.Ink), new FlagGlyph(p.Green, p.Ink)],
            ["3TrafficLights1"] = [new CircleGlyph(p.StrongRed), new CircleGlyph(p.Yellow), new CircleGlyph(p.Green)],
            ["3TrafficLights2"] = [new TrafficLightGlyph(p.LampRed, p.Ink), new TrafficLightGlyph(p.LampYellow, p.Ink), new TrafficLightGlyph(p.LampGreen, p.Ink)],
            ["3Signs"] = [new DiamondGlyph(p.Red), new WarningTriangleGlyph(p.Yellow), new CircleGlyph(p.Green)],
            ["3Symbols"] = [new CrossGlyph(p.Red, p.Paper, true), new ExclamationGlyph(p.Yellow, p.Paper, true), new CheckGlyph(p.Green, p.Paper, true)],
            ["3Symbols2"] = [new CrossGlyph(p.Red, p.Paper, false), new ExclamationGlyph(p.Yellow, p.Paper, false), new CheckGlyph(p.Green, p.Paper, false)],
            ["3Smilies"] = [new SmileyGlyph(-1, p.Yellow), new SmileyGlyph(0, p.Yellow), new SmileyGlyph(1, p.Yellow)],
            ["3ColorSmilies"] = [new SmileyGlyph(-1, p.Red), new SmileyGlyph(0, p.Yellow), new SmileyGlyph(1, p.Green)],
            ["3Stars"] = [new StarGlyph(0, p.Yellow, p.LightGray), new StarGlyph(0.5, p.Yellow, p.LightGray), new StarGlyph(1, p.Yellow, p.LightGray)],
            ["3Triangles"] = [new TriangleGlyph(-1, p.Red), new TriangleGlyph(0, p.Yellow), new TriangleGlyph(1, p.Green)],
            ["4Arrows"] = [new ArrowGlyph(180, p.Red), new ArrowGlyph(135, p.Yellow), new ArrowGlyph(45, p.Yellow), new ArrowGlyph(0, p.Green)],
            ["4ArrowsGray"] = [new ArrowGlyph(180, gray), new ArrowGlyph(135, gray), new ArrowGlyph(45, gray), new ArrowGlyph(0, gray)],
            ["4RedToBlack"] = [new CircleGlyph(p.DarkGray), new CircleGlyph(p.LightGray), new CircleGlyph(new IconTone("#FF9198", "#ED3D3B")), new CircleGlyph(p.DarkRed)],
            ["4Rating"] = [new BarsGlyph(1, p.Blue, p.Ink), new BarsGlyph(2, p.Blue, p.Ink), new BarsGlyph(3, p.Blue, p.Ink), new BarsGlyph(4, p.Blue, p.Ink)],
            ["4TrafficLights"] = [new CircleGlyph(gray), new CircleGlyph(p.StrongRed), new CircleGlyph(p.Yellow), new CircleGlyph(p.Green)],
            ["5Arrows"] = [new ArrowGlyph(180, p.Red), new ArrowGlyph(135, p.Yellow), new ArrowGlyph(90, p.Yellow), new ArrowGlyph(45, p.Yellow), new ArrowGlyph(0, p.Green)],
            ["5ArrowsGray"] = [new ArrowGlyph(180, gray), new ArrowGlyph(135, gray), new ArrowGlyph(90, gray), new ArrowGlyph(45, gray), new ArrowGlyph(0, gray)],
            ["5Rating"] = [.. Enumerable.Range(0, 5).Select(level => (IIconGlyph)new BarsGlyph(level, p.Blue, p.Ink))],
            ["5Quarters"] = [.. Enumerable.Range(0, 5).Select(level => (IIconGlyph)new PieGlyph(level / 4.0, gray, p.Paper))],
            ["5Boxes"] = [.. Enumerable.Range(0, 5).Select(level => (IIconGlyph)new SquaresGlyph(level, p.Blue, p.Ink))],
        };
    }

    public IReadOnlyList<string> Names => [.. _families.Keys];

    public int Count(string set) => Family(set).Count;

    public ConditionalIcon Icon(string set, int index)
    {
        var family = Family(set);
        var key = (_families.ContainsKey(set ?? string.Empty) ? set! : Fallback, Math.Clamp(index, 0, family.Count - 1));
        return _cache.GetOrAdd(key, entry => family[entry.Item2].Draw());
    }

    private IReadOnlyList<IIconGlyph> Family(string? set) =>
        set is not null && _families.TryGetValue(set, out var family) ? family : _families[Fallback];
}
