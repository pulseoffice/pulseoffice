using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.Odf;

internal readonly record struct TableRow(XElement Element, int Index, int Repeat);

internal readonly record struct TableCell(XElement Element, CellAddress Address, int Repeat);

internal sealed class TableRows
{
    private static readonly XNamespace Table = OdfPackage.Namespaces.Table;

    public IEnumerable<TableRow> In(XElement table)
    {
        var index = 0;
        foreach (var row in Rows(table))
        {
            var repeat = Repeat(row, Table + "number-rows-repeated");
            yield return new TableRow(row, index, repeat);
            index = Advance(index, repeat);
        }
    }

    public IEnumerable<XElement> Columns(XElement container) =>
        Flattened(container, Table + "table-column", Table + "table-column-group", Table + "table-header-columns");

    public int Advance(int index, int repeat) => (int)Math.Min((long)index + repeat, int.MaxValue);

    public int Repeat(XElement element, XName attribute) =>
        int.TryParse((string?)element.Attribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : 1;

    private IEnumerable<XElement> Rows(XElement container) =>
        Flattened(container, Table + "table-row", Table + "table-row-group", Table + "table-header-rows");

    private IEnumerable<XElement> Flattened(XElement container, XName item, XName group, XName header)
    {
        var pending = new Stack<XElement>();
        Push(container);
        while (pending.TryPop(out var element))
        {
            if (element.Name == item) yield return element;
            else if (element.Name == group || element.Name == header) Push(element);
        }

        void Push(XElement parent)
        {
            var children = parent.Elements().ToList();
            for (var position = children.Count - 1; position >= 0; position--) pending.Push(children[position]);
        }
    }
}

internal sealed class TableCells
{
    private static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    private readonly TableRows _rows = new();

    public IEnumerable<TableCell> In(XElement table) => _rows.In(table).SelectMany(Of);

    public IEnumerable<TableCell> Of(TableRow row)
    {
        var column = 0;
        foreach (var cell in row.Element.Elements())
        {
            if (cell.Name != Table + "table-cell" && cell.Name != Table + "covered-table-cell") continue;

            var repeat = _rows.Repeat(cell, Table + "number-columns-repeated");
            yield return new TableCell(cell, new CellAddress(row.Index, column), repeat);
            column = _rows.Advance(column, repeat);
        }
    }
}
