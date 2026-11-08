namespace PulseOffice.UI.Commands.Shortcuts;

public interface IShortcutSearch
{
    bool Matches(ShortcutEntry entry, string? gesture, string? query);

    bool Matches(string? gesture, GestureChord chord);
}

public sealed class ShortcutSearch(IGestureNotation notation) : IShortcutSearch
{
    private readonly GestureChordParser _parser = new();

    public bool Matches(ShortcutEntry entry, string? gesture, string? query)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(query)) return true;

        var shown = gesture is null ? string.Empty : notation.Display(gesture);

        if (shown.Length > 0 && shown.Equals(query.Trim(), StringComparison.CurrentCultureIgnoreCase)) return true;

        var haystack = string.Join(' ', [entry.Title, entry.Trail ?? string.Empty, entry.Id, .. entry.Keywords]);

        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(term => haystack.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || shown.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || (gesture?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    public bool Matches(string? gesture, GestureChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return _parser.Parse(gesture) is { } parsed
            && string.Equals(parsed.Canonical(), chord.Canonical(), StringComparison.OrdinalIgnoreCase);
    }
}
