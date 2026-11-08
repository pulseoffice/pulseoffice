using System.Globalization;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.UI.Controls.Shapes.Fontwork;

public sealed class FontworkDesigner
{
    private const int ThumbnailLength = 12;

    private readonly string _sample;
    private string _text;
    private string _fontFamily;
    private FontworkWarp _warp = FontworkWarp.ArchUp;
    private string _fill;
    private string _line;
    private bool _shadow = true;
    private bool _bold = true;
    private bool _italic;

    public FontworkDesigner(IReadOnlyList<FontworkLook> looks, string sample, string fontFamily)
    {
        ArgumentNullException.ThrowIfNull(looks);
        if (looks.Count == 0) throw new ArgumentException("At least one fontwork look is required.", nameof(looks));
        Looks = looks;
        _sample = sample ?? string.Empty;
        _text = _sample;
        _fontFamily = fontFamily ?? string.Empty;
        _fill = looks[0].Fill;
        _line = looks[0].Line;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<FontworkLook> Looks { get; }

    public IReadOnlyList<FontworkWarp> Warps { get; } = Enum.GetValues<FontworkWarp>();

    public string Text { get => _text; set => Set(ref _text, value ?? string.Empty); }

    public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value ?? string.Empty); }

    public FontworkWarp Warp { get => _warp; set => Set(ref _warp, value); }

    public string Fill { get => _fill; set => Set(ref _fill, value ?? string.Empty); }

    public string Line { get => _line; set => Set(ref _line, value ?? string.Empty); }

    public bool Shadow { get => _shadow; set => Set(ref _shadow, value); }

    public bool Bold { get => _bold; set => Set(ref _bold, value); }

    public bool Italic { get => _italic; set => Set(ref _italic, value); }

    public int Look => Looks.ToList().FindIndex(look =>
        string.Equals(look.Fill, _fill, StringComparison.OrdinalIgnoreCase) && string.Equals(look.Line, _line, StringComparison.OrdinalIgnoreCase)) is var found and >= 0 ? found : -1;

    public bool CanConfirm => _text.Trim().Length > 0;

    public void ChooseLook(int index)
    {
        if (index < 0 || index >= Looks.Count) return;
        var changed = !string.Equals(_fill, Looks[index].Fill, StringComparison.Ordinal) || !string.Equals(_line, Looks[index].Line, StringComparison.Ordinal);
        _fill = Looks[index].Fill;
        _line = Looks[index].Line;
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MoveWarp(int delta)
    {
        var position = Math.Max(0, IndexOf(Warps, _warp));
        Warp = Warps[Math.Clamp(position + delta, 0, Warps.Count - 1)];
    }

    public string PreviewText => _text.Trim().Length > 0 ? _text.Trim() : _sample;

    public string ThumbnailText
    {
        get
        {
            var elements = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(PreviewText);
            while (enumerator.MoveNext() && elements.Count < ThumbnailLength) elements.Add(enumerator.GetTextElement());
            return string.Concat(elements);
        }
    }

    public FontworkObject Build(string id) => Compose(id, _text.Trim(), _warp, _fill, _line);

    public FontworkObject Preview() => Compose("preview", PreviewText, _warp, _fill, _line);

    public FontworkObject Thumbnail(FontworkWarp warp) => Compose("thumbnail", ThumbnailText, warp, _fill, _line);

    public FontworkObject Swatch(int look) => Compose("swatch", "Aa", FontworkWarp.Plain, Looks[look].Fill, Looks[look].Line);

    private FontworkObject Compose(string id, string text, FontworkWarp warp, string fill, string line) => new()
    {
        Id = id,
        Text = text,
        Warp = warp,
        FontFamily = _fontFamily.Trim() is { Length: > 0 } font ? font : "Inter",
        Bold = _bold,
        Italic = _italic,
        Style = ShapeStyle.Default with
        {
            Fill = ShapeFill.Default with { ColorHex = fill.Length > 0 ? fill : Looks[0].Fill },
            Line = line.Length > 0
                ? ShapeLine.Default with { ColorHex = line, Width = Twip.FromPoints(0.75) }
                : ShapeLine.None,
            Shadow = ShapeShadow.Default with { Enabled = _shadow },
        },
    };

    private int IndexOf(IReadOnlyList<FontworkWarp> warps, FontworkWarp warp)
    {
        for (var i = 0; i < warps.Count; i++)
            if (warps[i] == warp) return i;
        return -1;
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
