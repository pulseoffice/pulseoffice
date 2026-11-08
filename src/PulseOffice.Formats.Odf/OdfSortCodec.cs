using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfSortCodec
{
    private const char OrderSeparator = '\n';
    private const int MaxFields = 64;

    public XNamespace Namespace { get; } = "urn:pulseoffice:sort";

    public XElement? Build(SortSettings? sort, Sheets.Addressing.CellRange range)
    {
        if (sort is null || !sort.FitsWithin(range)) return null;
        var element = new XElement(Namespaces.Table + "sort");
        if (sort.CaseSensitive) element.Add(new XAttribute(Namespaces.Table + "case-sensitive", "true"));
        foreach (var field in sort.Fields.Take(MaxFields))
        {
            var by = new XElement(Namespaces.Table + "sort-by",
                new XAttribute(Namespaces.Table + "field-number", field.ColumnOffset.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Table + "data-type", "automatic"),
                new XAttribute(Namespaces.Table + "order", field.Ascending ? "ascending" : "descending"));
            if (field.CustomOrder is { Count: > 0 } order)
                by.Add(new XAttribute(XNamespace.Xmlns + "pulseoffice-sort", Namespace.NamespaceName),
                    new XAttribute(Namespace + "custom-order", string.Join(OrderSeparator, order)));
            element.Add(by);
        }
        return element;
    }

    public XAttribute? Orientation(SortSettings? sort) => sort is { LeftToRight: true } ? new XAttribute(Namespaces.Table + "orientation", "column") : null;

    public SortSettings? Read(XElement? element, Sheets.Addressing.CellRange range)
    {
        if (element is null) return null;
        var leftToRight = (string?)element.Parent?.Attribute(Namespaces.Table + "orientation") == "column";
        var columns = leftToRight ? range.RowCount : range.ColumnCount;
        var fields = new List<SortField>();
        foreach (var by in element.Elements(Namespaces.Table + "sort-by").Take(MaxFields))
        {
            if (!int.TryParse((string?)by.Attribute(Namespaces.Table + "field-number"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)
                || offset < 0 || offset >= columns) continue;
            var field = new SortField(offset, (string?)by.Attribute(Namespaces.Table + "order") != "descending");
            if ((string?)by.Attribute(Namespace + "custom-order") is { Length: > 0 } order)
                field = field with { CustomOrder = order.Split(OrderSeparator) };
            fields.Add(field);
        }
        return fields.Count == 0 ? null : new SortSettings
        {
            Fields = fields,
            CaseSensitive = (string?)element.Attribute(Namespaces.Table + "case-sensitive") == "true",
            Orientation = leftToRight ? SortOrientation.LeftToRight : SortOrientation.TopToBottom,
        };
    }
}
