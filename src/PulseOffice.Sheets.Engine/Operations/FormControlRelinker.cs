using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class FormControlRelinker(IReferenceMap map)
{
    private readonly SheetReferenceText _references = new();

    public FormControlRelinker(StructureChange change) : this(new StructureReferenceMap(change))
    {
    }

    public void Relink(Workbook workbook)
    {
        foreach (var sheet in workbook.Sheets) Relink(sheet);
    }

    public void Relink(Worksheet sheet)
    {
        for (var index = 0; index < sheet.Objects.Count; index++)
        {
            var placed = sheet.Objects[index];
            var relinked = Relink(placed.Content, sheet.Name);
            if (!ReferenceEquals(relinked, placed.Content)) sheet.Objects[index] = placed with { Content = relinked };
        }
    }

    private EmbeddedObject Relink(EmbeddedObject content, string ownSheet)
    {
        if (content is not FormControlObject control) return content;

        var properties = control.Properties;
        var linked = Reference(properties.LinkedCell, ownSheet, single: true);
        var source = Reference(properties.SourceRange, ownSheet, single: false);

        return linked == properties.LinkedCell && source == properties.SourceRange
            ? content
            : control with { Properties = properties with { LinkedCell = linked, SourceRange = source } };
    }

    private string? Reference(string? text, string ownSheet, bool single)
    {
        if (text is null) return text;

        var reference = _references.Split(text);
        CellRange range;
        if (single && CellAddress.TryParseA1(reference.Local, out var address)) range = new CellRange(address, address);
        else if (single || !CellRange.TryParseA1(reference.Local, out range)) return text;

        if (map.Map(reference.Sheet ?? ownSheet, range) is not { } mapped) return text;
        if (mapped.Range is not { } moved) return null;

        var sheet = reference.Sheet is null && string.Equals(mapped.Sheet, ownSheet, StringComparison.CurrentCultureIgnoreCase)
            ? null
            : reference.Sheet is { } own && string.Equals(own, mapped.Sheet, StringComparison.CurrentCultureIgnoreCase) ? own : mapped.Sheet;
        return _references.Join(sheet, single ? moved.Start.ToA1() : moved.ToString());
    }
}

internal readonly record struct MappedReference(string Sheet, CellRange? Range);

internal interface IReferenceMap
{
    MappedReference? Map(string sheet, CellRange range);
}

internal sealed class StructureReferenceMap(StructureChange change) : IReferenceMap
{
    private readonly StructureShift _shift = new(change);

    public MappedReference? Map(string sheet, CellRange range) =>
        !string.Equals(sheet, change.SheetName, StringComparison.CurrentCultureIgnoreCase) || !change.InBand(range.Start) || !change.InBand(range.End)
            ? null
            : new MappedReference(sheet, _shift.Move(range));
}

internal sealed class FormControlRequalifier
{
    private readonly SheetReferenceText _references = new();

    public EmbeddedObject Carried(EmbeddedObject content, string fromSheet, string toSheet)
    {
        if (content is not FormControlObject control) return content;

        var properties = control.Properties;
        var linked = Carried(properties.LinkedCell, fromSheet, toSheet);
        var source = Carried(properties.SourceRange, fromSheet, toSheet);

        return linked == properties.LinkedCell && source == properties.SourceRange
            ? content
            : control with { Properties = properties with { LinkedCell = linked, SourceRange = source } };
    }

    private string? Carried(string? text, string fromSheet, string toSheet)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var reference = _references.Split(text);
        var sheet = reference.Sheet ?? fromSheet;
        return _references.Qualify(sheet, toSheet, reference.Local);
    }
}
