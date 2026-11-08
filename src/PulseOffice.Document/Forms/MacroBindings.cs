using System.Globalization;

namespace PulseOffice.Document.Forms;

public enum MacroBindingStatus
{
    Unassigned,
    Assigned,
    Missing,
}

public sealed record MacroEventSlot(string Key, string TitleKey, string DescriptionKey, string NameStem);

public sealed class MacroAssignments
{
    private readonly Dictionary<string, string?> _original;
    private readonly Dictionary<string, string?> _current;

    public MacroAssignments(IEnumerable<MacroEventSlot> slots, IReadOnlyDictionary<string, string?> bound)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(bound);

        _original = slots.ToDictionary(slot => slot.Key, slot => Normalize(bound.GetValueOrDefault(slot.Key)), StringComparer.Ordinal);
        _current = new Dictionary<string, string?>(_original, StringComparer.Ordinal);
    }

    public event EventHandler? Changed;

    public IReadOnlyDictionary<string, string?> Current => _current;

    public bool IsChanged => _current.Any(pair => !string.Equals(pair.Value, _original[pair.Key], StringComparison.Ordinal));

    public string? For(string key) => _current.GetValueOrDefault(key);

    public void Assign(string key, string? macro)
    {
        if (!_current.ContainsKey(key)) throw new ArgumentException($"Unknown event slot '{key}'.", nameof(key));

        var value = Normalize(macro);
        if (string.Equals(_current[key], value, StringComparison.Ordinal)) return;

        _current[key] = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear(string key) => Assign(key, null);

    public void Rename(string from, string to)
    {
        var target = Normalize(to);
        var touched = false;

        foreach (var key in _current.Keys.ToList())
        {
            if (!string.Equals(_current[key], from, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(_current[key], target, StringComparison.Ordinal)) continue;

            _current[key] = target;
            touched = true;
        }

        if (touched) Changed?.Invoke(this, EventArgs.Empty);
    }

    private string? Normalize(string? macro) => string.IsNullOrWhiteSpace(macro) ? null : macro.Trim();
}

public interface IMacroBindingRules
{
    MacroBindingStatus Status(string? macro, IReadOnlyList<DocumentMacro> library);

    string ProposeName(string stem, IReadOnlyList<DocumentMacro> library);

    IReadOnlyList<(string From, string To)> Renames(IReadOnlyList<DocumentMacro> before, IReadOnlyList<DocumentMacro> after);

    void Reconcile(MacroAssignments assignments, IReadOnlyList<DocumentMacro> before, IReadOnlyList<DocumentMacro> after);
}

public sealed class MacroBindingRules : IMacroBindingRules
{
    public MacroBindingStatus Status(string? macro, IReadOnlyList<DocumentMacro> library)
    {
        ArgumentNullException.ThrowIfNull(library);

        if (string.IsNullOrWhiteSpace(macro)) return MacroBindingStatus.Unassigned;

        return library.Any(entry => Same(entry.Name, macro)) ? MacroBindingStatus.Assigned : MacroBindingStatus.Missing;
    }

    public string ProposeName(string stem, IReadOnlyList<DocumentMacro> library)
    {
        ArgumentNullException.ThrowIfNull(library);

        var root = string.IsNullOrWhiteSpace(stem) ? "Macro" : stem.Trim();

        if (!library.Any(entry => Same(entry.Name, root))) return root;

        for (var index = 2; ; index++)
        {
            var candidate = string.Create(CultureInfo.InvariantCulture, $"{root} {index}");
            if (!library.Any(entry => Same(entry.Name, candidate))) return candidate;
        }
    }

    public IReadOnlyList<(string From, string To)> Renames(IReadOnlyList<DocumentMacro> before, IReadOnlyList<DocumentMacro> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var gone = before.Where(old => !after.Any(now => Same(now.Name, old.Name))).ToList();
        var added = after.Where(now => !before.Any(old => Same(old.Name, now.Name))).ToList();

        List<(string, string)> renames = [];

        foreach (var old in before)
        {
            var now = after.FirstOrDefault(entry => Same(entry.Name, old.Name));
            if (now is not null && !string.Equals(now.Name, old.Name, StringComparison.Ordinal)) renames.Add((old.Name, now.Name));
        }

        foreach (var old in gone)
        {
            var matches = added.Where(now => now.Language == old.Language && now.Source == old.Source).ToList();
            if (matches.Count != 1) continue;

            var twins = gone.Count(other => other.Language == old.Language && other.Source == old.Source);
            if (twins != 1) continue;

            renames.Add((old.Name, matches[0].Name));
        }

        return renames;
    }

    public void Reconcile(MacroAssignments assignments, IReadOnlyList<DocumentMacro> before, IReadOnlyList<DocumentMacro> after)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        foreach (var (from, to) in Renames(before, after)) assignments.Rename(from, to);
    }

    private bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
