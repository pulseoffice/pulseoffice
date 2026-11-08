using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal interface IOdsRowSink
{
    void BodyStarted(XDocument content);

    bool Row(XElement table, TableRow row);

    void TableEnded(XElement table);
}

internal sealed class OdsContentLoader(TableRows rows, OdsRowCompactor compactor)
{
    private static readonly XName TableName = Namespaces.Table + "table";
    private static readonly XName RowName = Namespaces.Table + "table-row";
    private static readonly XName BodyName = Namespaces.Office + "body";
    private static readonly XName SpreadsheetName = Namespaces.Office + "spreadsheet";

    public XDocument Load(Stream stream, IOdsRowSink sink)
    {
        var document = new XDocument();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, CloseInput = false };
        using var reader = XmlReader.Create(stream, settings);

        var open = new Stack<XContainer>();
        open.Push(document);
        var tables = new Stack<(XElement Table, int NextRow)>();

        reader.Read();
        while (!reader.EOF)
        {
            var parent = open.Peek();
            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.LocalName == RowName.LocalName && reader.NamespaceURI == RowName.NamespaceName
                    && tables.Count > 0 && InSheet(parent, tables.Peek().Table):
                    var row = (XElement)XNode.ReadFrom(reader);
                    var (table, next) = tables.Pop();
                    var repeat = rows.Repeat(row, Namespaces.Table + "number-rows-repeated");
                    var compact = sink.Row(table, new TableRow(row, next, repeat));
                    tables.Push((table, rows.Advance(next, repeat)));
                    if (compact) compactor.Append((XElement)parent, row);
                    else parent.Add(row);
                    continue;

                case XmlNodeType.Element:
                    var element = new XElement(XName.Get(reader.LocalName, reader.NamespaceURI));
                    if (reader.MoveToFirstAttribute())
                    {
                        do element.Add(new XAttribute(AttributeName(reader), reader.Value));
                        while (reader.MoveToNextAttribute());
                        reader.MoveToElement();
                    }

                    if (element.Name == BodyName) sink.BodyStarted(document);
                    parent.Add(element);
                    var sheet = element.Name == TableName && parent is XElement { Name: var parentName } && parentName == SpreadsheetName;
                    if (sheet && !reader.IsEmptyElement) tables.Push((element, 0));
                    if (sheet && reader.IsEmptyElement) sink.TableEnded(element);
                    if (!reader.IsEmptyElement) open.Push(element);
                    break;

                case XmlNodeType.EndElement:
                    if (open.Pop() is XElement closed && closed.Name == TableName && tables.Count > 0 && ReferenceEquals(tables.Peek().Table, closed))
                    {
                        tables.Pop();
                        sink.TableEnded(closed);
                    }
                    break;

                case XmlNodeType.Text or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                    parent.Add(new XText(reader.Value));
                    break;

                case XmlNodeType.CDATA:
                    parent.Add(new XCData(reader.Value));
                    break;

                case XmlNodeType.Comment:
                    parent.Add(new XComment(reader.Value));
                    break;

                case XmlNodeType.ProcessingInstruction:
                    parent.Add(new XProcessingInstruction(reader.Name, reader.Value));
                    break;
            }
            reader.Read();
        }

        return document;
    }

    private bool InSheet(XContainer parent, XElement table)
    {
        for (var current = parent as XElement; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, table)) return true;
            if (current.Name == TableName) return false;
        }
        return false;
    }

    private XName AttributeName(XmlReader reader) =>
        reader.NamespaceURI == XNamespace.Xmlns.NamespaceName
            ? reader.Prefix.Length == 0 ? XName.Get("xmlns") : XNamespace.Xmlns + reader.LocalName
            : XName.Get(reader.LocalName, reader.NamespaceURI);
}

internal sealed class OdsRowCompactor(TableRows rows)
{
    private static readonly XName Cell = Namespaces.Table + "table-cell";
    private static readonly XName Covered = Namespaces.Table + "covered-table-cell";
    private static readonly XName Paragraph = Namespaces.Text + "p";
    private static readonly XName ColumnsRepeated = Namespaces.Table + "number-columns-repeated";

    private static readonly HashSet<XName> ValueAttributes =
    [
        Namespaces.Table + "style-name", ColumnsRepeated, Namespaces.Table + "formula",
        Namespaces.Office + "value-type", Namespaces.Office + "value", Namespaces.Office + "date-value",
        Namespaces.Office + "time-value", Namespaces.Office + "boolean-value", Namespaces.Office + "string-value",
        Namespaces.Office + "currency", Namespaces.Calcext + "value-type",
    ];

    public void Append(XElement parent, XElement row)
    {
        Compact(row);
        parent.Add(row);
    }

    private void Compact(XElement row)
    {
        XElement? run = null;
        foreach (var cell in row.Elements().ToList())
        {
            if ((cell.Name != Cell && cell.Name != Covered) || !Plain(cell))
            {
                run = null;
                continue;
            }

            var repeat = rows.Repeat(cell, ColumnsRepeated);
            if (run is not null && run.Name == cell.Name)
            {
                run.SetAttributeValue(ColumnsRepeated, (rows.Repeat(run, ColumnsRepeated) + repeat).ToString(CultureInfo.InvariantCulture));
                cell.Remove();
                continue;
            }

            cell.RemoveNodes();
            cell.RemoveAttributes();
            if (repeat > 1) cell.SetAttributeValue(ColumnsRepeated, repeat.ToString(CultureInfo.InvariantCulture));
            run = cell;
        }
    }

    private bool Plain(XElement cell) =>
        cell.Attributes().All(attribute => ValueAttributes.Contains(attribute.Name))
        && cell.Elements().All(child => child.Name == Paragraph);
}
