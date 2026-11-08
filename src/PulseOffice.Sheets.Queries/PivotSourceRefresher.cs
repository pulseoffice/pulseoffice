using PulseOffice.Data;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Queries;

public sealed record PivotSourceFetched(string PivotId, PivotRecordCache? Cache, Diagnostic? Failure);

public sealed record PivotSourceFetch(IReadOnlyList<PivotSourceFetched> Entries)
{
    public int Refreshed => Entries.Count(entry => entry.Cache is not null);

    public IEnumerable<Diagnostic> Failures => Entries.Select(entry => entry.Failure).OfType<Diagnostic>();
}

public interface IPivotSourceRefresher
{
    bool HasExternal(Workbook? workbook);

    Task<PivotSourceFetch> FetchAsync(Workbook workbook, CancellationToken ct = default);

    int Apply(Workbook workbook, PivotSourceFetch fetch);
}

public sealed class PivotSourceRefresher(
    DatabaseProviderRegistry providers,
    IConnectionDirectory connections,
    IExternalQueryGate gate,
    IQueryValueConverter? values = null,
    IReadOnlySqlPolicy? readOnly = null) : IPivotSourceRefresher
{
    private readonly IExternalQueryGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly IQueryValueConverter _values = values ?? new QueryValueConverter();
    private readonly IReadOnlySqlPolicy _readOnly = readOnly ?? new ReadOnlySqlPolicy();

    public bool HasExternal(Workbook? workbook) => workbook?.Pivots.Any(pivot => pivot.IsExternal) == true;

    public async Task<PivotSourceFetch> FetchAsync(Workbook workbook, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var external = workbook.Pivots.ToList()
            .Where(pivot => pivot.ExternalSource is not null)
            .Select(pivot => (Pivot: pivot, Source: pivot.ExternalSource!))
            .ToList();

        var requests = external.Select(entry => Plan(entry.Source)).OfType<PivotQueryPlan>()
            .Select(plan => new ExternalQueryRequest(plan.Connection.Id, plan.Sql)).ToList();
        if (requests.Count > 0 && !await _gate.AllowAsync(workbook, requests, ct).ConfigureAwait(false))
            return new PivotSourceFetch([.. external.Select(entry => Failure(entry.Pivot,
                Diagnostic.Warning("PVT015", $"The source of pivot table '{entry.Pivot.Name}' was not queried: the user did not allow the document's queries.", entry.Pivot.Name).With(entry.Pivot.Name)))]);

        var entries = new List<PivotSourceFetched>();
        foreach (var (pivot, source) in external)
            entries.Add(await FetchAsync(pivot, source, ct).ConfigureAwait(false));
        return new PivotSourceFetch(entries);
    }

    public int Apply(Workbook workbook, PivotSourceFetch fetch)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(fetch);

        var applied = 0;
        foreach (var entry in fetch.Entries)
        {
            if (entry.Cache is not { } cache) continue;
            var index = workbook.Pivots.FindIndex(pivot => pivot.Id == entry.PivotId);
            if (index < 0 || workbook.Pivots[index].ExternalSource is not { } source) continue;
            workbook.Pivots[index] = workbook.Pivots[index] with
            {
                ExternalSource = source with { Cache = cache },
                Fields = Rebound(workbook.Pivots[index].Fields, cache.Headers),
            };
            applied++;
        }
        return applied;
    }

    private async Task<PivotSourceFetched> FetchAsync(PivotDefinition pivot, PivotExternalSource source, CancellationToken ct)
    {
        if (!source.CanQuery)
            return Failure(pivot, Diagnostic.Warning("PVT010", $"The source of pivot table '{pivot.Name}' cannot be queried from here: its cached records are kept.", pivot.Name).With(pivot.Name));

        var name = source.DatabaseName ?? source.ConnectionId ?? string.Empty;
        if (Connection(source) is not { } connection)
            return Failure(pivot, Diagnostic.Warning("PVT011", $"No connection named '{name}' is configured: the cached records of pivot table '{pivot.Name}' are kept.", name).With(name, pivot.Name));

        if (!providers.IsAvailable(connection.Provider))
            return Failure(pivot, Diagnostic.Warning("PVT012", $"Provider '{connection.Provider}' is not available: the cached records of pivot table '{pivot.Name}' are kept.", connection.Provider.ToString()).With(connection.Provider, pivot.Name));

        var provider = providers.Get(connection.Provider);
        var sql = Sql(provider, source);

        if (!_readOnly.IsReadOnly(sql))
            return Failure(pivot, Diagnostic.Error("PVT014", $"The query of pivot table '{pivot.Name}' is not a single read query (SELECT): it is not run.", pivot.Name).With(pivot.Name));

        try
        {
            var result = await provider.ExecuteAsync(connection, new QueryDefinition { Sql = sql }, ct).ConfigureAwait(false);
            return new PivotSourceFetched(pivot.Id, Cache(result), null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(pivot, Diagnostic.Warning("PVT013", $"Pivot table '{pivot.Name}' could not reach its source ({exception.Message}): its cached records are kept.", pivot.Name).With(pivot.Name, exception.Message));
        }
    }

    private sealed record PivotQueryPlan(ConnectionDescriptor Connection, string Sql);

    private PivotQueryPlan? Plan(PivotExternalSource source)
    {
        if (!source.CanQuery || Connection(source) is not { } connection || !providers.IsAvailable(connection.Provider)) return null;
        var sql = Sql(providers.Get(connection.Provider), source);
        return _readOnly.IsReadOnly(sql) ? new PivotQueryPlan(connection, sql) : null;
    }

    private string Sql(IDatabaseProvider provider, PivotExternalSource source) =>
        source.Kind is PivotExternalKind.Table or PivotExternalKind.Query
            ? "SELECT * FROM " + provider.QuoteListedTable(source.Command!)
            : source.Command!;

    private ConnectionDescriptor? Connection(PivotExternalSource source) =>
        (source.ConnectionId is { Length: > 0 } id ? connections.Find(id) : null)
        ?? connections.All.FirstOrDefault(candidate => source.DatabaseName is { Length: > 0 } name
            && (string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Id, name, StringComparison.OrdinalIgnoreCase)));

    private PivotRecordCache Cache(QueryResult result) => new()
    {
        Headers = [.. result.Columns.Select(column => column.Name)],
        Records = [.. result.Rows.Select(row => (IReadOnlyList<CellValue>)[.. row.Select(_values.Convert)])],
    };

    private IReadOnlyList<PivotField> Rebound(IReadOnlyList<PivotField> fields, IReadOnlyList<string> headers) =>
        [.. fields.Select(field => IndexOf(headers, field.SourceName) is var at and >= 0 ? field with { SourceColumnIndex = at } : field)];

    private int IndexOf(IReadOnlyList<string> headers, string name)
    {
        for (var index = 0; index < headers.Count; index++)
            if (string.Equals(headers[index], name, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }

    private PivotSourceFetched Failure(PivotDefinition pivot, Diagnostic diagnostic) => new(pivot.Id, null, diagnostic);
}
