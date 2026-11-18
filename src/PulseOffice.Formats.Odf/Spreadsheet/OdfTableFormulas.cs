using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfTableFormulas(ITableReferenceFlattening flattening)
{
    private readonly XName _structured = XNamespace.Get("urn:pulseoffice:spreadsheet") + "table-formula";

    public OdfTableFormulas() : this(new TableReferenceFlattening()) { }

    public (string Formula, XAttribute? Structured) Write(string formula, Workbook workbook, SheetCell at)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var flattened = flattening.Flatten(formula, workbook, at, _ => false);
        return string.Equals(flattened, formula, StringComparison.Ordinal) ? (formula, null) : (flattened, new XAttribute(_structured, formula));
    }

    public string? Read(XElement cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return (string?)cell.Attribute(_structured) is { Length: > 0 } formula ? formula : null;
    }
}
