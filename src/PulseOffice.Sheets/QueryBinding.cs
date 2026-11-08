using PulseOffice.Data;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum RefreshPolicy
{
    Manual,
    OnOpen,
    Periodic,
}

public enum RowShiftBehavior
{
    InsertDelete,
    Overwrite,
    Clip,
}

public sealed record QueryBinding
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string ConnectionId { get; init; }
    public required QueryDefinition Query { get; init; }

    public required CellAddress Anchor { get; init; }

    public CellRange Range { get; init; }

    public bool IncludeHeaders { get; init; } = true;
    public RefreshPolicy Policy { get; init; } = RefreshPolicy.Manual;
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(10);
    public RowShiftBehavior RowShift { get; init; } = RowShiftBehavior.InsertDelete;

    public bool PreserveCellFormatting { get; init; } = true;

    public DateTimeOffset? LastRefresh { get; init; }
    public TimeSpan? LastDuration { get; init; }
    public int LastRowCount { get; init; }

    public string? LastError { get; init; }

    public bool IsStale(DateTimeOffset now) =>
        Policy == RefreshPolicy.Periodic &&
        (LastRefresh is null || now - LastRefresh.Value >= RefreshInterval);
}

public sealed class QueryBindingCollection
{
    private readonly Dictionary<string, QueryBinding> _bindings = new(StringComparer.Ordinal);

    public IReadOnlyCollection<QueryBinding> All => _bindings.Values;
    public int Count => _bindings.Count;

    public void AddOrUpdate(QueryBinding binding) => _bindings[binding.Id] = binding;
    public bool Remove(string id) => _bindings.Remove(id);
    public QueryBinding? Find(string id) => _bindings.TryGetValue(id, out var b) ? b : null;

    public QueryBinding? FindAt(CellAddress address) =>
    _bindings.Values.FirstOrDefault(b => b.Range.Contains(address));

    public bool IsBound(CellAddress address) => FindAt(address) is not null;

    public IEnumerable<QueryBinding> Overlapping(CellRange range) =>
    _bindings.Values.Where(b => b.Range.IntersectsWith(range));
}
