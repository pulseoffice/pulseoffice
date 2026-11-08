using PulseOffice.Document.Forms;

namespace PulseOffice.Document.Commands;

public sealed class RecordedMacroLibrary(IMacroLibrary inner, UndoStack history, string description) : IMacroLibrary
{
    public IReadOnlyList<DocumentMacro> All => inner.All;

    public DocumentMacro? Find(string name) => inner.Find(name);

    public void Store(DocumentMacro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        macro = macro with { Name = macro.Name.Trim() };

        var previous = inner.Find(macro.Name);
        if (previous == macro) return;

        inner.Store(macro);
        history.Push(new DelegateAction(
            description,
            () => inner.Store(macro),
            () => Restore(macro.Name, previous)));
    }

    public bool Remove(string name)
    {
        if (inner.Find(name) is not { } previous) return false;

        inner.Remove(name);
        history.Push(new DelegateAction(
            description,
            () => inner.Remove(name),
            () => inner.Store(previous)));
        return true;
    }

    public event EventHandler? Changed
    {
        add => inner.Changed += value;
        remove => inner.Changed -= value;
    }

    private void Restore(string name, DocumentMacro? previous)
    {
        if (previous is null) inner.Remove(name);
        else inner.Store(previous);
    }
}
