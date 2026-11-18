using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtColumnsCodec(OdtMeasure measure) : OdtXml
{
    public XElement Write(SectionColumns columns)
    {
        var element = new XElement(Style + "columns",
            new XAttribute(Fo + "column-count", Math.Max(1, columns.Count)),
            new XAttribute(Fo + "column-gap", measure.Length(columns.Gap)));
        if (columns.Uneven)
            element.Add(columns.Spans.Select((span, index) => new XElement(Style + "column",
                new XAttribute(Style + "rel-width", Math.Max(1, span.Width.Value).ToString(CultureInfo.InvariantCulture) + "*"),
                new XAttribute(Fo + "start-indent", measure.Length(index == 0 ? Twip.Zero : new Twip(columns.Spans[index - 1].Gap.Value / 2))),
                new XAttribute(Fo + "end-indent", measure.Length(index == columns.Spans.Count - 1 ? Twip.Zero : new Twip(span.Gap.Value - span.Gap.Value / 2))))));
        if (columns.Separator)
            element.Add(new XElement(Style + "column-sep", new XAttribute(Style + "style", "solid"), new XAttribute(Style + "width", "0.5pt"), new XAttribute(Style + "color", "#000000")));
        return element;
    }

    public SectionColumns Read(XElement? properties)
    {
        var element = properties?.Element(Style + "columns");
        if (element is null) return SectionColumns.Single;
        var count = Math.Clamp(measure.Integer((string?)element.Attribute(Fo + "column-count")) ?? 1, 1, SectionColumns.LargestCount);
        var gap = measure.Twips((string?)element.Attribute(Fo + "column-gap"));
        if (gap is null && element.Elements(Style + "column").Skip(1).FirstOrDefault() is { } second)
            gap = (measure.Twips((string?)second.Attribute(Fo + "start-indent")) ?? Twip.Zero) * 2;
        var balanced = (string?)properties!.Attribute(Text + "dont-balance-text-columns") != "true";
        if (count <= 1 && balanced && element.Element(Style + "column-sep") is null) return SectionColumns.Single;
        var columns = new SectionColumns(Math.Max(1, count), gap ?? Twip.Zero)
        {
            Separator = element.Element(Style + "column-sep") is { } sep && (string?)sep.Attribute(Style + "style") != "none",
            Balanced = balanced,
        };
        return Spans(element, columns) is { } spans ? columns with { Spans = spans } : columns;
    }

    private List<ColumnSpan>? Spans(XElement element, SectionColumns columns)
    {
        var items = element.Elements(Style + "column").ToList();
        if (columns.Count <= 1 || items.Count != columns.Count) return null;
        var weights = items.Select(item => RelativeWidth((string?)item.Attribute(Style + "rel-width"))).ToList();
        if (weights.Any(weight => weight <= 0)) return null;
        var spans = items.Select((item, index) => new ColumnSpan(
            new Twip(weights[index]),
            index == items.Count - 1 ? Twip.Zero
                : (measure.Twips((string?)item.Attribute(Fo + "end-indent")) ?? Twip.Zero)
                  + (measure.Twips((string?)items[index + 1].Attribute(Fo + "start-indent")) ?? Twip.Zero))).ToList();
        var uneven = weights.Distinct().Skip(1).Any() || spans.Take(spans.Count - 1).Select(span => span.Gap).Distinct().Skip(1).Any();
        return uneven ? spans : null;
    }

    private int RelativeWidth(string? text) =>
        text is { Length: > 1 } && text.EndsWith('*') && int.TryParse(text[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight) ? Math.Min(weight, 1_000_000) : 0;
}
