using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxFormulaSyntax : FormulaDialect
{
    private readonly OoxmlFunctionNames _functions = new();
    private readonly XlsxSingleValueSyntax _single = new();

    protected override FunctionNameTable? Functions => _functions;

    private const string Name = @"'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_]*";
    private const string Cell = @"\$?[A-Z]{1,3}\$?[0-9]{1,7}";
    private const string Whole = @"\$?[A-Z]{1,3}:\$?[A-Z]{1,3}(?![0-9])|\$?[0-9]{1,7}:\$?[0-9]{1,7}";

    private static readonly Regex InternalSpan = new(
        $@"(?<first>{Name})\.(?<start>{Cell}):(?<last>{Name})\.(?<end>{Cell})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex OpenXmlSpan = new(
        $@"(?:'(?<qfirst>(?:[^':]|'')+):(?<qlast>(?:[^']|'')+)'|(?<first>[A-Za-z_][A-Za-z0-9_]*):(?<last>[A-Za-z_][A-Za-z0-9_]*))!(?<start>{Cell})(?::(?<end>{Cell}))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex SheetReference = new(
        $@"(?<quote>'[^']+'|[A-Za-z_][A-Za-z0-9_]*)(?<separator>[.!])(?<cell>{Whole}|{Cell})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private const string Area = $@"(?:(?:{Name})[.!])?{Cell}(?::{Cell})?|(?:(?:{Name})[.!])?(?:{Whole})";

    private static readonly Regex InternalUnion = new(
        $@"(?<![A-Za-z0-9_.$'])(?<areas>(?:{Area})(?:~(?:{Area}))+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex OpenXmlUnion = new(
        $@"(?<![A-Za-z0-9_.$'])\((?<areas>(?:{Area})(?:;(?:{Area}))+)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex AnchorArray = new(
        $@"_xlfn\.ANCHORARRAY\(\s*(?<reference>(?:(?:{Name})!)?{Cell})\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static readonly Regex Spill = new(
        $@"(?<![A-Za-z0-9_.$'])(?<reference>(?:(?:{Name})[.!])?{Cell})#",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public string ToOpenXml(string internalFormula) => FromInternal(internalFormula);

    public new string ToInternal(string openXmlFormula)
    {
        ArgumentNullException.ThrowIfNull(openXmlFormula);
        return base.ToInternal(openXmlFormula) ?? "=";
    }

    protected override string Prepare(string formula) => _single.Export(formula);

    protected override string Export(string segment)
    {
        var unions = InternalUnion.Replace(segment, match => $"({match.Groups["areas"].Value.Replace('~', '\u0001')})");
        var spills = Spill.Replace(unions, match => $"_xlfn.ANCHORARRAY({match.Groups["reference"].Value})");
        var spans = InternalSpan.Replace(spills, match =>
            $"{Joined(Bare(match.Groups["first"].Value), Bare(match.Groups["last"].Value))}!{match.Groups["start"].Value}:{match.Groups["end"].Value}");
        var references = SheetReference.Replace(spans, match =>
            match.Groups["separator"].Value == "!" ? match.Value : match.Groups["quote"].Value + "!" + match.Groups["cell"].Value);
        return Separators(references, ';', ',', '|', ';').Replace('\u0001', ',');
    }

    protected override string Import(string segment)
    {
        var separators = Separators(AnchorArray.Replace(_single.Import(segment), match => match.Groups["reference"].Value + "#"), ',', ';', ';', '|');
        var unions = OpenXmlUnion.Replace(separators, match => match.Groups["areas"].Value.Replace(';', '~'));
        var spans = OpenXmlSpan.Replace(unions, match =>
        {
            var first = match.Groups["qfirst"].Success ? match.Groups["qfirst"].Value.Replace("''", "'", StringComparison.Ordinal) : match.Groups["first"].Value;
            var last = match.Groups["qlast"].Success ? match.Groups["qlast"].Value.Replace("''", "'", StringComparison.Ordinal) : match.Groups["last"].Value;
            var end = match.Groups["end"].Success ? match.Groups["end"].Value : match.Groups["start"].Value;
            return $"{Quoted(first)}.{match.Groups["start"].Value}:{Quoted(last)}.{end}";
        });
        return SheetReference.Replace(spans, match =>
            match.Groups["separator"].Value == "." ? match.Value : match.Groups["quote"].Value + "." + match.Groups["cell"].Value);
    }

    private string Separators(string text, char argument, char exportedArgument, char row, char exportedRow)
    {
        var result = new StringBuilder(text.Length);
        var depth = 0;
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '\'') quoted = !quoted;
            if (!quoted && c == '{') depth++;
            if (!quoted && c == '}' && depth > 0) depth--;
            if (!quoted && depth > 0 && c == row) result.Append(exportedRow);
            else if (!quoted && c == argument) result.Append(exportedArgument);
            else result.Append(c);
        }
        return result.ToString();
    }

    private string Bare(string sheet) =>
        sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\'' ? sheet[1..^1].Replace("''", "'", StringComparison.Ordinal) : sheet;

    private string Joined(string first, string last) =>
        NeedsQuotes(first) || NeedsQuotes(last)
            ? $"'{first.Replace("'", "''", StringComparison.Ordinal)}:{last.Replace("'", "''", StringComparison.Ordinal)}'"
            : $"{first}:{last}";

    private string Quoted(string sheet) =>
        NeedsQuotes(sheet) ? $"'{sheet.Replace("'", "''", StringComparison.Ordinal)}'" : sheet;

    private bool NeedsQuotes(string sheet) =>
        sheet.Length == 0 || char.IsAsciiDigit(sheet[0]) || sheet.Any(c => !char.IsLetterOrDigit(c) && c != '_') || CellLike.IsMatch(sheet);

    private static readonly System.Text.RegularExpressions.Regex CellLike = new(@"^[A-Za-z]{1,3}[0-9]+$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}
