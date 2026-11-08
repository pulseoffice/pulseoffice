using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Redaction;

namespace PulseOffice.Sheets;

public sealed record RedactionHit(CellAddress Address, string Before, string After);

public interface ISheetRedaction
{
    IReadOnlyList<RedactionPattern> Patterns { get; }

    IReadOnlyList<RedactionHit> Plan(Worksheet sheet, CellRange range);

    IReadOnlyList<RedactionHit> PlanAutomatic(
        Worksheet sheet, IEnumerable<RedactionPattern> patterns);
}

public sealed class SheetRedaction(ITextRedactor redactor) : ISheetRedaction
{
    public IReadOnlyList<RedactionPattern> Patterns => redactor.Patterns;

    public IReadOnlyList<RedactionHit> Plan(Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var hits = new List<RedactionHit>();

        foreach (var (address, cell) in sheet.NonEmptyCellsIn(range))
        {
            if (cell.Value.IsEmpty && !cell.HasFormula) continue;

            var before = cell.HasFormula ? cell.Formula! : Text(cell.Value);

            hits.Add(new RedactionHit(address, before, redactor.Mask));
        }

        return hits;
    }

    public IReadOnlyList<RedactionHit> PlanAutomatic(
        Worksheet sheet, IEnumerable<RedactionPattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(patterns);

        var chosen = patterns.ToList();
        var hits = new List<RedactionHit>();

        foreach (var (address, cell) in sheet.NonEmptyCells())
        {
            if (cell.Value.Kind != ValueKind.Text) continue;

            var before = cell.Value.AsText();
            var after = redactor.Redact(before, chosen);

            if (after != before) hits.Add(new RedactionHit(address, before, after));
        }

        return hits;
    }

    private static string Text(CellValue value) => value.Kind switch
    {
        ValueKind.Text => value.AsText(),
        ValueKind.Empty => string.Empty,
        _ => value.ToString() ?? string.Empty,
    };
}
