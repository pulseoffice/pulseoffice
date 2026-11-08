using PulseOffice.Data;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Document.Data;

public interface IDataTableLoader
{
    Task<OperationResult<DataSnapshot>> LoadAsync(DataQuerySource query, CancellationToken ct = default);

    Task<OperationResult<DataTableObject>> RefreshAsync(DataTableObject table, CancellationToken ct = default);
}

public sealed class DataTableLoader(
    DatabaseProviderRegistry providers, IConnectionDirectory connections, IReadOnlySqlPolicy readOnly) : IDataTableLoader
{
    public DataTableLoader(DatabaseProviderRegistry providers, IConnectionDirectory connections)
        : this(providers, connections, new ReadOnlySqlPolicy())
    {
    }

    public async Task<OperationResult<DataSnapshot>> LoadAsync(
        DataQuerySource query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (connections.Find(query.ConnectionId) is not { } connection)
            return OperationResult<DataSnapshot>.Fail(Diagnostic.Error("data.connection.missing", $"Connection '{query.ConnectionId}' was not found.").With(query.ConnectionId));

        if (!providers.IsAvailable(connection.Provider))
            return OperationResult<DataSnapshot>.Fail(Diagnostic.Error("data.provider.missing", $"Provider '{connection.Provider}' is not available.").With(connection.Provider));

        if (!readOnly.IsReadOnly(query.Sql))
            return OperationResult<DataSnapshot>.Fail(Diagnostic.Error("data.query.notReadOnly", "Only a single read query (SELECT) can be run from a document."));

        QueryResult result;

        try
        {
            result = await providers.Get(connection.Provider).ExecuteAsync(
                connection,
                new QueryDefinition
                {
                    Sql = query.Sql,
                    MaxRows = query.MaxRows,
                    Parameters = query.Parameters.ToDictionary(
                        pair => pair.Key, pair => (object?)pair.Value),
                },
                ct);
        }
        catch (System.Data.Common.DbException failure)
        {
            return OperationResult<DataSnapshot>.Fail(Diagnostic.Error("data.query.failed", failure.Message).With(failure.Message));
        }

        return OperationResult<DataSnapshot>.Ok(new DataSnapshot
        {
            Columns = [.. result.Columns.Select(column => new DataColumn(column.Name, KindOf(column.ClrType)))],
            Rows = [.. result.Rows.Select(row => (IReadOnlyList<object?>)row)],
            Truncated = result.Truncated,
        });
    }

    public async Task<OperationResult<DataTableObject>> RefreshAsync(
        DataTableObject table, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(table);

        var loaded = await LoadAsync(table.Query, ct);

        if (!loaded.Succeeded)
            return OperationResult<DataTableObject>.Ok(
                table with { LastError = loaded.Diagnostics.FirstOrDefault()?.Message },
                loaded.Diagnostics);

        return OperationResult<DataTableObject>.Ok(
            table with { Snapshot = loaded.Value!, LastError = null });
    }

    private static DataValueKind KindOf(Type type) =>
        Type.GetTypeCode(Nullable.GetUnderlyingType(type) ?? type) switch
        {
            TypeCode.Boolean => DataValueKind.Boolean,
            TypeCode.DateTime => DataValueKind.Date,
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => DataValueKind.Number,
            _ => type == typeof(DateTimeOffset) || type == typeof(DateOnly)
                ? DataValueKind.Date
                : DataValueKind.Text,
        };
}
