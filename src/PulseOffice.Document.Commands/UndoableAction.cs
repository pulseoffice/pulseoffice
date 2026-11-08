namespace PulseOffice.Document.Commands;

public interface IUndoableAction
{
    string Description { get; }

    void Redo();
    void Undo();

    bool TryMergeWith(IUndoableAction previous) => false;
}

public sealed class DelegateAction(string description, Action redo, Action undo) : IUndoableAction
{
    public string Description { get; } = description;

    public void Redo() => redo();
    public void Undo() => undo();
}

public sealed class CompositeAction(string description, IReadOnlyList<IUndoableAction> actions)
    : IUndoableAction
{
    public string Description { get; } = description;

    public void Redo()
    {
        foreach (var action in actions) action.Redo();
    }

    public void Undo()
    {
        for (var i = actions.Count - 1; i >= 0; i--) actions[i].Undo();
    }
}

public interface IUndoMergeKey
{
    bool Continues(IUndoMergeKey previous);

    IUndoMergeKey Join(IUndoMergeKey previous);
}

public sealed class MergeableAction(string description, IReadOnlyList<IUndoableAction> actions, IUndoMergeKey key) : IUndoableAction
{
    public string Description { get; } = description;

    public IUndoMergeKey Key { get; private set; } = key;

    private IReadOnlyList<IUndoableAction> _actions = actions;

    public void Redo()
    {
        foreach (var action in _actions) action.Redo();
    }

    public void Undo()
    {
        for (var i = _actions.Count - 1; i >= 0; i--) _actions[i].Undo();
    }

    public bool TryMergeWith(IUndoableAction previous)
    {
        if (previous is not MergeableAction earlier || !Key.Continues(earlier.Key)) return false;
        _actions = [.. earlier._actions, .. _actions];
        Key = Key.Join(earlier.Key);
        return true;
    }
}
