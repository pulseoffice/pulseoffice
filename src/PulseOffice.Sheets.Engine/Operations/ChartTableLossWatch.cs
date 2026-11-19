using PulseOffice.Charts;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed class ChartTablesLostEventArgs(IReadOnlyList<string> tables) : EventArgs
{
    public IReadOnlyList<string> Tables { get; } = tables;
}

public sealed class ChartTableLossWatch
{
    private readonly Workbook _workbook;
    private readonly IChartTableSources _tables;
    private HashSet<string> _missing;

    public ChartTableLossWatch(Workbook workbook, IChartTableSources tables)
    {
        _workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _missing = Missing();
    }

    public ChartTableLossWatch(Workbook workbook) : this(workbook, new ChartTableSources()) { }

    public event EventHandler<ChartTablesLostEventArgs>? TablesLost;

    public void Check()
    {
        var missing = Missing();
        var lost = missing.Where(name => !_missing.Contains(name)).Order(StringComparer.OrdinalIgnoreCase).ToList();
        _missing = missing;
        if (lost.Count > 0) TablesLost?.Invoke(this, new ChartTablesLostEventArgs(lost));
    }

    public void AnnounceStanding()
    {
        _missing = Missing();
        if (_missing.Count > 0) TablesLost?.Invoke(this, new ChartTablesLostEventArgs(_missing.Order(StringComparer.OrdinalIgnoreCase).ToList()));
    }

    private HashSet<string> Missing() =>
        new(_workbook.Sheets
            .SelectMany(sheet => sheet.Objects)
            .Select(placed => placed.Content).OfType<ChartDefinition>()
            .SelectMany(chart => _tables.MissingTables(chart, _workbook)), StringComparer.OrdinalIgnoreCase);
}
