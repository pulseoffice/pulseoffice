using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Formats.Interchange;

public static class HtmlFormats
{
    public static readonly SpreadsheetFormat Html = new()
    {
        Id = "html",
        DisplayName = "HTML",
        Extensions = [".html", ".htm"],
        MimeType = "text/html",
        Capabilities = FormatCapabilities.MultipleSheets | FormatCapabilities.CellFormatting,
    };
}

public sealed class HtmlSpreadsheetWriter(ICellPresenter presenter) : ISpreadsheetWriter
{
    private readonly NumberFormatCatalog _builtInFormats = new();

    public SpreadsheetFormat Format => HtmlFormats.Html;

    public FidelityReport CheckFidelity(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var losses = new List<Diagnostic>
        {
            Diagnostic.Warning("HTML001", "HTML keeps what is visible: formulas become their result."),
        };

        var objects = workbook.Sheets.Sum(sheet => sheet.Objects.Count);

        if (objects > 0)
            losses.Add(Diagnostic.Warning("HTML002", $"{objects} objects are not written to HTML.").With(objects));

        return new FidelityReport(losses);
    }

    public OperationResult<bool> Write(Workbook workbook, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);

            writer.Write(Render(workbook));
            writer.Flush();

            return OperationResult<bool>.Ok(true);
        }
        catch (IOException ex)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("HTML100", $"Writing the HTML failed: {ex.Message}").With(ex.Message));
        }
    }

    public string Render(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var page = new StringBuilder();

        page.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\">\n")
            .Append("<title>").Append(Escape(Title(workbook))).Append("</title>\n")
            .Append("<style>\n")
            .Append("body{font-family:system-ui,-apple-system,Segoe UI,sans-serif;margin:24px;color:#0F172A}\n")
            .Append("h2{font-size:1rem;letter-spacing:.08em;text-transform:uppercase;color:#475569}\n")
            .Append("table{border-collapse:collapse;margin-bottom:32px}\n")
            .Append("td{border:1px solid #CBD5E1;padding:4px 8px;font-size:.9rem}\n")
            .Append("td.n{text-align:right;font-variant-numeric:tabular-nums}\n")
            .Append("</style>\n</head><body>\n");

        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.IsHidden) continue;

            page.Append("<h2>").Append(Escape(sheet.Name)).Append("</h2>\n");
            page.Append(RenderSheet(sheet, workbook.Styles));
        }

        return page.Append("</body></html>\n").ToString();
    }

    public string RenderFragment(Worksheet sheet, CellRange range, StyleCatalog styles)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(styles);
        return "<html><head><meta charset=\"utf-8\"></head><body>\n" + RenderRange(sheet, range, styles, standalone: true) + "</body></html>\n";
    }

    private string RenderSheet(Worksheet sheet, StyleCatalog styles) =>
        sheet.UsedRange() is { } used ? RenderRange(sheet, used, styles, standalone: false) : "<table></table>\n";

    private string RenderRange(Worksheet sheet, CellRange used, StyleCatalog styles, bool standalone)
    {
        var table = new StringBuilder("<table>\n");

        for (var row = used.Start.Row; row <= used.End.Row; row++)
        {
            if (sheet.Layout.IsRowHidden(row)) continue;

            table.Append("<tr>");

            for (var column = used.Start.Column; column <= used.End.Column; column++)
            {
                if (sheet.Layout.IsColumnHidden(column)) continue;

                var address = new CellAddress(row, column);

                if (sheet.Merges.IsCovered(address)) continue;

                var cell = sheet[address] with { StyleId = sheet.StyleIdAt(address) };
                var shown = presenter.Present(cell, styles, sheet.Owner?.NumberFormats ?? _builtInFormats, 12);
                var style = styles.Get(cell.StyleId);
                var span = sheet.Merges.At(address);

                table.Append("<td");

                var alignRight = shown.AlignRight || style.Horizontal == CellHorizontalAlignment.Right;
                if (alignRight)
                    table.Append(" class=\"n\"");

                if (span is { } region && region.Start == address)
                {
                    var columns = Enumerable.Range(region.Start.Column, region.End.Column - region.Start.Column + 1)
                        .Count(index => !sheet.Layout.IsColumnHidden(index));
                    var rows = Enumerable.Range(region.Start.Row, region.End.Row - region.Start.Row + 1)
                        .Count(index => !sheet.Layout.IsRowHidden(index));

                    if (columns > 1) table.Append(CultureInfo.InvariantCulture, $" colspan=\"{columns}\"");
                    if (rows > 1) table.Append(CultureInfo.InvariantCulture, $" rowspan=\"{rows}\"");
                }

                if (Inline(style, standalone, alignRight) is { Length: > 0 } inline)
                    table.Append(" style=\"").Append(Escape(inline)).Append('"');

                table.Append('>').Append(Lines(shown.Text)).Append("</td>");
            }

            table.Append("</tr>\n");
        }

        return table.Append("</table>\n").ToString();
    }

    private static string Inline(CellStyle style, bool standalone, bool alignRight)
    {
        var rules = new List<string>();

        if (standalone)
        {
            rules.Add("font-family:'" + style.Font.Family.Replace("'", string.Empty, StringComparison.Ordinal) + "'");
            rules.Add(string.Create(CultureInfo.InvariantCulture, $"font-size:{style.Font.Size}pt"));
            if (alignRight) rules.Add("text-align:right");
        }
        if (style.Horizontal == CellHorizontalAlignment.Center) rules.Add("text-align:center");

        if (style.Font.Bold) rules.Add("font-weight:600");
        if (style.Font.Italic) rules.Add("font-style:italic");
        if (style.Font.Underline || style.Font.DoubleUnderline) rules.Add("text-decoration:underline");
        if (style.BackgroundHex is { } background && IsColour(background)) rules.Add("background:" + background);
        if (style.Font.ColorHex is { } colour && IsColour(colour) && colour != "#000000") rules.Add("color:" + colour);
        Border(rules, "top", style.Borders.Top);
        Border(rules, "right", style.Borders.Right);
        Border(rules, "bottom", style.Borders.Bottom);
        Border(rules, "left", style.Borders.Left);

        return string.Join(';', rules);
    }

    private static void Border(List<string> rules, string side, BorderEdge edge)
    {
        if (!edge.IsVisible) return;
        var line = edge.Style switch
        {
            BorderStyle.Dashed => "dashed",
            BorderStyle.Dotted => "dotted",
            BorderStyle.Double => "double",
            _ => "solid",
        };
        var colour = IsColour(edge.ColorHex) ? edge.ColorHex : "#000000";
        rules.Add(string.Create(CultureInfo.InvariantCulture, $"border-{side}:{Math.Round(edge.Width.ToPoints(), 2)}pt {line} {colour}"));
    }

    private static bool IsColour(string value) =>
        value.Length is 7 or 9 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private static string Title(Workbook workbook) =>
        workbook.Properties.Title is { Length: > 0 } title ? title : workbook.Sheets[0].Name;

    private static string Lines(string text) =>
        Escape(text.ReplaceLineEndings("\n")).Replace("\n", "<br>", StringComparison.Ordinal);

    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
