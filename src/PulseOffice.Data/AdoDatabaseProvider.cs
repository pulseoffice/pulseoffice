using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace PulseOffice.Data;

public abstract class AdoDatabaseProvider(ICredentialStore credentials) : IDatabaseProvider
{
    protected ICredentialStore Credentials { get; } = credentials;

    public abstract DatabaseProviderKind Kind { get; }
    public abstract string DisplayName { get; }
    public abstract string QuoteIdentifier(string identifier);

    protected abstract DbConnection CreateConnection(string connectionString);

    protected abstract string ApplyCredentials(string connectionString, string? userName, string? secret);

    protected abstract string ListTablesSql { get; }

    protected virtual string ReadOnlyConnectionString(string connectionString) => connectionString;

    protected abstract Task<DbTransaction> BeginReadOnlyAsync(DbConnection connection, CancellationToken ct);

    protected virtual bool ListsSchemaQualifiedTables => true;

    public string QuoteListedTable(string listedTable)
    {
        var dot = ListsSchemaQualifiedTables ? listedTable.IndexOf('.', StringComparison.Ordinal) : -1;
        return dot <= 0
            ? QuoteIdentifier(listedTable)
            : QuoteIdentifier(listedTable[..dot]) + "." + QuoteIdentifier(listedTable[(dot + 1)..]);
    }

    public async Task<bool> TestConnectionAsync(ConnectionDescriptor connection, CancellationToken ct)
    {
        try
        {
            await using var db = await OpenAsync(connection, ct);
            return db.State == ConnectionState.Open;
        }
        catch (Exception ex) when (ex is DbException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<QueryResult> ExecuteAsync(
        ConnectionDescriptor connection, QueryDefinition query, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var db = await OpenAsync(connection, ct);
        await using var transaction = await BeginReadOnlyAsync(db, ct);
        try
        {
            return await ReadAsync(db, transaction, connection, query, stopwatch, ct);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static async Task<QueryResult> ReadAsync(
        DbConnection db, DbTransaction transaction, ConnectionDescriptor connection, QueryDefinition query,
        Stopwatch stopwatch, CancellationToken ct)
    {
        await using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = query.Sql;
        command.CommandTimeout = (int)connection.CommandTimeout.TotalSeconds;
        foreach (var (name, value) in query.Parameters)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);

        var columns = new List<ResultColumn>(reader.FieldCount);
        var schema = await reader.GetSchemaTableAsync(ct);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var allowsNull = schema?.Rows[i]["AllowDBNull"] is bool b && b;
            columns.Add(new ResultColumn(reader.GetName(i), reader.GetFieldType(i), allowsNull));
        }

        var rows = new List<object?[]>();
        var truncated = false;
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= query.MaxRows)
            {
                truncated = true;
                break;
            }
            var values = new object?[reader.FieldCount];
            reader.GetValues(values!);
            for (var i = 0; i < values.Length; i++)
                if (values[i] is DBNull) values[i] = null;
            rows.Add(values);
        }

        return new QueryResult(columns, rows, stopwatch.Elapsed, truncated);
    }

    public virtual async Task<IReadOnlyList<string>> ListTablesAsync(ConnectionDescriptor connection, CancellationToken ct)
    {
        var result = await ExecuteAsync(connection, new QueryDefinition { Sql = ListTablesSql, MaxRows = 10_000 }, ct);
        return [.. result.Rows.Select(r => r[0]?.ToString() ?? string.Empty).Where(s => s.Length > 0)];
    }

    protected async Task<string?> SecretAsync(ConnectionDescriptor connection, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.InsecureSecret is { Length: > 0 } inFile
            ? inFile
            : connection.CredentialKey is null
                ? null
                : await Credentials.GetSecretAsync(connection.CredentialKey, ct);
    }

    private async Task<DbConnection> OpenAsync(ConnectionDescriptor connection, CancellationToken ct)
    {
        var secret = await SecretAsync(connection, ct);

        var db = CreateConnection(ReadOnlyConnectionString(
            ApplyCredentials(connection.ConnectionString, connection.UserName, secret)));
        try
        {
            await db.OpenAsync(ct);
            return db;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }
}
