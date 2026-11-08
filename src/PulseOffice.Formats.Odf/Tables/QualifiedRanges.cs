using System.Text;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfQualifiedRanges
{
    private readonly SheetNameQuoting _quoting = new();

    public string Write(string sheet, CellRange range) =>
        $"{_quoting.Qualified(sheet, range.Start.ToA1())}:{_quoting.Qualified(sheet, range.End.ToA1())}";

    public string WriteAddress(string sheet, CellAddress address) => _quoting.Qualified(sheet, address.ToA1());

    public (string Sheet, CellRange Range)? Read(string? reference)
    {
        var all = ReadAll(reference);
        return all.Count == 0 ? null : all[0];
    }

    public IReadOnlyList<(string Sheet, CellRange Range)> ReadAll(string? reference)
    {
        var result = new List<(string Sheet, CellRange Range)>();
        foreach (var token in Tokens(reference))
        {
            var parts = SplitOutsideQuotes(token, ':');
            var (sheet, startText) = Split(parts[0]);
            if (!CellAddress.TryParseA1(startText, out var start)) continue;
            if (parts.Count == 1)
            {
                result.Add((sheet, new CellRange(start, start)));
                continue;
            }

            if (CellAddress.TryParseA1(Split(parts[1]).Address, out var end)) result.Add((sheet, new CellRange(start, end)));
        }

        return result;
    }

    private IEnumerable<string> Tokens(string? reference) =>
        reference is null ? [] : SplitOutsideQuotes(reference, ' ').Where(token => token.Length > 0);

    private List<string> SplitOutsideQuotes(string text, char separator)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        foreach (var character in text)
        {
            if (character == '\'') quoted = !quoted;
            if (character == separator && !quoted)
            {
                pieces.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        pieces.Add(current.ToString());
        return pieces;
    }

    private (string Sheet, string Address) Split(string reference)
    {
        var dot = reference.LastIndexOf('.');
        var address = (dot < 0 ? reference : reference[(dot + 1)..]).Replace("$", string.Empty, StringComparison.Ordinal);
        if (dot < 0) return (string.Empty, address);

        var sheet = reference[..dot].TrimStart('$');
        if (sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\'')
            sheet = sheet[1..^1].Replace("''", "'", StringComparison.Ordinal);

        return (sheet, address);
    }
}
