using System.Diagnostics;
using PulseOffice.Data;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Queries;

public interface IQueryRefresher
{
    Task<OperationResult<QueryBinding>> RefreshAsync(
        Workbook workbook, Worksheet sheet, QueryBinding binding, CancellationToken ct = default);

    Task<OperationResult<int>> RefreshAllAsync(
        Workbook workbook, CancellationToken ct = default);

    Task<QueryFetch> FetchAllAsync(Workbook workbook, CancellationToken ct = default);

    OperationResult<int> Apply(Workbook workbook, QueryFetch fetch);
}

public sealed record QueryFetched(Worksheet Sheet, QueryBinding Binding, QueryResult? Result, TimeSpan Elapsed, Diagnostic? Failure);

public sealed record QueryFetch(IReadOnlyList<QueryFetched> Entries);

public sealed class QueryRefresher : IQueryRefresher
{
    private readonly DatabaseProviderRegistry _providers;
    private readonly IConnectionDirectory _connections;
    private readonly IQueryValueConverter _values;
    private readonly IReadOnlySqlPolicy _readOnly;
    private readonly IExternalQueryGate _gate;

    public const string DeclinedCode = "QRY006";

    public QueryRefresher(
        DatabaseProviderRegistry providers,
        IConnectionDirectory connections,
        IExternalQueryGate gate,
        Func<DateTime, double>? toSerial = null,
        IReadOnlySqlPolicy? readOnly = null)
    {
        _readOnly = readOnly ?? new ReadOnlySqlPolicy();
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _providers = providers;
        _connections = connections;
        _values = new QueryValueConverter(toSerial);
    }

    public async Task<OperationResult<QueryBinding>> RefreshAsync(
        Workbook workbook, Worksheet sheet, QueryBinding binding, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(binding);

        var fetched = await GatedAsync(workbook, [(sheet, binding)], ct).ConfigureAwait(false);
        return Written(workbook, fetched[0]);
    }

    public async Task<OperationResult<int>> RefreshAllAsync(
        Workbook workbook, CancellationToken ct = default) =>
        Apply(workbook, await FetchAllAsync(workbook, ct).ConfigureAwait(false));

