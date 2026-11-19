using DocumentFormat.OpenXml;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxTableStyleCodec(DocxValues values)
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
            var borders = new W.TableBorders(Line(new W.TopBorder()), Line(new W.LeftBorder()), Line(new W.BottomBorder()), Line(new W.RightBorder()));
            if (table.InnerLines != false) borders.Append(Line(new W.InsideHorizontalBorder()), Line(new W.InsideVerticalBorder()));
            properties.Append(borders);
        }
        if (Margins(table) is { } margins) properties.Append(margins);
        if (properties.HasChildren) element.Append(properties);
        if (table.Background is { } background)
            element.Append(new W.StyleTableCellProperties(Shading(background)));
        if (table.HeaderBackground is not null || table.HeaderColor is not null || table.HeaderBold is not null)
        {
            var header = new W.TableStyleProperties { Type = W.TableStyleOverrideValues.FirstRow };
            var run = new W.RunPropertiesBaseStyle();
            if (table.HeaderBold is { } bold) run.Append(new W.Bold { Val = OnOffValue.FromBoolean(bold) });
            if (table.HeaderColor is { } ink) run.Append(new W.Color { Val = Ink(ink) });
            if (run.HasChildren) header.Append(run);
            if (table.HeaderBackground is { } fill) header.Append(new W.TableStyleConditionalFormattingTableCellProperties(Shading(fill)));
            element.Append(header);
        }
        foreach (var (region, kind) in _regions)
        {
            var format = table.Region(region);
            if (table.HeaderBold == true && Emphasis(region)) format = (format ?? new TableRegionFormat()) with { Bold = format?.Bold ?? true };
            if (format is null || format.IsEmpty) continue;
            var conditional = new W.TableStyleProperties { Type = kind };
            var run = new W.RunPropertiesBaseStyle();
            if (format.Bold is { } bold) run.Append(new W.Bold { Val = OnOffValue.FromBoolean(bold) });
            if (format.Ink is { } ink) run.Append(new W.Color { Val = Ink(ink) });
            if (run.HasChildren) conditional.Append(run);
            if (format.Background is { } fill) conditional.Append(new W.TableStyleConditionalFormattingTableCellProperties(Shading(fill)));
            element.Append(conditional);
        }
        if (table.FirstBandBackground is { } first)
        {
            var axes = table.FirstBandAxes ?? TableBandAxes.Both;
            if (axes.HasFlag(TableBandAxes.Columns)) element.Append(new W.TableStyleProperties(new W.TableStyleConditionalFormattingTableCellProperties(Shading(first))) { Type = W.TableStyleOverrideValues.Band1Vertical });
            if (axes.HasFlag(TableBandAxes.Rows)) element.Append(new W.TableStyleProperties(new W.TableStyleConditionalFormattingTableCellProperties(Shading(first))) { Type = W.TableStyleOverrideValues.Band1Horizontal });
        }
        if (table.BandBackground is { } band)
        {
            var axes = table.BandAxes ?? TableBandAxes.Both;
            if (axes.HasFlag(TableBandAxes.Columns)) element.Append(new W.TableStyleProperties(new W.TableStyleConditionalFormattingTableCellProperties(Shading(band))) { Type = W.TableStyleOverrideValues.Band2Vertical });
            if (axes.HasFlag(TableBandAxes.Rows)) element.Append(new W.TableStyleProperties(new W.TableStyleConditionalFormattingTableCellProperties(Shading(band))) { Type = W.TableStyleOverrideValues.Band2Horizontal });
        }
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
        foreach (var (region, kind) in _regions)
        {
            var conditional = element.Elements<W.TableStyleProperties>().FirstOrDefault(item => item.Type?.Value == kind);
            if (conditional is null) continue;
            var regionRun = conditional.GetFirstChild<W.RunPropertiesBaseStyle>();
            var format = new TableRegionFormat
            {
                Background = Fill(conditional.GetFirstChild<W.TableStyleConditionalFormattingTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value),
                Ink = Ink(regionRun?.GetFirstChild<W.Color>()?.Val?.Value),
                Bold = regionRun?.GetFirstChild<W.Bold>() is { } regionBold ? regionBold.Val?.Value ?? true : null,
            };
            if (headerBold == true && Emphasis(region) && format.Bold == true) format = format with { Bold = null };
            read = read.WithRegion(region, format);
        }
        return read;
    }

    private bool Emphasis(TableRegion region) => region is TableRegion.TotalRow or TableRegion.FirstColumn or TableRegion.LastColumn;

    private string Ink(uint argb) => argb >> 24 == 0 ? "auto" : values.Color(argb);

    private uint? Ink(string? hex) => string.Equals(hex, "auto", StringComparison.OrdinalIgnoreCase) ? TableRegionFormat.NoColour : values.Color(hex);

    private uint? Fill(string? hex) => string.Equals(hex, "auto", StringComparison.OrdinalIgnoreCase) ? TableRegionFormat.NoColour : values.Color(hex);

    public TableProperties Borders(W.TableBorders? borders)
    {
        if (borders is null) return TableProperties.None;
        bool Drawn(W.BorderType? border) => border?.Val?.Value is { } value && value != W.BorderValues.None && value != W.BorderValues.Nil;
        W.BorderType? outer = borders.GetFirstChild<W.TopBorder>();
        if (!Drawn(outer)) outer = new W.BorderType?[] { borders.GetFirstChild<W.BottomBorder>(), borders.GetFirstChild<W.LeftBorder>(), borders.GetFirstChild<W.StartBorder>(), borders.GetFirstChild<W.RightBorder>(), borders.GetFirstChild<W.EndBorder>() }.FirstOrDefault(Drawn) ?? outer;
        var top = outer;
        var lined = Drawn(top);
        var inside = borders.GetFirstChild<W.InsideHorizontalBorder>()?.Val?.Value;
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
