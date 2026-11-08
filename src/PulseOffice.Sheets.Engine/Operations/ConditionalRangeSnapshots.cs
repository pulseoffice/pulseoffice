using System.Globalization;
using System.Runtime.CompilerServices;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class ConditionalRangeSnapshot
{
    public ConditionalRangeSnapshot(IReadOnlyList<CellValue> values)
    {
        Values = values;
        Ascending = [.. values.Where(value => value.Kind == ValueKind.Number).Select(value => value.AsNumber()).Order()];
        Average = Ascending.Count > 0 ? Ascending.Average() : 0;
        StandardDeviation = Ascending.Count > 1
            ? Math.Sqrt(Ascending.Sum(number => (number - Average) * (number - Average)) / (Ascending.Count - 1))
            : 0;
        KeyCounts = values.GroupBy(Key).ToDictionary(group => group.Key, group => group.Count());
    }

    public IReadOnlyList<CellValue> Values { get; }

    public List<double> Ascending { get; }

    public double Average { get; }

    public double StandardDeviation { get; }

    public IReadOnlyDictionary<string, int> KeyCounts { get; }

    public int Occurrences(CellValue value) => KeyCounts.GetValueOrDefault(Key(value));

    private string Key(CellValue value) => value.Kind == ValueKind.Number
        ? value.AsNumber().ToString("R", CultureInfo.InvariantCulture)
        : value.ToString().ToUpperInvariant();
}

internal sealed class ConditionalRangeSnapshots
{
    private sealed class SheetEntries
    {
        public long Revision = -1;

        public readonly Dictionary<ConditionalRule, ConditionalRangeSnapshot> Snapshots = new(ReferenceEqualityComparer.Instance);
    }

    private readonly ConditionalWeakTable<Worksheet, SheetEntries> _sheets = [];
    private readonly Lock _gate = new();

    public ConditionalRangeSnapshot For(Worksheet sheet, ConditionalRule rule)
    {
        lock (_gate)
        {
            var entries = _sheets.GetOrCreateValue(sheet);
            if (entries.Revision != sheet.Revision || entries.Snapshots.Count > sheet.ConditionalRules.Count + 16)
            {
                entries.Snapshots.Clear();
                entries.Revision = sheet.Revision;
            }
            if (entries.Snapshots.TryGetValue(rule, out var snapshot)) return snapshot;
            snapshot = new ConditionalRangeSnapshot(Read(sheet, rule));
            entries.Snapshots[rule] = snapshot;
            return snapshot;
        }
    }

    private List<CellValue> Read(Worksheet sheet, ConditionalRule rule)
    {
        var seen = new HashSet<Addressing.CellAddress>();
        var values = new List<CellValue>();
        foreach (var range in rule.Ranges)
            foreach (var pair in sheet.NonEmptyCellsIn(range))
                if (seen.Add(pair.Key) && !pair.Value.Value.IsEmpty) values.Add(pair.Value.Value);
        return values;
    }
}
