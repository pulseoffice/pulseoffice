using DocumentFormat.OpenXml;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed record LevelStyle
{
    public static LevelStyle Empty { get; } = new();

    public double? FontSize { get; init; }

    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    public uint? Color { get; init; }

    public ColorRole? ColorRole { get; init; }

    public string? Latin { get; init; }

    public TextAlign? Align { get; init; }

    public bool? Bullet { get; init; }

    public bool? Numbered { get; init; }

    public BulletSymbol? Symbol { get; init; }

    public ListNumberFormat? NumberFormat { get; init; }

    public double? LineSpacing { get; init; }

    public double? SpaceBefore { get; init; }

    public double? SpaceAfter { get; init; }

    public double? MarginLeft { get; init; }

    public double? Indent { get; init; }

    public double? MarginRight { get; init; }

    public IReadOnlyList<TabStop>? TabStops { get; init; }

    public PulseOffice.Text.Rich.Bidi.TextDirection? Direction { get; init; }

    public bool HasColor => Color is not null || ColorRole is not null;

    public LevelStyle Over(LevelStyle lower) => new()
    {
        FontSize = FontSize ?? lower.FontSize,
        Bold = Bold ?? lower.Bold,
        Italic = Italic ?? lower.Italic,
        Color = HasColor ? Color : lower.Color,
        ColorRole = HasColor ? ColorRole : lower.ColorRole,
        Latin = Latin ?? lower.Latin,
        Align = Align ?? lower.Align,
        Bullet = Bullet ?? lower.Bullet,
        Numbered = Numbered ?? lower.Numbered,
        Symbol = Bullet is null ? Symbol ?? lower.Symbol : Symbol,
        NumberFormat = Numbered is null ? NumberFormat ?? lower.NumberFormat : NumberFormat,
        LineSpacing = LineSpacing ?? lower.LineSpacing,
        SpaceBefore = SpaceBefore ?? lower.SpaceBefore,
        SpaceAfter = SpaceAfter ?? lower.SpaceAfter,
        MarginLeft = MarginLeft ?? lower.MarginLeft,
        Indent = Indent ?? lower.Indent,
        MarginRight = MarginRight ?? lower.MarginRight,
        TabStops = TabStops ?? lower.TabStops,
        Direction = Direction ?? lower.Direction,
    };

    public bool IsMajorFont(bool fallback) => Latin switch
    {
        "+mj-lt" => true,
        "+mn-lt" => false,
        _ => fallback,
    };

    public string? ExplicitFont => Latin is { } latin && !latin.StartsWith('+') ? latin : null;
}

internal sealed class LevelStyleReader(OoxmlValues values, SchemeRoles schemes, ISymbolFonts symbols, IDrawingBulletPictures pictures, IDrawingNumberSchemes numbers)
{
    public LevelStyleReader(OoxmlValues values, SchemeRoles schemes) : this(values, schemes, new SymbolFonts(), new DrawingBulletPictures(), new DrawingNumberSchemes())
    {
    }

    private const int SmallestBulletPercent = 25000;
    private const int LargestBulletPercent = 400000;

    private readonly PulseOffice.Formats.ParagraphMargins _margins = new();
    private readonly DrawingTabStops _tabs = new();

    public LevelStyle Paragraph(OpenXmlElement? properties)
    {
        if (properties is null) return LevelStyle.Empty;

        var bullet = properties.GetFirstChild<D.AutoNumberedBullet>() is not null ? (bool?)false
            : properties.GetFirstChild<D.CharacterBullet>() is not null || properties.GetFirstChild<D.PictureBullet>() is not null ? true
            : properties.GetFirstChild<D.NoBullet>() is not null ? false
            : null;
        var numbered = properties.GetFirstChild<D.AutoNumberedBullet>() is not null ? true
            : bullet is not null ? false
            : (bool?)null;

        var own = new LevelStyle
        {
            Align = properties.GetAttributes().FirstOrDefault(a => a.LocalName == "algn").Value is { } algn ? Align(algn) : null,
            Bullet = bullet,
            Numbered = numbered,
            Symbol = Symbol(properties),
            NumberFormat = numbers.Read(properties.GetFirstChild<D.AutoNumberedBullet>()),
            LineSpacing = properties.GetFirstChild<D.LineSpacing>()?.GetFirstChild<D.SpacingPercent>()?.Val?.Value is { } percent
                ? percent / 100000.0 : null,
            SpaceBefore = Points(properties.GetFirstChild<D.SpaceBefore>()),
            SpaceAfter = Points(properties.GetFirstChild<D.SpaceAfter>()),
            MarginLeft = _margins.Read(Emu(Attribute(properties, "marL"))),
            Indent = Emu(Attribute(properties, "indent")),
            MarginRight = Emu(Attribute(properties, "marR")),
            TabStops = _tabs.Read(properties),
            Direction = Attribute(properties, "rtl") switch
            {
                "1" or "true" => PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft,
                "0" or "false" => PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight,
                _ => null,
            },
        };
        return own.Over(Character(properties.GetFirstChild<D.DefaultRunProperties>()));
    }

