using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IAutoInput
{
    string? Complete(Worksheet sheet, CellAddress address, string typed);

    IReadOnlyList<string> Choices(Worksheet sheet, CellAddress address);
}

public sealed class AutoInput : IAutoInput
{
    private const int Reach = 200;

    public string? Complete(Worksheet sheet, CellAddress address, string typed)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (typed.Length == 0) return null;
        if (typed.StartsWith('=')) return null;
        if (double.TryParse(typed, out _)) return null;

        string? found = null;

        foreach (var candidate in Candidates(sheet, address))
        {
            if (!candidate.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase)) continue;
            if (candidate.Length == typed.Length) continue;

            if (found is not null && !string.Equals(found, candidate, StringComparison.CurrentCulture))
                return null;

            found = candidate;
        }

        return found;
    }

    public IReadOnlyList<string> Choices(Worksheet sheet, CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return [.. Candidates(sheet, address).Order(StringComparer.CurrentCultureIgnoreCase)];
    }

    private IEnumerable<string> Candidates(Worksheet sheet, CellAddress address)
    {
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        for (var row = address.Row - 1; row >= Math.Max(0, address.Row - Reach); row--)
        {
            var cell = sheet[new CellAddress(row, address.Column)];

            if (cell.Value.Kind != ValueKind.Text || cell.Formula is not null) continue;

            var text = cell.Value.AsText();
            if (text.Length > 0 && seen.Add(text)) yield return text;
        }

        for (var row = address.Row + 1; row <= Math.Min(address.Row + Reach, CellAddress.MaxRows - 1); row++)
        {
            var cell = sheet[new CellAddress(row, address.Column)];

            if (cell.Value.Kind != ValueKind.Text || cell.Formula is not null) continue;

            var text = cell.Value.AsText();
            if (text.Length > 0 && seen.Add(text)) yield return text;
        }
    }
}
