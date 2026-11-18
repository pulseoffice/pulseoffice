using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfTableFormulas(ITableReferenceFlattening flattening)
{
    private readonly XName _structured = XNamespace.Get("urn:pulseoffice:spreadsheet") + "table-formula";

    private readonly XName _structuredSecond = XNamespace.Get("urn:pulseoffice:spreadsheet") + "table-second-formula";

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

    public string? ReadSecond(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.Attribute(_structuredSecond) is { Length: > 0 } formula ? formula : null;
    }

    public string? FlattenText(string? formula, Workbook workbook, SheetCell at)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (string.IsNullOrEmpty(formula) || !formula.Contains('[', StringComparison.Ordinal)) return formula;
        var prefixed = formula.StartsWith('=');
        var flattened = flattening.Flatten(prefixed ? formula : "=" + formula, workbook, at, _ => false);
        return prefixed ? flattened : flattened[1..];
    }

    public IEnumerable<XAttribute> Kept(string? first, string? flattenedFirst, string? second = null, string? flattenedSecond = null)
    {
        if (!string.Equals(first, flattenedFirst, StringComparison.Ordinal)) yield return new XAttribute(_structured, first!);
        if (!string.Equals(second, flattenedSecond, StringComparison.Ordinal)) yield return new XAttribute(_structuredSecond, second!);
    }
}
