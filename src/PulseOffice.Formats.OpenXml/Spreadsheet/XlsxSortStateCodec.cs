using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxSortStateCodec
{
    private const int MaxConditions = 64;
    private const string ExtensionUri = "{5B0E8C2A-7D4F-4E61-9C3B-2F8A1D6E4B70}";
    private const string Namespace = "urn:pulseoffice:sort-order";
    private const string Prefix = "pso";

    public SortState? Build(CellRange range, bool hasHeader, SortSettings? sort)
    {
        if (sort is null || !sort.FitsWithin(range)) return null;
        var data = new SheetRangeSort(range, hasHeader, sort).Data;
        var state = new SortState
        {
            Reference = Reference(data),
            CaseSensitive = sort.CaseSensitive ? true : null,
            ColumnSort = sort.LeftToRight ? true : null,
        };
        var orders = new OpenXmlUnknownElement(Prefix, "orders", Namespace);
        var index = 0;
        foreach (var field in sort.Fields.Take(MaxConditions))
        {
            if (field.CustomOrder is { } items && items.Any(item => item.Contains(',', StringComparison.Ordinal)))
                orders.AppendChild(Order(index, items));
            index++;
            var key = sort.LeftToRight
                ? new CellRange(new CellAddress(range.Start.Row + field.ColumnOffset, data.Start.Column), new CellAddress(range.Start.Row + field.ColumnOffset, data.End.Column))
                : new CellRange(new CellAddress(data.Start.Row, range.Start.Column + field.ColumnOffset), new CellAddress(data.End.Row, range.Start.Column + field.ColumnOffset));
            state.Append(new SortCondition
            {
                Reference = Reference(key),
                Descending = field.Ascending ? null : true,
                CustomList = field.CustomOrder is { Count: > 0 } order ? string.Join(',', order) : null,
            });
        }
        if (orders.HasChildren) state.Append(new ExtensionList(new Extension(orders) { Uri = ExtensionUri }));
        return state;
    }

    private OpenXmlUnknownElement Order(int condition, IReadOnlyList<string> items)
    {
        var order = new OpenXmlUnknownElement(Prefix, "order", Namespace);
        order.SetAttribute(new OpenXmlAttribute("condition", string.Empty, condition.ToString(CultureInfo.InvariantCulture)));
        foreach (var item in items)
        {
            var entry = new OpenXmlUnknownElement(Prefix, "item", Namespace);
            entry.SetAttribute(new OpenXmlAttribute("value", string.Empty, item));
            order.AppendChild(entry);
        }
        return order;
    }

    private Dictionary<int, IReadOnlyList<string>> Orders(SortState state)
    {
        var orders = new Dictionary<int, IReadOnlyList<string>>();
        var stored = state.GetFirstChild<ExtensionList>()?.Elements<Extension>()
            .Where(extension => extension.Uri?.Value == ExtensionUri)
            .SelectMany(extension => extension.ChildElements)
            .Where(element => element.NamespaceUri == Namespace && element.LocalName == "orders")
            .SelectMany(element => element.ChildElements)
            .Where(element => element.NamespaceUri == Namespace && element.LocalName == "order") ?? [];
        foreach (var order in stored)
            if (int.TryParse(Attribute(order, "condition"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var condition))
                orders[condition] = [.. order.ChildElements.Where(element => element.LocalName == "item").Select(element => Attribute(element, "value") ?? string.Empty)];
        return orders;
    }

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().Where(attribute => attribute.LocalName == name).Select(attribute => attribute.Value).FirstOrDefault();

    public SortState? Build(SheetRangeSort? remembered) =>
        remembered is null ? null : Build(remembered.Range, remembered.HasHeader, remembered.Settings);

    public SortSettings? Read(SortState? state, CellRange range)
    {
        if (state is null) return null;
        var leftToRight = state.ColumnSort?.Value == true;
        var fields = new List<SortField>();
        var orders = Orders(state);
        var index = -1;
        foreach (var condition in state.Elements<SortCondition>().Take(MaxConditions))
        {
            index++;
            if (condition.Reference?.Value is not { } reference || Parse(reference) is not { } area) continue;
            var offset = leftToRight ? area.Start.Row - range.Start.Row : area.Start.Column - range.Start.Column;
            if (offset < 0 || offset >= (leftToRight ? range.RowCount : range.ColumnCount)) continue;
            var field = new SortField(offset, condition.Descending?.Value != true);
            if (condition.CustomList?.Value is { Length: > 0 } list)
                field = field with
                {
                    CustomOrder = orders.TryGetValue(index, out var stored) && string.Join(',', stored) == list ? stored : list.Split(','),
                };
            fields.Add(field);
        }
        return fields.Count == 0 ? null : new SortSettings
        {
            Fields = fields,
            CaseSensitive = state.CaseSensitive?.Value == true,
            Orientation = leftToRight ? SortOrientation.LeftToRight : SortOrientation.TopToBottom,
        };
    }

    public SheetRangeSort? ReadPlain(SortState? state)
    {
        if (state?.Reference?.Value is not { } reference || Parse(reference) is not { } range) return null;
        return Read(state, range) is { } settings ? new SheetRangeSort(range, false, settings) : null;
    }

    public CellRange? Area(SortState? state) => state?.Reference?.Value is { } reference ? Parse(reference) : null;

    private string Reference(CellRange range) => $"{range.Start.ToA1()}:{range.End.ToA1()}";

    private CellRange? Parse(string reference)
    {
        var plain = reference.Replace("$", string.Empty, StringComparison.Ordinal);
        if (CellRange.TryParseA1(plain, out var range)) return range;
        return CellAddress.TryParseA1(plain, out var single) ? new CellRange(single, single) : null;
    }
}
