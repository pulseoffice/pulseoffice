using System.Xml.Linq;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtTableTemplates(OdtMeasure measure, OdtBorders borders, OdtCharacterCodec characters) : OdtXml
{
    public IEnumerable<XElement> Write(TableProperties table, string encodedName)
    {
        var cells = new List<XElement>();
        var template = new XElement(Table + "table-template", new XAttribute(Table + "name", encodedName));
        void Add(string part, TableRegionFormat? format, bool always = false)
        {
            if (format is null || format.IsEmpty)
            {
                if (!always) return;
                format = new TableRegionFormat();
            }
            var cellName = encodedName + "." + part;
            cells.Add(CellStyle(cellName, format));
            var paragraph = ParagraphStyle(cellName + ".paragraph", format);
            if (paragraph is not null) cells.Add(paragraph);
            template.Add(new XElement(Table + part,
                new XAttribute(Table + "style-name", cellName),
                paragraph is null ? null : new XAttribute(Table + "paragraph-style-name", cellName + ".paragraph"),
                paragraph is null ? null : new XAttribute(Pt + "paragraph-style", "true")));
        }
        var firstAxes = table.FirstBandAxes ?? TableBandAxes.Both;
        var secondAxes = table.BandAxes ?? TableBandAxes.Both;
        var firstBand = TableRegionFormat.Merge(new TableRegionFormat { Background = table.FirstBandBackground }, table.FirstBand);
        var secondBand = TableRegionFormat.Merge(new TableRegionFormat { Background = table.BandBackground }, table.SecondBand);
        Add("first-row", TableRegionFormat.Merge(new TableRegionFormat { Background = table.HeaderBackground, Ink = table.HeaderColor, Bold = table.HeaderBold }, table.HeaderRow));
        Add("last-row", table.TotalRow);
        Add("first-column", table.FirstColumn);
        Add("last-column", table.LastColumn);
        Add("body", new TableRegionFormat { Background = table.Background, Borders = Body(table) }, always: true);
        Add("even-rows", secondAxes.HasFlag(TableBandAxes.Rows) ? secondBand : null);
        Add("odd-rows", firstAxes.HasFlag(TableBandAxes.Rows) ? firstBand : null);
        Add("even-columns", secondAxes.HasFlag(TableBandAxes.Columns) ? secondBand : null);
        Add("odd-columns", firstAxes.HasFlag(TableBandAxes.Columns) ? firstBand : null);
        return [.. cells, template];
    }

    public IEnumerable<string> OwnParagraphStyles(XElement template) =>
        template.Elements()
            .Where(part => (string?)part.Attribute(Pt + "paragraph-style") == "true")
            .Select(part => (string?)part.Attribute(Table + "paragraph-style-name"))
            .OfType<string>();

    public TableProperties Read(XElement template, Func<string, string, XElement?> styles, IReadOnlyDictionary<string, string> fontFaces)
    {
        TableRegionFormat? Part(string part)
        {
            var element = template.Element(Table + part);
            var cell = (string?)element?.Attribute(Table + "style-name") is { } name ? styles(name, "table-cell") : null;
            var paragraph = (string?)element?.Attribute(Table + "paragraph-style-name") is { } named ? styles(named, "paragraph") : null;
            return cell is null && paragraph is null ? null : Format(cell, paragraph, fontFaces);
        }
        var body = Part("body");
        var header = Part("first-row");
        var oddRows = Part("odd-rows");
        var oddColumns = Part("odd-columns");
        var evenRows = Part("even-rows");
        var evenColumns = Part("even-columns");
        var line = new[] { body?.Borders?.Top, body?.Borders?.Bottom, body?.Borders?.Left, body?.Borders?.Right }.FirstOrDefault(found => found is { Width: > 0 });
        return new TableProperties
        {
            Background = body?.Background,
            BorderWidth = line?.Width,
            BorderColor = line?.Color,
            HeaderBackground = header?.Background,
            HeaderColor = header?.Ink,
            HeaderBold = header?.Bold,
            HeaderRow = Extras(header),
            TotalRow = Kept(Part("last-row")),
            FirstColumn = Kept(Part("first-column")),
            LastColumn = Kept(Part("last-column")),
            FirstBandBackground = oddRows?.Background ?? oddColumns?.Background,
            FirstBandAxes = Axes(oddRows, oddColumns),
            FirstBand = Extras(oddRows ?? oddColumns),
            BandBackground = evenRows?.Background ?? evenColumns?.Background,
            BandAxes = Axes(evenRows, evenColumns),
            SecondBand = Extras(evenRows ?? evenColumns),
        };
    }

    private TableBandAxes? Axes(TableRegionFormat? rows, TableRegionFormat? columns) =>
        ((rows is not null ? TableBandAxes.Rows : TableBandAxes.None) | (columns is not null ? TableBandAxes.Columns : TableBandAxes.None)) switch
        {
            TableBandAxes.None => null,
            var axes => axes,
        };

    private TableRegionFormat? Kept(TableRegionFormat? format) => format is { IsEmpty: false } ? format : null;

    private TableRegionFormat? Extras(TableRegionFormat? format) =>
        format is null ? null : Kept(format with { Background = null, Ink = null, Bold = null });

    private BoxBorders? Body(TableProperties table)
    {
        if (table.BorderWidth is not { } width || width <= 0) return null;
        var line = new BorderLine(width, table.BorderColor ?? 0xFF000000);
        return new BoxBorders { Top = line, Bottom = line, Left = line, Right = line };
    }

    private XElement? ParagraphStyle(string name, TableRegionFormat format)
    {
        var text = TextProperties(format);
        return text.HasAttributes
            ? new XElement(Style + "style", new XAttribute(Style + "name", name), new XAttribute(Style + "family", "paragraph"), text)
            : null;
    }

    private XElement CellStyle(string name, TableRegionFormat format)
    {
        var cell = new XElement(Style + "table-cell-properties");
        if (format.Background is { } fill) cell.Add(new XAttribute(Fo + "background-color", measure.Fill(fill)));
        if (format.Borders is { } lines) Sides(cell, lines);
        var text = TextProperties(format);
        return new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "table-cell"),
            cell.HasAttributes ? cell : null,
            text.HasAttributes ? text : null);
    }

    private XElement TextProperties(TableRegionFormat format)
    {
        var text = new XElement(Style + "text-properties");
        if (format.Font is { Length: > 0 } font) text.Add(new XAttribute(Fo + "font-family", font));
        if (format.FontSize is { } size && size > 0) text.Add(new XAttribute(Fo + "font-size", measure.Points(size)));
        if (format.Bold is { } bold) text.Add(new XAttribute(Fo + "font-weight", bold ? "bold" : "normal"));
        if (format.Italic is { } italic) text.Add(new XAttribute(Fo + "font-style", italic ? "italic" : "normal"));
        if (format.Ink is { } ink && ink >> 24 != 0) text.Add(new XAttribute(Fo + "color", measure.Hex(ink)));
        return text;
    }

    private void Sides(XElement cell, BoxBorders lines) => borders.WriteSides(cell, lines);

    private TableRegionFormat Format(XElement? style, XElement? paragraph, IReadOnlyDictionary<string, string> fontFaces)
    {
        var cell = style?.Element(Style + "table-cell-properties");
        var own = characters.Read(style?.Element(Style + "text-properties"), fontFaces).Properties;
        var text = own.Over(characters.Read(paragraph?.Element(Style + "text-properties"), fontFaces).Properties);
        var lines = cell is null ? null : borders.ReadSides(cell);
        return new TableRegionFormat
        {
            Background = measure.Fill((string?)cell?.Attribute(Fo + "background-color")),
            Ink = text.Color,
            Bold = text.Bold,
            Italic = text.Italic,
            Font = text.Font,
            FontSize = text.FontSize,
            Borders = lines,
        };
    }
}
