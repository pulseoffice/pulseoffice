using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed record XlsxDataTableKey(bool TwoDimensional, bool RowInput, string First, string? Second, int Top, int Left);

internal sealed class XlsxDataTablePlan(IReadOnlyDictionary<CellAddress, CellFormula?> cells)
{
    public bool Covers(CellAddress address, out CellFormula? formula) => cells.TryGetValue(address, out formula);
}

internal sealed class XlsxDataTableWriter(XlsxFormulaSyntax formulas)
{
    private readonly Regex _operation = new(
        @"^MULTIPLE\.OPERATIONS\((?<a>[^,()!]+),(?<b>[^,()!]+),(?<c>[^,()!]+)(?:,(?<d>[^,()!]+),(?<e>[^,()!]+))?\)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    public XlsxDataTablePlan Plan(Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var keyed = new Dictionary<XlsxDataTableKey, List<CellAddress>>();
        foreach (var address in sheet.CellAddresses)
            if (sheet[address].Formula is { } formula && formula.Contains("MULTIPLE.OPERATIONS", StringComparison.OrdinalIgnoreCase)
                && Key(formulas.ToOpenXml(formula).TrimStart('='), address) is { } key)
                (keyed.TryGetValue(key, out var members) ? members : keyed[key] = []).Add(address);
        if (keyed.Count == 0) return new XlsxDataTablePlan(new Dictionary<CellAddress, CellFormula?>());

        var plan = new Dictionary<CellAddress, CellFormula?>();
        foreach (var (key, members) in keyed)
        {
            var range = new CellRange(new CellAddress(key.Top, key.Left),
                new CellAddress(members.Max(member => member.Row), members.Max(member => member.Column)));
            if (members.Min(member => member.Row) != key.Top || members.Min(member => member.Column) != key.Left || members.Count != range.CellCount) continue;

            foreach (var member in members) plan[member] = null;
            plan[range.Start] = new CellFormula
            {
                FormulaType = CellFormulaValues.DataTable,
                Reference = $"{range.Start.ToA1()}:{range.End.ToA1()}",
                DataTable2D = key.TwoDimensional ? true : null,
                DataTableRow = key.RowInput ? true : null,
                R1 = key.First,
                R2 = key.Second,
            };
        }
        return new XlsxDataTablePlan(plan);
    }

    private XlsxDataTableKey? Key(string text, CellAddress cell)
    {
        var match = _operation.Match(text.Replace(" ", string.Empty, StringComparison.Ordinal));
        if (!match.Success) return null;
        if (Address(match.Groups["a"].Value) is not { } formula || Address(match.Groups["b"].Value) is not { } first
            || Address(match.Groups["c"].Value) is not { } replacement) return null;

        if (match.Groups["d"].Success)
        {
            if (Address(match.Groups["d"].Value) is not { } rowInput || Address(match.Groups["e"].Value) is not { } top) return null;
            return replacement.Row == cell.Row && replacement.Column == formula.Column && top.Column == cell.Column && top.Row == formula.Row
                ? new XlsxDataTableKey(true, false, rowInput.ToA1(), first.ToA1(), formula.Row + 1, formula.Column + 1)
                : null;
        }

        if (formula.Column == cell.Column && replacement.Row == cell.Row && formula.Row < cell.Row && replacement.Column < cell.Column)
            return new XlsxDataTableKey(false, false, first.ToA1(), null, formula.Row + 1, replacement.Column + 1);
        if (formula.Row == cell.Row && replacement.Column == cell.Column && formula.Column < cell.Column && replacement.Row < cell.Row)
            return new XlsxDataTableKey(false, true, first.ToA1(), null, replacement.Row + 1, formula.Column + 1);
        return null;
    }

    private CellAddress? Address(string text) =>
        CellAddress.TryParseA1(text.Replace("$", string.Empty, StringComparison.Ordinal), out var address) ? address : null;
}
