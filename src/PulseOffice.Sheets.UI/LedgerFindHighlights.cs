using System.Runtime.CompilerServices;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI;

public sealed record LedgerFindQuery(string Query, SearchOptions Options, int SheetIndex);

public interface ILedgerFindHighlights
{
    void Show(Workbook workbook, LedgerFindQuery query);

    void Clear(Workbook workbook);

    IReadOnlySet<CellAddress> Found(Workbook workbook, int sheetIndex);

    event EventHandler? Changed;
}

public sealed class LedgerFindHighlights(ISheetSearch search) : ILedgerFindHighlights
{
    private static readonly IReadOnlySet<CellAddress> None = new HashSet<CellAddress>();

    private readonly ConditionalWeakTable<Workbook, Entry> _entries = new();

    public LedgerFindHighlights() : this(new SheetSearch())
    {
    }

    public event EventHandler? Changed;

    public void Show(Workbook workbook, LedgerFindQuery query)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(query);
        if (_entries.TryGetValue(workbook, out var current) && current.Query == query) return;
        _entries.AddOrUpdate(workbook, new Entry(query));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (_entries.Remove(workbook)) Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlySet<CellAddress> Found(Workbook workbook, int sheetIndex)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (!_entries.TryGetValue(workbook, out var entry)) return None;
        var query = entry.Query;
        if (!query.Options.AllSheets && sheetIndex != query.SheetIndex) return None;
        if (sheetIndex < 0 || sheetIndex >= workbook.Sheets.Count) return None;
        var sheet = workbook.Sheets[sheetIndex];
        if (entry.Sheet == sheet && entry.Revision == sheet.Revision && entry.Cells is { } cached) return cached;
        entry.Sheet = sheet;
        entry.Revision = sheet.Revision;
        entry.Cells = search.FindAll(sheet, query.Query, query.Options, styles: workbook.Styles).Select(match => match.Address).ToHashSet();
        return entry.Cells;
    }

    private sealed class Entry(LedgerFindQuery query)
    {
        public LedgerFindQuery Query { get; } = query;

        public Worksheet? Sheet { get; set; }

        public long Revision { get; set; } = -1;

        public IReadOnlySet<CellAddress>? Cells { get; set; }
    }
}
