using PulseOffice.Drawing.Fontwork;

namespace PulseOffice.Formats;

public sealed class FontworkWarps
{
    private readonly TokenMap<FontworkWarp> _odf = new(FontworkWarp.Plain, "fontwork-plain-text",
        (FontworkWarp.ArchUp, "fontwork-arch-up-curve"), (FontworkWarp.ArchDown, "fontwork-arch-down-curve"),
        (FontworkWarp.Circle, "fontwork-circle-curve"), (FontworkWarp.Wave, "fontwork-wave"),
        (FontworkWarp.SlantUp, "fontwork-slant-up"), (FontworkWarp.SlantDown, "fontwork-slant-down"),
        (FontworkWarp.Triangle, "fontwork-triangle-up"));

    private readonly TokenMap<FontworkWarp> _ooxml = new(FontworkWarp.Plain, "textPlain",
        (FontworkWarp.ArchUp, "textArchUp"), (FontworkWarp.ArchDown, "textArchDown"),
        (FontworkWarp.Circle, "textCircle"), (FontworkWarp.Wave, "textWave1"),
        (FontworkWarp.SlantUp, "textSlantUp"), (FontworkWarp.SlantDown, "textSlantDown"),
        (FontworkWarp.Triangle, "textTriangle"));

    public string Odf(FontworkWarp warp) => _odf.Token(warp);

    public FontworkWarp FromOdf(string? token) => _odf.Value(token);

    public string Ooxml(FontworkWarp warp) => _ooxml.Token(warp);

    public FontworkWarp FromOoxml(string? token) => _ooxml.Value(token);
}
