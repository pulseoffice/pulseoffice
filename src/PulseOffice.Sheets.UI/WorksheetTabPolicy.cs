namespace PulseOffice.Sheets.UI;

public interface IWorksheetTabPolicy
{
    bool CanDelete(Workbook workbook, int index);

    bool CanHide(Workbook workbook, int index);

    bool NameTaken(Workbook workbook, Worksheet renamed, string name);

    IReadOnlyList<char> ForbiddenNameCharacters { get; }

    int MaxNameLength { get; }

    bool NameAllowed(string name);

    string UniqueName(Workbook workbook, string name, bool keepWhenFree = true);

    int VisibleNear(Workbook workbook, int index);

    int VisibleAfterRemoval(Workbook workbook, int removedIndex);
}

public sealed class WorksheetTabPolicy : IWorksheetTabPolicy
{
    public bool CanDelete(Workbook workbook, int index) =>
        InRange(workbook, index)
        && workbook.Sheets.Count > 1
        && (workbook.Sheets[index].IsHidden || VisibleCount(workbook) > 1);

    public bool CanHide(Workbook workbook, int index) =>
        InRange(workbook, index)
        && !workbook.Sheets[index].IsHidden
        && VisibleCount(workbook) > 1;

    public bool NameTaken(Workbook workbook, Worksheet renamed, string name) =>
        workbook.FindSheet(name) is { } found && !ReferenceEquals(found, renamed);

    private readonly char[] _forbidden = ['[', ']', '*', '?', ':', '/', '\\'];

    public IReadOnlyList<char> ForbiddenNameCharacters => _forbidden;

    public int MaxNameLength => 31;

    public bool NameAllowed(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        return trimmed.Length <= MaxNameLength
            && trimmed.IndexOfAny(_forbidden) < 0
            && !trimmed.StartsWith('\'')
            && !trimmed.EndsWith('\'');
    }

    public string UniqueName(Workbook workbook, string name, bool keepWhenFree = true)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var taken = workbook.Sheets.Select(sheet => sheet.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var stem = Fit(name.Trim(), MaxNameLength);

        if (keepWhenFree && stem.Length > 0 && !taken.Contains(stem)) return stem;

        for (var index = 2; ; index++)
        {
            var suffix = $" ({index})";
            var candidate = Fit(stem, MaxNameLength - suffix.Length) + suffix;
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    private static string Fit(string text, int length)
    {
        if (text.Length <= length) return text;
        var cut = length;
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1])) cut--;
        return text[..cut].TrimEnd();
    }

    public int VisibleNear(Workbook workbook, int index)
    {
        for (var candidate = index + 1; candidate < workbook.Sheets.Count; candidate++)
            if (!workbook.Sheets[candidate].IsHidden) return candidate;

        for (var candidate = index - 1; candidate >= 0; candidate--)
            if (!workbook.Sheets[candidate].IsHidden) return candidate;

        return Math.Clamp(index, 0, Math.Max(0, workbook.Sheets.Count - 1));
    }

    public int VisibleAfterRemoval(Workbook workbook, int removedIndex)
    {
        for (var candidate = removedIndex - 1; candidate >= 0; candidate--)
            if (!workbook.Sheets[candidate].IsHidden) return candidate;

        for (var candidate = removedIndex + 1; candidate < workbook.Sheets.Count; candidate++)
            if (!workbook.Sheets[candidate].IsHidden) return candidate - 1;

        return 0;
    }

    private static bool InRange(Workbook workbook, int index) => index >= 0 && index < workbook.Sheets.Count;

    private static int VisibleCount(Workbook workbook) => workbook.Sheets.Count(sheet => !sheet.IsHidden);
}