    private BulletSymbol? Symbol(OpenXmlElement properties)
    {
        if (pictures.Read(properties) is { } picture)
            return new BulletSymbol("•")
            {
                Picture = picture,
                RelativeSize = RelativeSize(properties),
            };
        if (properties.GetFirstChild<D.CharacterBullet>()?.Char?.Value is not { Length: > 0 } character) return null;
        var bulletFont = properties.GetFirstChild<D.BulletFont>();
        var font = bulletFont?.Typeface?.Value;
        var colour = properties.GetFirstChild<D.BulletColor>()?.GetFirstChild<D.RgbColorModelHex>()?.Val?.Value;
        var symbol = new BulletSymbol(character)
        {
            Font = font is { Length: > 0 } && !font.StartsWith('+') ? font : null,
            Color = colour is { Length: 6 } && uint.TryParse(colour, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rgb) ? 0xFF000000 | rgb : null,
            RelativeSize = RelativeSize(properties),
            SymbolCharset = bulletFont?.CharacterSet?.Value == 2 || symbols.IsSymbolic(font),
        };
        return symbol is { Character: "•", Color: null, RelativeSize: null } ? null : symbol;
    }

    private double? RelativeSize(OpenXmlElement properties) =>
        properties.GetFirstChild<D.BulletSizePercentage>()?.Val?.Value is { } percent && percent > 0 && percent != 100000
            ? Math.Clamp(percent, SmallestBulletPercent, LargestBulletPercent) / 100000.0
            : null;

    public LevelStyle FontReference(D.FontReference? reference)
    {
        if (reference is null) return LevelStyle.Empty;
        return new LevelStyle
        {
            Color = reference.RgbColorModelHex?.Val?.Value is { Length: 6 } hex ? values.Color(hex)
                : reference.SystemColor?.LastColor?.Value is { Length: 6 } last ? values.Color(last)
                : null,
            ColorRole = reference.SchemeColor?.Val?.Value is { } scheme ? schemes.Of(scheme) : null,
        };
    }

    public LevelStyle Character(OpenXmlCompositeElement? properties)
    {
        if (properties is null) return LevelStyle.Empty;

        var fill = properties.GetFirstChild<D.SolidFill>();
        return new LevelStyle
        {
            FontSize = Attribute(properties, "sz") is { } size && int.TryParse(size, out var hundredths) ? hundredths / 100.0 : null,
            Bold = Flag(Attribute(properties, "b")),
            Italic = Flag(Attribute(properties, "i")),
            Color = fill?.RgbColorModelHex?.Val?.Value is { Length: 6 } hex ? values.Color(hex)
                : fill?.SystemColor?.LastColor?.Value is { Length: 6 } last ? values.Color(last)
                : null,
            ColorRole = fill?.SchemeColor?.Val?.Value is { } scheme ? schemes.Of(scheme) : null,
            Latin = properties.GetFirstChild<D.LatinFont>()?.Typeface?.Value,
        };
    }

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(a => a.LocalName == name && string.IsNullOrEmpty(a.NamespaceUri)).Value;

    private bool? Flag(string? value) => value switch
    {
        "1" or "true" => true,
        "0" or "false" => false,
        _ => null,
    };

    private double? Emu(string? value) =>
        long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var emu)
            ? emu / 12700.0 : null;

    private double? Points(OpenXmlElement? spacing) =>
        spacing?.GetFirstChild<D.SpacingPoints>()?.Val?.Value is { } points ? points / 100.0 : null;

    private TextAlign? Align(string algn) => algn switch
    {
        "ctr" => TextAlign.Center,
        "r" => TextAlign.Right,
        "just" or "dist" => TextAlign.Justify,
        "l" => TextAlign.Left,
        _ => null,
    };
}

internal interface ITextStyleLayer
{
    LevelStyle At(int level);
}

internal sealed class ListStyleLayer(OpenXmlElement? list, LevelStyleReader reader) : ITextStyleLayer
{
    public LevelStyle At(int level) =>
        list?.ChildElements.FirstOrDefault(child => child.LocalName == $"lvl{Math.Clamp(level, 0, 8) + 1}pPr") is { } properties
            ? reader.Paragraph(properties)
            : LevelStyle.Empty;
}

internal sealed class UniformStyleLayer(LevelStyle style) : ITextStyleLayer
{
    public LevelStyle At(int level) => style;
}

internal sealed class CompositeTextStyle(IReadOnlyList<ITextStyleLayer> layers) : ITextStyleLayer
{
    public LevelStyle At(int level) =>
        layers.Aggregate(LevelStyle.Empty, (style, layer) => style.Over(layer.At(level)));
}
