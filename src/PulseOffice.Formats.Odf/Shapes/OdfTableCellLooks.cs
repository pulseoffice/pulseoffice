using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfTableCellLooks
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Loext = OdfPackage.Namespaces.Loext;

    private const double MinimumFontSize = 1;
    private const double MaximumFontSize = 4000;

    private readonly OdfMeasure _measure = new();

    public TableCellLook? Read(XElement? style)
    {
        if (style is null) return null;
        var graphic = style.Element(Loext + "graphic-properties") ?? style.Element(Style + "graphic-properties");
        var cell = style.Element(Style + "table-cell-properties");
        var paragraph = style.Element(Style + "paragraph-properties");
        var text = style.Element(Style + "text-properties");
        if (graphic is null && cell is null && paragraph is null && text is null) return null;

        var fill = (string?)graphic?.Attribute(Draw + "fill") switch
        {
            "solid" => (string?)graphic!.Attribute(Draw + "fill-color"),
            "none" => null,
            _ => (string?)cell?.Attribute(Fo + "background-color") is { } background && background != "transparent" ? background : null,
        };

        var look = new TableCellLook
        {
            FillHex = Hex(fill),
            Left = Border(paragraph, cell, "left"),
            Right = Border(paragraph, cell, "right"),
            Top = Border(paragraph, cell, "top"),
            Bottom = Border(paragraph, cell, "bottom"),
            TextHex = Hex((string?)text?.Attribute(Fo + "color")),
            Bold = (string?)text?.Attribute(Fo + "font-weight") is "bold" or "600" or "700" or "800" or "900",
            Italic = (string?)text?.Attribute(Fo + "font-style") is "italic" or "oblique",
            Underline = (string?)text?.Attribute(Style + "text-underline-style") is { } underline && underline != "none",
            Strikethrough = (string?)text?.Attribute(Style + "text-line-through-style") is { } strike && strike != "none",
            FontFamily = Family((string?)text?.Attribute(Fo + "font-family") ?? (string?)text?.Attribute(Style + "font-name")),
            FontSize = (string?)text?.Attribute(Fo + "font-size") is { } size && size.EndsWith("pt", StringComparison.Ordinal)
                && double.TryParse(size[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var points) && points is >= MinimumFontSize and <= MaximumFontSize ? points : null,
            Horizontal = (string?)paragraph?.Attribute(Fo + "text-align") switch
            {
                "center" => CellHorizontalAlign.Center,
                "end" or "right" => CellHorizontalAlign.Right,
                "start" or "left" => CellHorizontalAlign.Left,
                _ => null,
            },
        };
        return look == TableCellLook.Plain && graphic is null && paragraph is null && text is null ? null : look;
    }

    public XElement Write(string name, TableCellLook look, CellVerticalAlign align)
    {
        var graphic = new XElement(Loext + "graphic-properties",
            new XAttribute(Draw + "fill", look.FillHex is null ? "none" : "solid"));
        if (look.FillHex is { } fill) graphic.Add(new XAttribute(Draw + "fill-color", _measure.Colour(fill)));

        var cell = new XElement(Style + "table-cell-properties");
        if (align != CellVerticalAlign.Top) cell.Add(new XAttribute(Style + "vertical-align", align == CellVerticalAlign.Center ? "middle" : "bottom"));

        var paragraph = new XElement(Style + "paragraph-properties",
            new XAttribute(Fo + "border-left", Border(look.Left)),
            new XAttribute(Fo + "border-right", Border(look.Right)),
            new XAttribute(Fo + "border-top", Border(look.Top)),
            new XAttribute(Fo + "border-bottom", Border(look.Bottom)));
        if (look.Horizontal is { } horizontal)
            paragraph.Add(new XAttribute(Fo + "text-align", horizontal switch
            {
                CellHorizontalAlign.Center => "center",
                CellHorizontalAlign.Right => "end",
                _ => "start",
            }));

        var text = new XElement(Style + "text-properties");
        if (look.TextHex is { } ink) text.Add(new XAttribute(Fo + "color", _measure.Colour(ink)));
        if (look.Bold) text.Add(new XAttribute(Fo + "font-weight", "bold"));
        if (look.Italic) text.Add(new XAttribute(Fo + "font-style", "italic"));
        if (look.Underline)
            text.Add(new XAttribute(Style + "text-underline-style", "solid"),
                new XAttribute(Style + "text-underline-width", "auto"),
                new XAttribute(Style + "text-underline-color", "font-color"));
        if (look.Strikethrough) text.Add(new XAttribute(Style + "text-line-through-style", "solid"));
        if (Family(look.FontFamily) is { } family)
            text.Add(new XAttribute(Fo + "font-family", family.Contains(' ', StringComparison.Ordinal) ? $"'{family}'" : family));
        if (look.FontSize is { } size) text.Add(new XAttribute(Fo + "font-size", $"{_measure.Number(size)}pt"));

        return new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "table-cell"),
            graphic, cell, paragraph, text);
    }

    private string Border(TableBorderLine? line) =>
        line is null ? "none" : $"{_measure.Number(line.WidthPoints)}pt solid {_measure.Colour(line.ColorHex)}";

    private TableBorderLine? Border(XElement? paragraph, XElement? cell, string side)
    {
        var value = (string?)paragraph?.Attribute(Fo + $"border-{side}") ?? (string?)paragraph?.Attribute(Fo + "border")
            ?? (string?)cell?.Attribute(Fo + $"border-{side}") ?? (string?)cell?.Attribute(Fo + "border");
        if (value is null || value.Trim() == "none") return null;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var colour = parts.FirstOrDefault(part => part.StartsWith('#'));
        var width = parts.Select(part => _measure.ParseLength(part, Twip.Zero)).FirstOrDefault(length => length.Value > 0);
        if (colour is null) return null;
        return new TableBorderLine(colour.ToUpperInvariant(), width.Value > 0 ? width.ToPoints() : 0.75);
    }

    private const int MaximumFamilyLength = 256;

    private string? Family(string? value)
    {
        var family = value?.Split(',')[0].Trim().Trim('\'', '"').Trim();
        return family is { Length: > 0 and <= MaximumFamilyLength } ? family : null;
    }

    private string? Hex(string? colour) => colour is { Length: 7 } && colour[0] == '#' ? colour.ToUpperInvariant() : null;
}
