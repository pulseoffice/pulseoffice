namespace PulseOffice.Document.Forms;

public interface ITabOrdering
{
    IReadOnlyList<FormControlProperties> Normalize(IReadOnlyList<FormControlProperties> controls);

    IReadOnlyList<FormControlProperties> Move(
        IReadOnlyList<FormControlProperties> controls, string name, int offset);

    FormControlProperties? Next(IReadOnlyList<FormControlProperties> controls, string? current, bool backwards);
}

public sealed class TabOrdering : ITabOrdering
{
    public IReadOnlyList<FormControlProperties> Normalize(IReadOnlyList<FormControlProperties> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        return [.. Ordered(controls).Select((control, index) => control with { TabIndex = index })];
    }

    public IReadOnlyList<FormControlProperties> Move(
        IReadOnlyList<FormControlProperties> controls, string name, int offset)
    {
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var ordered = Ordered(controls).ToList();
        var from = ordered.FindIndex(control => control.Name == name);

        if (from < 0) return Normalize(controls);

        var to = Math.Clamp(from + offset, 0, ordered.Count - 1);
        if (to == from) return Normalize(controls);

        var moved = ordered[from];
        ordered.RemoveAt(from);
        ordered.Insert(to, moved);

        return [.. ordered.Select((control, index) => control with { TabIndex = index })];
    }

    public FormControlProperties? Next(
        IReadOnlyList<FormControlProperties> controls, string? current, bool backwards)
    {
        ArgumentNullException.ThrowIfNull(controls);

        var reachable = Ordered(controls).Where(Reachable).ToList();
        if (reachable.Count == 0) return null;

        var index = current is null ? -1 : reachable.FindIndex(control => control.Name == current);
        var step = backwards ? -1 : 1;

        if (index < 0) return backwards ? reachable[^1] : reachable[0];

        var next = (index + step + reachable.Count) % reachable.Count;
        return reachable[next];
    }

    private static bool Reachable(FormControlProperties control) =>
        control.TabStop && control.Enabled && control.Visible;

    private static IEnumerable<FormControlProperties> Ordered(IReadOnlyList<FormControlProperties> controls) =>
        controls
            .Select((control, position) => (control, position))
            .OrderBy(entry => entry.control.TabIndex)
            .ThenBy(entry => entry.position)
            .Select(entry => entry.control);
}
