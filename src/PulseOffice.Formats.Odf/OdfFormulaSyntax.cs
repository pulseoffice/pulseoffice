using System.Text.RegularExpressions;

namespace PulseOffice.Formats.Odf;

public sealed class OdfFormulaSyntax : FormulaDialect
{
    private readonly OdfFunctionNames _functions = new();

    protected override FunctionNameTable? Functions => _functions;

    private const string OdfPrefix = "of:=";

    private static readonly Regex OdfReference = new(
        @"\[(?<sheet>\$?(?:'(?:[^']|'')*'|[^\].':]*))\.(?<start>\$?[A-Z]+\$?[0-9]+)(?::(?<endSheet>\$?(?:'(?:[^']|'')*'|[^\].':]*))\.(?<end>\$?[A-Z]+\$?[0-9]+))?\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex OurReference = new(
        @"(?<![A-Za-z0-9_.$!'])(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_]*)[.!])?(?<start>\$?[A-Z]{1,3}\$?[0-9]{1,7})(?::(?:(?<endSheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_]*)[.!])?(?<end>\$?[A-Z]{1,3}\$?[0-9]{1,7}))?(?![A-Za-z0-9_(])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex OdfWhole = new(
        @"\[(?<sheet>\$?(?:'(?:[^']|'')*'|[^\].':]*))\.(?<start>\$?[A-Z]+|\$?[0-9]+):(?<endSheet>\$?(?:'(?:[^']|'')*'|[^\].':]*))\.(?<end>\$?[A-Z]+|\$?[0-9]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex OurWhole = new(
        @"(?<![A-Za-z0-9_.$!'\[])(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_]*)[.!])?(?<start>\$?[A-Z]{1,3}|\$?[0-9]{1,7}):(?<end>\$?[A-Z]{1,3}|\$?[0-9]{1,7})(?![A-Za-z0-9_(.!])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex OdfSpill = new(
        @"_xlfn\.ANCHORARRAY\(\s*(?<reference>\[[^\]]+\])\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static readonly Regex DeletedReference = new(
        @"\[[^\[\]]*#REF![^\[\]]*\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex ExportedSpill = new(
        @"(?<reference>\[[^\]]+\])#",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    protected override string Prefix => OdfPrefix;

    protected override bool ImportsOutsideQuotes => false;

    public string ToOdf(string internalFormula) => FromInternal(internalFormula);

    public string ToOdfExpression(string internalFormula) => ToOdf(internalFormula)[OdfPrefix.Length..];

    public string ToInternalExpression(string odfExpression) =>
        ToInternal(OdfPrefix + odfExpression)?.TrimStart('=') ?? string.Empty;

    protected override string Export(string segment) => ExportedSpill.Replace(ExportReferences(segment), match =>
        $"_xlfn.ANCHORARRAY({match.Groups["reference"].Value})");

    private string ExportReferences(string segment) => OurReference.Replace(OurWhole.Replace(segment, match =>
        SameKind(match.Groups["start"].Value, match.Groups["end"].Value)
            ? $"[{Exported(match.Groups["sheet"].Value)}.{match.Groups["start"].Value}:.{match.Groups["end"].Value}]"
            : match.Value), match =>
    {
        var reference = $"[{Exported(match.Groups["sheet"].Value)}.{match.Groups["start"].Value}";
        if (match.Groups["endSheet"].Success) reference += $":{Exported(match.Groups["endSheet"].Value)}.{match.Groups["end"].Value}";
        else if (match.Groups["end"].Success) reference += $":.{match.Groups["end"].Value}";
        return reference + "]";
    });

    protected override string Import(string segment) => OdfWhole.Replace(ImportCells(DeletedReference.Replace(OdfSpill.Replace(segment, match => match.Groups["reference"].Value + "#"), "#REF!")), match =>
    {
        if (!SameKind(match.Groups["start"].Value, match.Groups["end"].Value)) return match.Value;
        var sheet = Unquote(match.Groups["sheet"].Value.TrimStart('$'));
        var qualifier = string.IsNullOrEmpty(sheet) ? string.Empty : Quote(sheet) + ".";
        return qualifier + match.Groups["start"].Value + ":" + match.Groups["end"].Value;
    });

    private bool SameKind(string start, string end) =>
        char.IsAsciiDigit(start.TrimStart('$')[0]) == char.IsAsciiDigit(end.TrimStart('$')[0]);

    private string ImportCells(string segment) => OdfReference.Replace(segment, match =>
    {
        var sheet = Unquote(match.Groups["sheet"].Value.TrimStart('$'));
        var endSheet = Unquote(match.Groups["endSheet"].Value.TrimStart('$'));
        var endQualifier = endSheet.Length > 0 && !string.Equals(endSheet, sheet, StringComparison.Ordinal) ? Quote(endSheet) + "." : string.Empty;
        var end = match.Groups["end"].Success ? ":" + endQualifier + match.Groups["end"].Value : string.Empty;
        var qualifier = string.IsNullOrEmpty(sheet) ? string.Empty : Quote(sheet) + ".";
        return qualifier + match.Groups["start"].Value + end;
    });

    private string Exported(string sheet) =>
        sheet.Length == 0 || sheet.StartsWith('\'') ? sheet : Quote(sheet);

    private string Unquote(string sheet) =>
        sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\'' ? sheet[1..^1].Replace("''", "'", StringComparison.Ordinal) : sheet;

    private string Quote(string sheetName) =>
        sheetName.Any(c => !char.IsLetterOrDigit(c) && c != '_') || sheetName.Length == 0 || char.IsAsciiDigit(sheetName[0]) || CellLike.IsMatch(sheetName)
            ? $"'{sheetName.Replace("'", "''", StringComparison.Ordinal)}'"
            : sheetName;

    private static readonly Regex CellLike = new(@"^[A-Za-z]{1,3}[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}
