using DocumentFormat.OpenXml;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxTableStyleCodec(DocxValues values)
{
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
            if (table.HeaderColor is { } ink) run.Append(new W.Color { Val = values.Color(ink) });
            if (run.HasChildren) header.Append(run);
            if (table.HeaderBackground is { } fill) header.Append(new W.TableStyleConditionalFormattingTableCellProperties(Shading(fill)));
            element.Append(header);
        }
        if (table.BandBackground is { } band)
            element.Append(new W.TableStyleProperties(new W.TableStyleConditionalFormattingTableCellProperties(Shading(band))) { Type = W.TableStyleOverrideValues.Band2Horizontal });
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
            VerticalCellPadding = top is { } vertical ? vertical / 20.0 : 0,
        };
    }

    public TableProperties Read(W.Style element)
    {
        var properties = element.GetFirstChild<W.StyleTableProperties>();
        var lines = Borders(properties?.GetFirstChild<W.TableBorders>());
        var padding = Padding(properties?.GetFirstChild<W.TableCellMarginDefault>());
        var header = element.Elements<W.TableStyleProperties>().FirstOrDefault(item => item.Type?.Value == W.TableStyleOverrideValues.FirstRow);
        var band = element.Elements<W.TableStyleProperties>().FirstOrDefault(item => item.Type?.Value == W.TableStyleOverrideValues.Band2Horizontal);
        var run = header?.GetFirstChild<W.RunPropertiesBaseStyle>();
        return new TableProperties
        {
            BorderWidth = lines.BorderWidth,
            BorderColor = lines.BorderColor,
            InnerLines = lines.InnerLines,
            CellPadding = padding.CellPadding,
            VerticalCellPadding = padding.VerticalCellPadding,
            Background = values.Color(element.GetFirstChild<W.StyleTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value),
            HeaderBackground = values.Color(header?.GetFirstChild<W.TableStyleConditionalFormattingTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value),
            HeaderColor = values.Color(run?.GetFirstChild<W.Color>()?.Val?.Value),
            HeaderBold = run?.GetFirstChild<W.Bold>() is { } bold ? bold.Val?.Value ?? true : null,
            BandBackground = values.Color(band?.GetFirstChild<W.TableStyleConditionalFormattingTableCellProperties>()?.GetFirstChild<W.Shading>()?.Fill?.Value),
        };
    }

    public TableProperties Borders(W.TableBorders? borders)
    {
        if (borders is null) return TableProperties.None;
        var top = borders.GetFirstChild<W.TopBorder>();
        var lined = top?.Val?.Value is { } value && value != W.BorderValues.None && value != W.BorderValues.Nil;
        var inside = borders.GetFirstChild<W.InsideHorizontalBorder>()?.Val?.Value;
        return new TableProperties
        {
            BorderWidth = lined ? Math.Min(top!.Size?.Value ?? 4, 96u) / 8.0 : 0,
            BorderColor = lined ? values.Color(top!.Color?.Value) : null,
            InnerLines = lined ? inside is { } line && line != W.BorderValues.None && line != W.BorderValues.Nil : null,
        };
    }

    private W.Shading Shading(uint color) => new() { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = values.Color(color) };
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
