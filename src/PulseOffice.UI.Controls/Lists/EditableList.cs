namespace PulseOffice.UI.Controls.Lists;

public enum ListEditMode { Editable, ReorderOnly }

public sealed class EditableList
{
    private readonly List<string> _items;
    private readonly StringComparer _comparer;
    private readonly Func<string, bool>? _accepts;

    public EditableList(
        IEnumerable<string> items, bool unique = false, StringComparer? comparer = null, Func<string, bool>? accepts = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        Unique = unique;
        _comparer = comparer ?? StringComparer.CurrentCulture;
        _accepts = accepts;
        _items = [];
        foreach (var item in items) Add(item);
    }

    public bool Unique { get; }

    public IReadOnlyList<string> Items => _items;

    public int Count => _items.Count;

    public event EventHandler? Changed;

    public bool Accepts(string value) => _accepts?.Invoke(value) ?? true;

    public bool CanAdd(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        return value.Length > 0 && Accepts(value) && !(Unique && _items.Contains(value, _comparer));
    }

    public bool Add(string? text)
    {
        if (!CanAdd(text)) return false;
        _items.Add(text!.Trim());
        Raise();
        return true;
    }

    public bool Replace(int index, string? text)
    {
        if (!Valid(index)) return false;
        var value = text?.Trim() ?? string.Empty;
        if (value.Length == 0 || !Accepts(value)) return false;
        if (Unique && _items.Where((_, other) => other != index).Contains(value, _comparer)) return false;
        if (_items[index] == value) return true;
        _items[index] = value;
        Raise();
        return true;
    }

    public bool CanMoveUp(int index) => Valid(index) && index > 0;

    public bool CanMoveDown(int index) => Valid(index) && index < _items.Count - 1;

    public bool MoveUp(int index) => CanMoveUp(index) && Swap(index, index - 1);

    public bool MoveDown(int index) => CanMoveDown(index) && Swap(index, index + 1);

    public bool Remove(int index)
    {
        if (!Valid(index)) return false;
        _items.RemoveAt(index);
        Raise();
        return true;
    }

    private bool Swap(int first, int second)
    {
        (_items[first], _items[second]) = (_items[second], _items[first]);
        Raise();
        return true;
    }

    private bool Valid(int index) => index >= 0 && index < _items.Count;

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
