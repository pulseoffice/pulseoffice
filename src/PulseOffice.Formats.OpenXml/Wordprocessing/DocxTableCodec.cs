using DocumentFormat.OpenXml;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxTableStyleCodec(DocxValues values, DocxBorders borders)
{
    private static readonly (TableRegion Region, W.TableStyleOverrideValues Kind)[] _regions =
    [
        (TableRegion.TotalRow, W.TableStyleOverrideValues.LastRow),
        (TableRegion.FirstColumn, W.TableStyleOverrideValues.FirstColumn),
        (TableRegion.LastColumn, W.TableStyleOverrideValues.LastColumn),
        (TableRegion.NorthWestCell, W.TableStyleOverrideValues.NorthWestCell),
        (TableRegion.NorthEastCell, W.TableStyleOverrideValues.NorthEastCell),
        (TableRegion.SouthWestCell, W.TableStyleOverrideValues.SouthWestCell),
        (TableRegion.SouthEastCell, W.TableStyleOverrideValues.SouthEastCell),
    ];

    public void Write(W.Style element, TableProperties table)
    {
        var properties = new W.StyleTableProperties();
        if (table.BorderWidth is { } width && width > 0)
        {
            var color = values.Color(table.BorderColor ?? 0xFF000000);
            var size = (uint)Math.Clamp(Math.Round(width * 8), 2, 96);
            W.BorderType Line(W.BorderType border)
            {
                border.Val = W.BorderValues.Single;
                border.Size = size;
                border.Space = 0;
                border.Color = color;
                return border;
            }
            var outline = new W.TableBorders(Line(new W.TopBorder()), Line(new W.LeftBorder()), Line(new W.BottomBorder()), Line(new W.RightBorder()));
            if (table.InnerLines != false) outline.Append(Line(new W.InsideHorizontalBorder()), Line(new W.InsideVerticalBorder()));
            properties.Append(outline);
        }
        if (Margins(table) is { } margins) properties.Append(margins);
        if (properties.HasChildren) element.Append(properties);
        if (table.Background is { } background)
            element.Append(new W.StyleTableCellProperties(Shading(background)));
        var header = TableRegionFormat.Merge(new TableRegionFormat { Background = table.HeaderBackground, Ink = table.HeaderColor, Bold = table.HeaderBold }, table.HeaderRow);
        if (header is { IsEmpty: false }) element.Append(Conditional(W.TableStyleOverrideValues.FirstRow, header));
        foreach (var (region, kind) in _regions)
        {
            var format = table.Region(region);
            if (table.HeaderBold == true && Emphasis(region)) format = (format ?? new TableRegionFormat()) with { Bold = format?.Bold ?? true };
            if (format is null || format.IsEmpty) continue;
            element.Append(Conditional(kind, format));
        }
        Bands(element, table.FirstBandBackground, table.FirstBandAxes, table.FirstBand, W.TableStyleOverrideValues.Band1Horizontal, W.TableStyleOverrideValues.Band1Vertical);
        Bands(element, table.BandBackground, table.BandAxes, table.SecondBand, W.TableStyleOverrideValues.Band2Horizontal, W.TableStyleOverrideValues.Band2Vertical);
    }

    private void Bands(W.Style element, uint? background, TableBandAxes? axes, TableRegionFormat? region, W.TableStyleOverrideValues rows, W.TableStyleOverrideValues columns)
    {
        var format = TableRegionFormat.Merge(new TableRegionFormat { Background = background }, region);
        if (format is null || format.IsEmpty) return;
        var along = axes ?? TableBandAxes.Both;
        if (along.HasFlag(TableBandAxes.Columns)) element.Append(Conditional(columns, format));
        if (along.HasFlag(TableBandAxes.Rows)) element.Append(Conditional(rows, format));
    }

    private W.TableStyleProperties Conditional(W.TableStyleOverrideValues kind, TableRegionFormat format)
    {
        var conditional = new W.TableStyleProperties { Type = kind };
        var run = new W.RunPropertiesBaseStyle();
        if (format.Font is { Length: > 0 } font) run.Append(new W.RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font });
        if (format.Bold is { } bold) run.Append(new W.Bold { Val = OnOffValue.FromBoolean(bold) });
        if (format.Italic is { } italic) run.Append(new W.Italic { Val = OnOffValue.FromBoolean(italic) });
        if (format.Ink is { } ink) run.Append(new W.Color { Val = Ink(ink) });
        if (format.FontSize is { } size && size > 0) run.Append(new W.FontSize { Val = ((int)Math.Round(size * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        if (run.HasChildren) conditional.Append(run);
        var cell = new W.TableStyleConditionalFormattingTableCellProperties();
        if (format.Borders is { } lines) cell.Append(Edges(lines));
        if (format.Background is { } fill) cell.Append(Shading(fill));
        if (cell.HasChildren) conditional.Append(cell);
        return conditional;
    }

    private W.TableCellBorders Edges(BoxBorders lines)
    {
        var written = borders.Write(new W.TableCellBorders(), lines);
        var sides = written.Elements<W.BorderType>().Zip(new BorderLine?[] { lines.Top, lines.Left, lines.Bottom, lines.Right }).ToList();
        foreach (var (side, line) in sides)
            if (line is null && side.Val?.Value == W.BorderValues.Nil && side.Size is null) side.Remove();
        return written;
    }

    private BoxBorders Cleared(OpenXmlElement edges, BoxBorders lines)
    {
        BorderLine? Explicit(BorderLine? line, W.BorderType? side) =>
            line ?? (side?.Val?.Value is { } value && (value == W.BorderValues.Nil || value == W.BorderValues.None) ? new BorderLine(0, 0xFF000000) : null);
        return new BoxBorders
        {
            Top = Explicit(lines.Top, edges.GetFirstChild<W.TopBorder>()),
            Left = Explicit(lines.Left, (W.BorderType?)edges.GetFirstChild<W.LeftBorder>() ?? edges.GetFirstChild<W.StartBorder>()),
            Bottom = Explicit(lines.Bottom, edges.GetFirstChild<W.BottomBorder>()),
            Right = Explicit(lines.Right, (W.BorderType?)edges.GetFirstChild<W.RightBorder>() ?? edges.GetFirstChild<W.EndBorder>()),
        };
    }

    private TableRegionFormat Format(W.TableStyleProperties conditional)
    {
        var run = conditional.GetFirstChild<W.RunPropertiesBaseStyle>();
        var cell = conditional.GetFirstChild<W.TableStyleConditionalFormattingTableCellProperties>();
        var fonts = run?.GetFirstChild<W.RunFonts>();
        var size = run?.GetFirstChild<W.FontSize>()?.Val?.Value is { } half && double.TryParse(half, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var halves) ? halves / 2 : (double?)null;
        var lines = cell?.GetFirstChild<W.TableCellBorders>() is { } edges ? Cleared(edges, borders.Read(edges)) : null;
        return new TableRegionFormat
        {
            Background = Fill(cell?.GetFirstChild<W.Shading>()?.Fill?.Value),
            Ink = Ink(run?.GetFirstChild<W.Color>()?.Val?.Value),
            Bold = run?.GetFirstChild<W.Bold>() is { } bold ? bold.Val?.Value ?? true : null,
            Italic = run?.GetFirstChild<W.Italic>() is { } italic ? italic.Val?.Value ?? true : null,
            Font = fonts?.Ascii?.Value ?? fonts?.HighAnsi?.Value,
            FontSize = size,
            Borders = lines is { Top: null, Bottom: null, Left: null, Right: null } ? null : lines is null ? null : new BoxBorders { Top = lines.Top, Bottom = lines.Bottom, Left = lines.Left, Right = lines.Right },
        };
    }

    public W.TableCellMarginDefault? Margins(TableProperties table)
    {
        if (table.CellPadding is null && table.VerticalCellPadding is null) return null;
        var side = (short)Math.Round((table.CellPadding ?? table.VerticalCellPadding ?? 0) * 20);
        var vertical = ((int)Math.Round((table.VerticalCellPadding ?? table.CellPadding ?? 0) * 20)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new W.TableCellMarginDefault(
            new W.TopMargin { Width = vertical, Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellLeftMargin { Width = side, Type = W.TableWidthValues.Dxa },
            new W.BottomMargin { Width = vertical, Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellRightMargin { Width = side, Type = W.TableWidthValues.Dxa });
    }

    public TableProperties Padding(W.TableCellMarginDefault? margins)
    {
        if (margins is null) return TableProperties.None;
        var left = margins.GetFirstChild<W.TableCellLeftMargin>()?.Width?.Value
                   ?? (margins.GetFirstChild<W.StartMargin>()?.Width?.Value is { } start && short.TryParse(start, out var parsed) ? parsed : null);
        var top = margins.GetFirstChild<W.TopMargin>()?.Width?.Value is { } text && int.TryParse(text, out var twips) ? twips : (int?)null;
        return new TableProperties
        {
            CellPadding = left is { } horizontal ? horizontal / 20.0 : null,
            VerticalCellPadding = top is { } vertical ? vertical / 20.0 : left is null ? 0 : null,
        };
    }

    public TableProperties Read(W.Style element)
    {
        var properties = element.GetFirstChild<W.StyleTableProperties>();
        var lines = Borders(properties?.GetFirstChild<W.TableBorders>());
        var padding = Padding(properties?.GetFirstChild<W.TableCellMarginDefault>());
        var header = element.Elements<W.TableStyleProperties>().FirstOrDefault(item => item.Type?.Value == W.TableStyleOverrideValues.FirstRow);
        uint? Band(params W.TableStyleOverrideValues[] kinds) => kinds
            .Select(kind => element.Elements<W.TableStyleProperties>().FirstOrDefault(item => item.Type?.Value == kind))
            .Select(item => Fill(item?.GetFirstChild<W.TableStyleConditionalFormattingTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value))
            .FirstOrDefault(color => color is not null);
        TableBandAxes? Axes(W.TableStyleOverrideValues rows, W.TableStyleOverrideValues columns)
        {
            bool Shaded(W.TableStyleOverrideValues kind) => element.Elements<W.TableStyleProperties>()
                .Any(item => item.Type?.Value == kind && Fill(item.GetFirstChild<W.TableStyleConditionalFormattingTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value) is not null);
            var axes = (Shaded(rows) ? TableBandAxes.Rows : TableBandAxes.None) | (Shaded(columns) ? TableBandAxes.Columns : TableBandAxes.None);
            return axes is TableBandAxes.None or TableBandAxes.Both ? null : axes;
        }
        var run = header?.GetFirstChild<W.RunPropertiesBaseStyle>();
        var headerBold = run?.GetFirstChild<W.Bold>() is { } bold ? bold.Val?.Value ?? true : (bool?)null;
        var read = new TableProperties
        {
            BorderWidth = lines.BorderWidth,
            BorderColor = lines.BorderColor,
            InnerLines = lines.InnerLines,
            CellPadding = padding.CellPadding,
            VerticalCellPadding = padding.VerticalCellPadding,
            Background = Fill(element.GetFirstChild<W.StyleTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value),
            HeaderBackground = Fill(header?.GetFirstChild<W.TableStyleConditionalFormattingTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value),
            HeaderColor = Ink(run?.GetFirstChild<W.Color>()?.Val?.Value),
            HeaderBold = headerBold,
            BandBackground = Band(W.TableStyleOverrideValues.Band2Horizontal, W.TableStyleOverrideValues.Band2Vertical),
            FirstBandBackground = Band(W.TableStyleOverrideValues.Band1Horizontal, W.TableStyleOverrideValues.Band1Vertical),
            BandAxes = Axes(W.TableStyleOverrideValues.Band2Horizontal, W.TableStyleOverrideValues.Band2Vertical),
            FirstBandAxes = Axes(W.TableStyleOverrideValues.Band1Horizontal, W.TableStyleOverrideValues.Band1Vertical),
        };
        TableRegionFormat? Extras(params W.TableStyleOverrideValues[] kinds) => kinds
            .Select(kind => element.Elements<W.TableStyleProperties>().FirstOrDefault(item => item.Type?.Value == kind))
            .Where(item => item is not null)
            .Select(item => Format(item!) with { Background = null, Ink = null, Bold = null })
            .FirstOrDefault(format => !format.IsEmpty);
        read = read with
        {
            HeaderRow = header is null ? null : Format(header) with { Background = null, Ink = null, Bold = null } is { IsEmpty: false } headerExtras ? headerExtras : null,
            FirstBand = Extras(W.TableStyleOverrideValues.Band1Horizontal, W.TableStyleOverrideValues.Band1Vertical),
            SecondBand = Extras(W.TableStyleOverrideValues.Band2Horizontal, W.TableStyleOverrideValues.Band2Vertical),
        };
        foreach (var (region, kind) in _regions)
        {
            var conditional = element.Elements<W.TableStyleProperties>().FirstOrDefault(item => item.Type?.Value == kind);
            if (conditional is null) continue;
            var format = Format(conditional);
            if (headerBold == true && Emphasis(region) && format.Bold == true) format = format with { Bold = null };
            read = read.WithRegion(region, format);
        }
        return read;
    }

    private bool Emphasis(TableRegion region) => region is TableRegion.TotalRow or TableRegion.FirstColumn or TableRegion.LastColumn;

    private string Ink(uint argb) => argb >> 24 == 0 ? "auto" : values.Color(argb);

    private uint? Ink(string? hex) => string.Equals(hex, "auto", StringComparison.OrdinalIgnoreCase) ? TableRegionFormat.NoColour : values.Color(hex);

    private uint? Fill(string? hex) => string.Equals(hex, "auto", StringComparison.OrdinalIgnoreCase) ? TableRegionFormat.NoColour : values.Color(hex);

    public TableProperties Borders(W.TableBorders? outline)
    {
        if (outline is null) return TableProperties.None;
        bool Drawn(W.BorderType? border) => border?.Val?.Value is { } value && value != W.BorderValues.None && value != W.BorderValues.Nil;
        W.BorderType? outer = outline.GetFirstChild<W.TopBorder>();
        if (!Drawn(outer)) outer = new W.BorderType?[] { outline.GetFirstChild<W.BottomBorder>(), outline.GetFirstChild<W.LeftBorder>(), outline.GetFirstChild<W.StartBorder>(), outline.GetFirstChild<W.RightBorder>(), outline.GetFirstChild<W.EndBorder>() }.FirstOrDefault(Drawn) ?? outer;
        var top = outer;
        var lined = Drawn(top);
        var inside = outline.GetFirstChild<W.InsideHorizontalBorder>()?.Val?.Value;
        return new TableProperties
        {
            BorderWidth = lined ? Math.Min(top!.Size?.Value ?? 4, 96u) / 8.0 : 0,
            BorderColor = lined ? values.Color(top!.Color?.Value) : null,
            InnerLines = lined ? inside is { } line && line != W.BorderValues.None && line != W.BorderValues.Nil : null,
        };
    }

    private W.Shading Shading(uint color) => new() { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = color >> 24 == 0 ? "auto" : values.Color(color) };
}

public sealed class DocxCellFormulas(ITableFormulaParser parser, ITableFormulaSyntax canonical)
{
    public void Write(W.TableCell cell, string formula)
    {
        if (!parser.TryParse(formula, out var node) || node is null) return;
        var paragraph = cell.Elements<W.Paragraph>().FirstOrDefault();
        if (paragraph is null) return;
        var runs = paragraph.Elements<W.Run>().ToList();
        var field = new W.SimpleField { Instruction = " =" + canonical.Print(node, null) + " " };
        foreach (var run in runs)
        {
            run.Remove();
            field.Append(run);
        }
        paragraph.Append(field);
    }

    public string? Read(W.TableCell cell)
    {
        foreach (var simple in cell.Descendants<W.SimpleField>())
            if (Formula(simple.Instruction?.Value) is { } found) return found;
        var codes = string.Concat(cell.Descendants<W.FieldCode>().Select(code => code.Text));
        return Formula(codes);
    }

    private string? Formula(string? instruction)
    {
        var text = instruction?.Trim();
        if (string.IsNullOrEmpty(text) || !text.StartsWith('=')) return null;
        var switches = text.IndexOf('\\');
        if (switches > 0) text = text[..switches].Trim();
        return parser.TryParse(text, out var node) && node is not null ? canonical.Print(node, null) : null;
    }
}
