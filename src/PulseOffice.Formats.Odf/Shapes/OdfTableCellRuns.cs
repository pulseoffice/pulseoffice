using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Formats.Odf.TextCodecs;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfTableCellRuns(IOdfParagraphText paragraphs)
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    private static readonly XNamespace TextNs = OdfPackage.Namespaces.Text;

    private const double MinimumFontSize = 1;
    private const double MaximumFontSize = 4000;
    private const int MaximumFamilyLength = 256;

    private readonly OdfMeasure _measure = new();

    public IEnumerable<XElement> Write(TableCellText text, string prefix, XElement automaticStyles)
    {
        var index = 0;
        foreach (var paragraph in text.Paragraphs)
        {
            var element = new XElement(TextNs + "p");
            foreach (var run in paragraph.Runs.Where(run => run.Text.Length > 0))
            {
                var nodes = paragraphs.Paragraph(run.Text).Nodes().ToList();
                if (run.IsPlain)
                {
                    element.Add(nodes);
                    continue;
                }
                var name = $"{prefix}_t{index++}";
                automaticStyles.Add(new XElement(Style + "style",
                    new XAttribute(Style + "name", name),
                    new XAttribute(Style + "family", "text"),
                    Properties(run)));
                element.Add(new XElement(TextNs + "span", new XAttribute(TextNs + "style-name", name), nodes));
            }
            yield return element;
        }
    }

    public TableCellText? Read(XElement cell, int row, int column, IReadOnlyDictionary<string, XElement>? textStyles)
    {
        var result = new List<TableCellParagraph>();
        foreach (var paragraph in cell.Elements().Where(element => element.Name == TextNs + "p" || element.Name == TextNs + "h"))
        {
            var runs = new List<TableTextRun>();
            var probe = new XElement(TextNs + "p");
            var before = string.Empty;
            foreach (var node in paragraph.Nodes())
            {
                var styled = node is XElement { } span && span.Name == TextNs + "span"
                    && (string?)span.Attribute(TextNs + "style-name") is { } styleName
                    && textStyles?.GetValueOrDefault(styleName)?.Element(Style + "text-properties") is { } properties
                        ? Format(properties)
                        : null;
                probe.Add(node is XElement element ? new XElement(element) : node is XText text ? (XNode)new XText(text) : null);
                var after = paragraphs.Line(probe);
                if (!after.StartsWith(before, StringComparison.Ordinal)) return null;
                var piece = after[before.Length..];
                before = after;
                if (piece.Length == 0) continue;
                var run = (styled ?? new TableTextRun(string.Empty)) with { Text = piece };
                if (runs.Count > 0 && runs[^1].SameFormat(run)) runs[^1] = runs[^1] with { Text = runs[^1].Text + piece };
                else runs.Add(run);
            }
            result.Add(new TableCellParagraph(runs));
        }
        var cellText = new TableCellText(row, column, result);
        return cellText.IsPlain ? null : cellText;
    }

    private XElement Properties(TableTextRun run)
    {
        var text = new XElement(Style + "text-properties");
        if (run.TextHex is { } ink) text.Add(new XAttribute(Fo + "color", _measure.Colour(ink)));
        if (run.Bold is { } bold) text.Add(new XAttribute(Fo + "font-weight", bold ? "bold" : "normal"));
        if (run.Italic is { } italic) text.Add(new XAttribute(Fo + "font-style", italic ? "italic" : "normal"));
        if (run.Underline is { } underline)
            text.Add(new XAttribute(Style + "text-underline-style", underline ? "solid" : "none"),
                new XAttribute(Style + "text-underline-width", "auto"),
                new XAttribute(Style + "text-underline-color", "font-color"));
        if (run.Strikethrough is { } strike) text.Add(new XAttribute(Style + "text-line-through-style", strike ? "solid" : "none"));
        if (Family(run.FontFamily) is { } family)
            text.Add(new XAttribute(Fo + "font-family", family.Contains(' ', StringComparison.Ordinal) ? $"'{family}'" : family));
        if (run.FontSize is { } size) text.Add(new XAttribute(Fo + "font-size", $"{_measure.Number(size)}pt"));
        return text;
    }

    private TableTextRun Format(XElement text) => new(string.Empty)
    {
        TextHex = (string?)text.Attribute(Fo + "color") is { Length: 7 } colour && colour[0] == '#' ? colour.ToUpperInvariant() : null,
        Bold = (string?)text.Attribute(Fo + "font-weight") switch
        {
            null => null,
            "bold" or "600" or "700" or "800" or "900" => true,
            _ => false,
        },
        Italic = (string?)text.Attribute(Fo + "font-style") switch
        {
            null => null,
            "italic" or "oblique" => true,
            _ => false,
        },
        Underline = (string?)text.Attribute(Style + "text-underline-style") is { } underline ? underline != "none" : null,
        Strikethrough = (string?)text.Attribute(Style + "text-line-through-style") is { } strike ? strike != "none" : null,
        FontFamily = Family((string?)text.Attribute(Fo + "font-family") ?? (string?)text.Attribute(Style + "font-name")),
        FontSize = (string?)text.Attribute(Fo + "font-size") is { } size && size.EndsWith("pt", StringComparison.Ordinal)
            && double.TryParse(size[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var points) && points is >= MinimumFontSize and <= MaximumFontSize ? points : null,
    };

    private string? Family(string? value)
    {
        var family = value?.Split(',')[0].Trim().Trim('\'', '"').Trim();
        return family is { Length: > 0 and <= MaximumFamilyLength } ? family : null;
    }
}
