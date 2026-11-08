using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public enum PivotExternalKind { Sql, Table, Query, Service, Workbook }

public sealed record PivotRecordCache
{
    public static readonly PivotRecordCache Empty = new();

    public IReadOnlyList<string> Headers { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<CellValue>> Records { get; init; } = [];

    public bool HasRecords => Headers.Count > 0 && Records.Count > 0;

    public bool Equals(PivotRecordCache? other) =>
        other is not null
        && Headers.SequenceEqual(other.Headers, StringComparer.Ordinal)
        && Records.Count == other.Records.Count
        && Records.Zip(other.Records).All(pair => pair.First.SequenceEqual(pair.Second));

    public override int GetHashCode() => HashCode.Combine(Headers.Count, Records.Count);
}

public sealed record PivotExternalSource
{
    public required PivotExternalKind Kind { get; init; }

    public string? DatabaseName { get; init; }

    public string? Command { get; init; }

    public bool ParseSql { get; init; } = true;

    public string? ServiceName { get; init; }

    public string? ServiceSourceName { get; init; }

    public string? ServiceObjectName { get; init; }

    public string? ServiceUserName { get; init; }

    public string? ConnectionId { get; init; }

    public PivotRecordCache Cache { get; init; } = PivotRecordCache.Empty;

    public bool CanQuery => Kind is PivotExternalKind.Sql or PivotExternalKind.Table or PivotExternalKind.Query && Command is { Length: > 0 };
}
