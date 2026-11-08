namespace PulseOffice.Document.Commands;

public sealed class UndoStack
{
    private readonly List<IUndoableAction> _done = [];
    private readonly List<IUndoableAction> _undone = [];
    private readonly int _capacity;

    private List<IUndoableAction>? _transaction;
    private string? _transactionDescription;
    private IUndoMergeKey? _transactionKey;
    private bool _transactionLabelled;
    private object? _transactionOwner;
    private readonly AsyncLocal<object?> _flowOwner = new();

    public UndoStack(int capacity = 200)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public bool CanUndo => _done.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    public object? StatePoint => _done.Count > 0 ? _done[^1] : _base;

    private object? _base;

    public string? UndoDescription => _done.Count > 0 ? _done[^1].Description : null;
    public string? RedoDescription => _undone.Count > 0 ? _undone[^1].Description : null;

    public event EventHandler? Changed;

    public event EventHandler? ChangedOutsideHistory;

    public void ApplyOutsideHistory(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);

        change();

        ChangedOutsideHistory?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Push(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_transaction is not null && (_transactionOwner is null || ReferenceEquals(_flowOwner.Value, _transactionOwner)))
        {
            _transaction.Add(action);
            return;
        }

        _undone.Clear();

        if (_done.Count > 0 && action.TryMergeWith(_done[^1]))
        {
            _done[^1] = action;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _done.Add(action);
        if (_done.Count > _capacity)
        {
            _base = _done[0];
            _done.RemoveAt(0);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_done.Count == 0 || _transaction is not null) return;

        var action = _done[^1];
        action.Undo();

        _done.RemoveAt(_done.Count - 1);
        _undone.Add(action);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_undone.Count == 0 || _transaction is not null) return;

        var action = _undone[^1];
        action.Redo();

        _undone.RemoveAt(_undone.Count - 1);
        _done.Add(action);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IDisposable BeginTransaction(string description) => BeginTransaction(description, null);

    public IDisposable BeginTransaction(string description, IUndoMergeKey? mergeKey) => Begin(description, mergeKey, labelled: false);

    public IDisposable BeginLabelledStep(string description) => Begin(description, null, labelled: true);

    private IDisposable Begin(string description, IUndoMergeKey? mergeKey, bool labelled)
    {
        if (_transaction is not null) return new NestedScope();

        _transaction = [];
        _transactionDescription = description;
        _transactionKey = mergeKey;
        _transactionLabelled = labelled;
        _transactionOwner = labelled ? new object() : null;
        _flowOwner.Value = _transactionOwner;

        return new TransactionScope(this);
    }

    private void EndTransaction()
    {
        var actions = _transaction;
        var description = _transactionDescription;
        var key = _transactionKey;
        var labelled = _transactionLabelled;

        _transaction = null;
        _transactionDescription = null;
        _transactionKey = null;
        _transactionLabelled = false;
        _transactionOwner = null;
        _flowOwner.Value = null;

        if (actions is null || actions.Count == 0) return;

        if (key is not null) Push(new MergeableAction(description ?? string.Empty, actions, key));
        else Push(actions.Count == 1 && !labelled
            ? actions[0]
            : new CompositeAction(description ?? string.Empty, actions));
    }

    public void Clear()
    {
        _base = _done.Count > 0 ? new object() : _base;
        _done.Clear();
        _undone.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TransactionScope(UndoStack stack) : IDisposable
    {
        public void Dispose() => stack.EndTransaction();
    }

    private sealed class NestedScope : IDisposable
    {
        public void Dispose() { }
    }
}
