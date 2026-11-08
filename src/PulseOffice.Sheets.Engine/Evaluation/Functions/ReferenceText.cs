using System.Globalization;
using System.Text.RegularExpressions;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class ReferenceText
{
    private const int MaxNameDepth = 8;

    private readonly Regex _r1c1 = new(@"^R(\[-?\d+\]|\d+)?C(\[-?\d+\]|\d+)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private readonly Regex _wholeColumns = new(@"^\$?([A-Za-z]{1,3}):\$?([A-Za-z]{1,3})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly Regex _wholeRows = new(@"^\$?(\d+):\$?(\d+)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public RangeValue? Resolve(string text, bool a1Style, IEvaluationContext context) =>
        Resolve(text.Trim(), a1Style, context, 0);

    private RangeValue? Resolve(string text, bool a1Style, IEvaluationContext context, int depth)
    {
        if (text.Length == 0 || depth > MaxNameDepth) return null;
        if (text.StartsWith('=')) text = text[1..].Trim();

        var (sheetName, local) = SplitSheet(text);
        if (sheetName is not null && !context.SheetExists(sheetName)) return null;

        if (Area(local, a1Style, context.CurrentCell.Address) is { } range)
            return new RangeValue(sheetName, range);

        if (sheetName is null && context.ResolveName(text) is { } expression)
            return Resolve(expression.Trim(), a1Style: true, context, depth + 1);

        return null;
    }

    private (string? Sheet, string Local) SplitSheet(string text)
    {
        var separator = LastSeparatorOutsideQuotes(text);
        if (separator <= 0) return (null, text);

        var sheet = text[..separator].Trim().TrimStart('$');
        if (sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\'')
            sheet = sheet[1..^1].Replace("''", "'", StringComparison.Ordinal);
        return (sheet, StripRepeatedSheet(text[(separator + 1)..], text[..separator]));
    }

    private string StripRepeatedSheet(string local, string sheetPrefix)
    {
        var colon = local.IndexOf(':');
        if (colon < 0) return local;
        var second = local[(colon + 1)..];
        var prefix = sheetPrefix.Trim() + ".";
        return second.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || second.StartsWith("." , StringComparison.Ordinal)
            ? local[..(colon + 1)] + second[(second.IndexOf('.') + 1)..]
            : local;
    }

    private int LastSeparatorOutsideQuotes(string text)
    {
        var quoted = false;
        var found = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '\'') quoted = !quoted;
            else if (!quoted && character == '!') return i;
            else if (!quoted && character == '.' && found < 0 && text.IndexOf(':') is var colon && (colon < 0 || i < colon)) found = i;
        }
        return found;
    }

    private CellRange? Area(string text, bool a1Style, CellAddress origin)
    {
        var colon = text.IndexOf(':');
        if (a1Style)
        {
            if (CellRange.TryParseA1(text, out var range)) return Normalized(range);
            if (_wholeColumns.Match(text) is { Success: true } columns)
            {
                var first = CellAddress.ParseColumn(columns.Groups[1].Value);
                var last = CellAddress.ParseColumn(columns.Groups[2].Value);
                if (first < 0 || last < 0) return null;
                return Normalized(new CellRange(new CellAddress(0, first), new CellAddress(CellAddress.MaxRows - 1, last)));
            }
            if (_wholeRows.Match(text) is { Success: true } rows
                && int.TryParse(rows.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var top)
                && int.TryParse(rows.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bottom)
                && top >= 1 && bottom >= 1 && top <= CellAddress.MaxRows && bottom <= CellAddress.MaxRows)
                return Normalized(new CellRange(new CellAddress(top - 1, 0), new CellAddress(bottom - 1, CellAddress.MaxColumns - 1)));
            return null;
        }

        if (colon < 0) return R1C1(text, origin) is { } single ? CellRange.Single(single) : null;
        if (R1C1(text[..colon], origin) is not { } start || R1C1(text[(colon + 1)..], origin) is not { } end) return null;
        return Normalized(new CellRange(start, end));
    }

    private CellAddress? R1C1(string text, CellAddress origin)
    {
        var match = _r1c1.Match(text.Trim());
        if (!match.Success) return null;
        var row = Coordinate(match.Groups[1].Value, origin.Row);
        var column = Coordinate(match.Groups[2].Value, origin.Column);
        if (row is null || column is null) return null;
        var address = new CellAddress(row.Value, column.Value);
        return address.IsValid ? address : null;
    }

    private int? Coordinate(string part, int origin)
    {
        if (part.Length == 0) return origin;
        if (part[0] == '[')
            return int.TryParse(part[1..^1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var relative) ? origin + relative : null;
        return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var absolute) && absolute >= 1 ? absolute - 1 : null;
    }

    private CellRange Normalized(CellRange range) => new(
        new CellAddress(Math.Min(range.Start.Row, range.End.Row), Math.Min(range.Start.Column, range.End.Column)),
        new CellAddress(Math.Max(range.Start.Row, range.End.Row), Math.Max(range.Start.Column, range.End.Column)));
}