    public async Task<QueryFetch> FetchAllAsync(Workbook workbook, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var bindings = workbook.Sheets.ToList()
            .SelectMany(sheet => sheet.QueryBindings.All.ToList().Select(binding => (sheet, binding)))
            .ToList();

        return new QueryFetch(await GatedAsync(workbook, bindings, ct).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<QueryFetched>> GatedAsync(
        Workbook workbook, IReadOnlyList<(Worksheet Sheet, QueryBinding Binding)> bindings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var requests = bindings.Select(entry => Request(entry.Binding)).OfType<ExternalQueryRequest>().ToList();
        if (requests.Count > 0 && !await _gate.AllowAsync(workbook, requests, ct).ConfigureAwait(false))
            return [.. bindings.Select(entry => Failure(entry.Sheet, entry.Binding,
                Diagnostic.Warning(DeclinedCode, $"The query '{entry.Binding.DisplayName}' was not run: the user did not allow the document's queries.").With(entry.Binding.DisplayName)))];

        var entries = new List<QueryFetched>(bindings.Count);
        foreach (var (sheet, binding) in bindings)
            entries.Add(await FetchAsync(sheet, binding, ct).ConfigureAwait(false));
        return entries;
    }

    private ExternalQueryRequest? Request(QueryBinding binding) =>
        _connections.Find(binding.ConnectionId) is { } connection
            && _providers.IsAvailable(connection.Provider)
            && _readOnly.IsReadOnly(binding.Query.Sql)
            ? new ExternalQueryRequest(connection.Id, binding.Query.Sql)
            : null;

    public OperationResult<int> Apply(Workbook workbook, QueryFetch fetch)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(fetch);

        var refreshed = 0;
        var diagnostics = new List<Diagnostic>();

        foreach (var entry in fetch.Entries)
        {
            if (!workbook.Sheets.Contains(entry.Sheet) || entry.Sheet.QueryBindings.Find(entry.Binding.Id) is not { } current) continue;

            if (entry.Failure is { Code: DeclinedCode } declined)
            {
                diagnostics.Add(declined);
                continue;
            }

            var outcome = Written(workbook, entry with { Binding = current });

            if (outcome.Value is { } updated) entry.Sheet.QueryBindings.AddOrUpdate(updated);
            else entry.Sheet.QueryBindings.AddOrUpdate(current with
            {
                LastError = outcome.Diagnostics.FirstOrDefault()?.Message,
            });

            if (outcome.Succeeded) refreshed++;
            diagnostics.AddRange(outcome.Diagnostics);
        }

        return diagnostics.Count == 0
            ? OperationResult<int>.Ok(refreshed)
            : OperationResult<int>.Ok(refreshed, diagnostics);
    }

    private async Task<QueryFetched> FetchAsync(Worksheet sheet, QueryBinding binding, CancellationToken ct)
    {
        if (_connections.Find(binding.ConnectionId) is not { } connection)
            return Failure(sheet, binding, Diagnostic.Error("QRY001", $"Unknown connection '{binding.ConnectionId}'.").With(binding.ConnectionId));

        if (!_providers.IsAvailable(connection.Provider))
            return Failure(sheet, binding, Diagnostic.Error("QRY002", $"Provider '{connection.Provider}' is not available.").With(connection.Provider));

        if (!_readOnly.IsReadOnly(binding.Query.Sql))
            return Failure(sheet, binding, Diagnostic.Error("QRY004", "Only a single read query (SELECT) can be run from a document."));

        var clock = Stopwatch.StartNew();

        try
        {
            var result = await _providers.Get(connection.Provider)
                .ExecuteAsync(connection, binding.Query, ct)
                .ConfigureAwait(false);

            return new QueryFetched(sheet, binding, result, clock.Elapsed, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(sheet, binding, Diagnostic.Error("QRY003", exception.Message).With(exception.Message));
        }
    }

    private OperationResult<QueryBinding> Written(Workbook workbook, QueryFetched fetched)
    {
        if (fetched.Result is not { } result)
            return OperationResult<QueryBinding>.Fail(fetched.Failure ?? Diagnostic.Error("QRY003", "The query returned no result."));

        var (written, clipped) = Write(workbook, fetched.Sheet, fetched.Binding, result);

        return OperationResult<QueryBinding>.Ok(fetched.Binding with
        {
            Range = written,
            LastRefresh = DateTimeOffset.Now,
            LastDuration = fetched.Elapsed,
            LastRowCount = result.Rows.Count,
            LastError = null,
        }, clipped
            ? [Diagnostic.Warning("QRY005", "The query result does not fit in the sheet and was truncated.")]
            : null);
    }

    private (CellRange Range, bool Clipped) Write(Workbook workbook, Worksheet sheet, QueryBinding binding, QueryResult result)
    {
        var anchor = binding.Anchor;
        var headerRows = binding.IncludeHeaders ? 1 : 0;

        var roomRows = CellAddress.MaxRows - anchor.Row;
        var roomColumns = CellAddress.MaxColumns - anchor.Column;
        var dataColumns = Math.Min(result.Columns.Count, roomColumns);
        var dataRows = (int)Math.Clamp((long)roomRows - headerRows, 0, result.Rows.Count);
        var clipped = dataColumns < result.Columns.Count || dataRows < result.Rows.Count;

        var columns = Math.Max(1, dataColumns);
        var rows = Math.Min(dataRows + headerRows, roomRows);

        Clear(sheet, binding, anchor, rows, columns);

        if (binding.IncludeHeaders)
            for (var column = 0; column < dataColumns; column++)
                Set(sheet, binding, anchor.Offset(0, column), CellValue.Text(result.Columns[column].Name));

        for (var row = 0; row < dataRows; row++)
            for (var column = 0; column < dataColumns; column++)
            {
                var address = anchor.Offset(row + headerRows, column);
                var value = result.Rows[row][column];

                Set(sheet, binding, address, _values.Convert(value));

                if (DateFormatFor(value) is { } formatId)
                    sheet.SetStyle(address, workbook.Styles.Derive(
                        sheet.StyleIdAt(address), style => style with { NumberFormatId = formatId }));
            }

        return (new CellRange(
            anchor,
            anchor.Offset(Math.Max(0, rows - 1), Math.Max(0, columns - 1))), clipped);
    }

    private static void Clear(
        Worksheet sheet, QueryBinding binding, CellAddress anchor, int rows, int columns)
    {
        if (binding.Range.Start == binding.Range.End && binding.LastRowCount == 0) return;

        foreach (var address in binding.Range.Cells())
        {
            var insideRows = address.Row >= anchor.Row && address.Row < anchor.Row + rows;
            var insideColumns = address.Column >= anchor.Column && address.Column < anchor.Column + columns;

            if (insideRows && insideColumns) continue;

            Set(sheet, binding, address, CellValue.Empty);
        }
    }

    private static void Set(
        Worksheet sheet, QueryBinding binding, CellAddress address, CellValue value)
    {
        var previous = sheet[address];

        sheet[address] = binding.PreserveCellFormatting
            ? previous with { Value = value, Formula = null }
            : new Cell { Value = value };
    }

    private static int? DateFormatFor(object? value) => value switch
    {
        DateTime moment => moment.TimeOfDay == TimeSpan.Zero ? 14 : 22,
        DateTimeOffset moment => moment.TimeOfDay == TimeSpan.Zero ? 14 : 22,
        DateOnly => 14,
        _ => null,
    };

    private static QueryFetched Failure(Worksheet sheet, QueryBinding binding, Diagnostic diagnostic) =>
        new(sheet, binding, null, TimeSpan.Zero, diagnostic);
}
