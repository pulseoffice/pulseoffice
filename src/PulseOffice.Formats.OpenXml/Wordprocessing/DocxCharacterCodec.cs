using DocumentFormat.OpenXml;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxCharacterCodec(DocxValues values)
{
    private const int LargestHalfPoints = 3276;
    private const int LargestSpacing = 31680;
    private const int LargestScale = 600;

    private readonly IReadOnlyDictionary<W.HighlightColorValues, uint> _highlights = new Dictionary<W.HighlightColorValues, uint>
    {
        [W.HighlightColorValues.Yellow] = 0xFFFFFF00,
        [W.HighlightColorValues.Green] = 0xFF00FF00,
        [W.HighlightColorValues.Cyan] = 0xFF00FFFF,
        [W.HighlightColorValues.Magenta] = 0xFFFF00FF,
        [W.HighlightColorValues.Blue] = 0xFF0000FF,
        [W.HighlightColorValues.Red] = 0xFFFF0000,
        [W.HighlightColorValues.DarkBlue] = 0xFF000080,
        [W.HighlightColorValues.DarkCyan] = 0xFF008080,
        [W.HighlightColorValues.DarkGreen] = 0xFF008000,
        [W.HighlightColorValues.DarkMagenta] = 0xFF800080,
        [W.HighlightColorValues.DarkRed] = 0xFF800000,
        [W.HighlightColorValues.DarkYellow] = 0xFF808000,
        [W.HighlightColorValues.DarkGray] = 0xFF808080,
        [W.HighlightColorValues.LightGray] = 0xFFC0C0C0,
        [W.HighlightColorValues.Black] = 0xFF000000,
        [W.HighlightColorValues.White] = 0xFFFFFFFF,
    };

    public IEnumerable<OpenXmlElement> Elements(CharacterProperties properties, string? styleId = null)
    {
        if (styleId is not null) yield return new W.RunStyle { Val = styleId };
        if (properties.Font is { } font) yield return new W.RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font, EastAsia = font };
        if (properties.Bold is { } bold) yield return new W.Bold { Val = bold };
        if (properties.Italic is { } italic) yield return new W.Italic { Val = italic };
        if (properties.Case is { } textCase && textCase is TextCase.None or TextCase.Upper) yield return new W.Caps { Val = textCase == TextCase.Upper };
        if (properties.SmallCaps is { } smallCaps) yield return new W.SmallCaps { Val = smallCaps };
        if (properties.Strikethrough is { } strike) yield return new W.Strike { Val = strike };
        if (properties.Outline is { } outline) yield return new W.Outline { Val = outline };
        if (properties.Shadow is { } shadow) yield return new W.Shadow { Val = shadow };
        if (properties.Relief is { } relief)
        {
            if (relief != FontRelief.Engraved) yield return new W.Emboss { Val = relief == FontRelief.Embossed };
            if (relief != FontRelief.Embossed) yield return new W.Imprint { Val = relief == FontRelief.Engraved };
        }
        if (properties.Hidden is { } hidden) yield return new W.Vanish { Val = hidden };
        if (properties.Color is { } color) yield return new W.Color { Val = values.Color(color) };
        if (properties.LetterSpacing is { } spacing) yield return new W.Spacing { Val = (int)Math.Round(spacing * 20) };
        if (properties.ScaleWidth is { } scale) yield return new W.CharacterScale { Val = (int)Math.Round(scale) };
        if (properties.Kerning is { } kerning) yield return new W.Kern { Val = kerning ? 2u : 0u };
        if (properties.FontSize is { } size) yield return new W.FontSize { Val = values.Text((int)Math.Round(size * 2)) };
        if (Underline(properties) is { } underline) yield return new W.Underline { Val = underline };
        if (properties.Highlight is { } highlight)
            yield return new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = values.Color(highlight) };
        if (properties.Baseline is { } baseline)
            yield return new W.VerticalTextAlignment
            {
                Val = baseline switch
                {
                    TextBaseline.Superscript => W.VerticalPositionValues.Superscript,
                    TextBaseline.Subscript => W.VerticalPositionValues.Subscript,
                    _ => W.VerticalPositionValues.Baseline,
                },
            };
        if (properties.RightToLeft is { } rightToLeft) yield return new W.RightToLeftText { Val = rightToLeft };
        if (properties.Language is { } language) yield return new W.Languages { Val = language };
    }

    public CharacterProperties Read(OpenXmlElement? container)
    {
        if (container is null) return CharacterProperties.None;
        var fonts = container.GetFirstChild<W.RunFonts>();
        var underline = container.GetFirstChild<W.Underline>()?.Val?.Value;
        var vertical = container.GetFirstChild<W.VerticalTextAlignment>()?.Val?.Value;
        var size = values.Integer(container.GetFirstChild<W.FontSize>()?.Val?.Value);
        return new CharacterProperties
        {
            Font = fonts?.Ascii?.Value ?? fonts?.HighAnsi?.Value,
            FontSize = size is { } halfPoints ? values.Bounded(halfPoints, 2, LargestHalfPoints) / 2.0 : null,
            Bold = values.Flag(container.GetFirstChild<W.Bold>()),
            Italic = values.Flag(container.GetFirstChild<W.Italic>()),
            SmallCaps = values.Flag(container.GetFirstChild<W.SmallCaps>()),
            Strikethrough = values.Flag(container.GetFirstChild<W.Strike>()) ?? (values.Flag(container.GetFirstChild<W.DoubleStrike>()) == true ? true : null),
            Outline = values.Flag(container.GetFirstChild<W.Outline>()),
            Shadow = values.Flag(container.GetFirstChild<W.Shadow>()),
            Color = values.Color(container.GetFirstChild<W.Color>()?.Val?.Value),
            Underline = underline is null || underline == W.UnderlineValues.Double ? null : underline != W.UnderlineValues.None,
            DoubleUnderline = underline == W.UnderlineValues.Double ? true : null,
            Highlight = Highlight(container),
            Baseline = vertical is null ? null
                : vertical == W.VerticalPositionValues.Superscript ? TextBaseline.Superscript
                : vertical == W.VerticalPositionValues.Subscript ? TextBaseline.Subscript
                : TextBaseline.Normal,
            Language = container.GetFirstChild<W.Languages>()?.Val?.Value,
            LetterSpacing = container.GetFirstChild<W.Spacing>()?.Val?.Value is { } spacing ? values.Bounded(spacing, -LargestSpacing, LargestSpacing) / 20.0 : null,
            ScaleWidth = container.GetFirstChild<W.CharacterScale>()?.Val?.Value is { } scale ? values.Bounded(scale, 1, LargestScale) : null,
            Kerning = container.GetFirstChild<W.Kern>()?.Val?.Value is { } kern ? kern > 0 : null,
            Case = values.Flag(container.GetFirstChild<W.Caps>()) is { } caps ? caps ? TextCase.Upper : TextCase.None : null,
            Relief = Relief(container),
            Hidden = values.Flag(container.GetFirstChild<W.Vanish>()),
            RightToLeft = values.Flag(container.GetFirstChild<W.RightToLeftText>()),
        };
    }

    private FontRelief? Relief(OpenXmlElement container)
    {
        var embossed = values.Flag(container.GetFirstChild<W.Emboss>());
        var engraved = values.Flag(container.GetFirstChild<W.Imprint>());
        if (embossed is null && engraved is null) return null;
        return embossed == true ? FontRelief.Embossed : engraved == true ? FontRelief.Engraved : FontRelief.None;
    }

    public string? StyleId(OpenXmlElement? container) => container?.GetFirstChild<W.RunStyle>()?.Val?.Value;

    private uint? Highlight(OpenXmlElement container)
    {
        if (container.GetFirstChild<W.Highlight>()?.Val?.Value is { } named && named != W.HighlightColorValues.None)
            return _highlights.TryGetValue(named, out var colour) ? colour : null;
        return values.Color(container.GetFirstChild<W.Shading>()?.Fill?.Value);
    }

    private W.UnderlineValues? Underline(CharacterProperties properties) =>
        properties.DoubleUnderline == true ? W.UnderlineValues.Double
        : properties.Underline == true ? W.UnderlineValues.Single
        : properties.Underline == false || properties.DoubleUnderline == false ? W.UnderlineValues.None
        : null;
}
