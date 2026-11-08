namespace PulseOffice.Presentations;

public interface ICustomShowEditing
{
    IReadOnlyList<string> Insert(IReadOnlyList<string> show, IReadOnlyList<string> slides, int at);

    IReadOnlyList<string> Move(IReadOnlyList<string> show, int from, int to);

    IReadOnlyList<string> RemoveAt(IReadOnlyList<string> show, int index);
}

public sealed class CustomShowEditing : ICustomShowEditing
{
    public IReadOnlyList<string> Insert(IReadOnlyList<string> show, IReadOnlyList<string> slides, int at)
    {
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(slides);
        var list = show.ToList();
        list.InsertRange(Math.Clamp(at, 0, list.Count), slides);
        return list;
    }

    public IReadOnlyList<string> Move(IReadOnlyList<string> show, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(show);
        if (from < 0 || from >= show.Count) return show;
        var list = show.ToList();
        var moved = list[from];
        list.RemoveAt(from);
        list.Insert(Math.Clamp(to > from ? to - 1 : to, 0, list.Count), moved);
        return list;
    }

    public IReadOnlyList<string> RemoveAt(IReadOnlyList<string> show, int index)
    {
        ArgumentNullException.ThrowIfNull(show);
        if (index < 0 || index >= show.Count) return show;
        var list = show.ToList();
        list.RemoveAt(index);
        return list;
    }
}
