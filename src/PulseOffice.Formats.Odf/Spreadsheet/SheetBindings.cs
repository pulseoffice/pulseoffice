using System.Xml.Linq;
using PulseOffice.Sheets;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfLabelRangeMapper
{
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly TokenMap<LabelOrientation> _orientations = new(LabelOrientation.Columns, "column",
        (LabelOrientation.Columns, "column"), (LabelOrientation.Rows, "row"));

    public XElement? Build(Workbook workbook)
    {
        var container = new XElement(Namespaces.Table + "label-ranges",
            workbook.Sheets.SelectMany(sheet => sheet.Labels.All.Select(label => new XElement(Namespaces.Table + "label-range",
                new XAttribute(Namespaces.Table + "label-cell-range-address", _ranges.Write(sheet.Name, label.Labels)),
                new XAttribute(Namespaces.Table + "data-cell-range-address", _ranges.Write(sheet.Name, label.Data)),
                new XAttribute(Namespaces.Table + "orientation", _orientations.Token(label.Orientation))))));
        return container.HasElements ? container : null;
    }

    public void Read(XElement spreadsheet, Workbook workbook)
    {
        foreach (var element in spreadsheet.Elements(Namespaces.Table + "label-ranges").Elements(Namespaces.Table + "label-range"))
        {
            if (_ranges.Read((string?)element.Attribute(Namespaces.Table + "label-cell-range-address")) is not { } labels) continue;
            if (_ranges.Read((string?)element.Attribute(Namespaces.Table + "data-cell-range-address")) is not { } data) continue;
            if (workbook.FindSheet(labels.Sheet) is not { } sheet) continue;
            sheet.Labels.Add(new LabelRange(labels.Range, data.Range, _orientations.Value((string?)element.Attribute(Namespaces.Table + "orientation"))));
        }
    }

}

internal sealed class OdfSheetEventMapper
{
    public static readonly XNamespace OfficeExtension = "http://openoffice.org/2009/office";
    private const string Prefix = "PulseOffice.";

    private readonly TokenMap<SheetEventKind> _events = new(SheetEventKind.ContentChanged, "office:content-changed",
        (SheetEventKind.Activated, "dom:DOMFocusIn"),
        (SheetEventKind.Deactivated, "dom:DOMFocusOut"),
        (SheetEventKind.SelectionChanged, "dom:select"),
        (SheetEventKind.DoubleClicked, "office:dblclick"),
        (SheetEventKind.RightClicked, "office:contextmenu"),
        (SheetEventKind.ContentChanged, "office:content-changed"),
        (SheetEventKind.Calculated, "office:calculated"));

    public XElement? Build(Worksheet sheet) =>
        sheet.Events.All.Count == 0
            ? null
            : new XElement(OfficeExtension + "event-listeners",
                sheet.Events.All.OrderBy(pair => pair.Key).Select(pair => new XElement(Namespaces.Script + "event-listener",
                    new XAttribute(Namespaces.Script + "language", "ooo:script"),
                    new XAttribute(Namespaces.Script + "event-name", _events.Token(pair.Key)),
                    new XAttribute(Namespaces.Xlink + "href", $"vnd.sun.star.script:{Prefix}{pair.Value}?language=PulseOffice&location=document"),
                    new XAttribute(Namespaces.Xlink + "type", "simple"))));

    public void Read(XElement table, Worksheet sheet)
    {
        var listeners = table.Elements(OfficeExtension + "event-listeners")
            .Concat(table.Elements(Namespaces.Office + "event-listeners"))
            .Elements(Namespaces.Script + "event-listener");
        foreach (var listener in listeners)
        {
            if (_events.Find((string?)listener.Attribute(Namespaces.Script + "event-name")) is not { } kind) continue;
            if ((string?)listener.Attribute(Namespaces.Xlink + "href") is not { } href) continue;
            sheet.Events.Bind(kind, Macro(href));
        }
    }

    private string Macro(string href)
    {
        var body = href.StartsWith("vnd.sun.star.script:", StringComparison.Ordinal) ? href["vnd.sun.star.script:".Length..] : href;
        var question = body.IndexOf('?', StringComparison.Ordinal);
        if (question >= 0) body = body[..question];
        return body.StartsWith(Prefix, StringComparison.Ordinal) ? body[Prefix.Length..] : body;
    }
}
