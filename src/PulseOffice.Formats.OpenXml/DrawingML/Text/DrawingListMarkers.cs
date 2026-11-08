using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Colors;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingListMarkers(IDrawingBulletPictures pictures, IDrawingNumberSchemes numbers)
{
    public DrawingListMarkers() : this(new DrawingBulletPictures())
    {
    }

    public DrawingListMarkers(IDrawingBulletPictures pictures) : this(pictures, new DrawingNumberSchemes())
    {
    }

    public IEnumerable<OpenXmlElement> Write(bool numbered, bool bullet, BulletSymbol? symbol, ListNumberFormat? format = null)
    {
        if (numbered)
        {
            yield return numbers.Write(format);
            yield break;
        }
        if (!bullet)
        {
            yield return new D.NoBullet();
            yield break;
        }
        if (symbol?.Picture is { Data.Length: > 0 } picture)
        {
            foreach (var element in pictures.Write(symbol, picture)) yield return element;
            yield break;
        }
        if (symbol?.Color is { } colour) yield return new D.BulletColor(new D.RgbColorModelHex { Val = new ArgbColor(colour).ToRgbDigits() });
        if (symbol?.RelativeSize is { } size) yield return new D.BulletSizePercentage { Val = (int)Math.Round(Math.Clamp(size, 0.25, 4) * 100000) };
        if (symbol?.Font is { } font) yield return new D.BulletFont { Typeface = font, CharacterSet = symbol.SymbolCharset ? 2 : null };
        yield return new D.CharacterBullet { Char = symbol?.Character ?? "•" };
    }
}

internal sealed class DrawingShapeLists(LevelStyleReader styles, DrawingListMarkers markers, DrawingParagraphLevels levels)
{
    private const int LevelIndent = 342900;

    private const int Hanging = 228600;


    public DrawingShapeLists() : this(new LevelStyleReader(new OoxmlValues(), new SchemeRoles()), new DrawingListMarkers(), new DrawingParagraphLevels())
    {
    }

    public ShapeListItem? Read(D.ParagraphProperties? properties)
    {
        if (properties is null) return null;
        var style = styles.Paragraph(properties);
        var level = levels.Read(properties);
        var margin = properties.LeftMargin?.HasValue == true ? properties.LeftMargin.Value / (double)PulseOffice.Foundation.Units.Emu.PerPoint : (double?)null;
        var indent = properties.Indent?.HasValue == true ? properties.Indent.Value / (double)PulseOffice.Foundation.Units.Emu.PerPoint : (double?)null;
        if (style.Numbered == true) return new ShapeListItem { Level = level, Numbered = true, NumberFormat = style.NumberFormat, Margin = margin, Indent = indent };
        return style.Bullet == true ? new ShapeListItem { Level = level, Symbol = style.Symbol, Margin = margin, Indent = indent } : null;
    }

    public D.ParagraphProperties Write(D.ParagraphProperties? template, ShapeListItem? item)
    {
        var properties = (D.ParagraphProperties?)template?.CloneNode(true) ?? new D.ParagraphProperties();
        foreach (var old in properties.ChildElements.Where(Marker).ToList()) old.Remove();
        if (item is null)
        {
            properties.Level = null;
            properties.LeftMargin = null;
            properties.Indent = null;
            return properties;
        }

        properties.Level = item.Level > 0 ? item.Level : null;
        properties.LeftMargin = item.Margin is { } margin ? Emu(margin) : Hanging + item.Level * LevelIndent;
        properties.Indent = item.Indent is { } indent ? Emu(indent) : item.Margin is null ? -Hanging : null;
        var anchor = properties.ChildElements.FirstOrDefault(child => child is D.TabStopList or D.DefaultRunProperties or D.ExtensionList);
        foreach (var element in markers.Write(item.Numbered, !item.Numbered, item.Symbol, item.NumberFormat))
        {
            if (anchor is null) properties.Append(element);
            else anchor.InsertBeforeSelf(element);
        }
        return properties;
    }

    private int Emu(double points) => (int)PulseOffice.Foundation.Units.Emu.FromPoints(points).Value;

    private bool Marker(OpenXmlElement element) => element is D.BulletColorText or D.BulletColor or D.BulletSizeText or D.BulletSizePercentage
        or D.BulletSizePoints or D.BulletFontText or D.BulletFont or D.NoBullet or D.AutoNumberedBullet or D.CharacterBullet or D.PictureBullet;
}

internal sealed class DrawingParagraphLevels
{
    public int Read(D.ParagraphProperties? properties)
    {
        var raw = properties?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "lvl" && string.IsNullOrEmpty(attribute.NamespaceUri)).Value;
        return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var level)
            ? Math.Clamp(level, 0, ShapeTextLists.LevelCount - 1)
            : 0;
    }
}
