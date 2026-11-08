namespace PulseOffice.Formats.OpenXml;

internal sealed class ExcelBooleanFormat
{
    private const string Keyword = "BOOLEAN";

    public string ToExcel(string code)
    {
        var index = code.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || code.Contains(';')) return code;

        var prefix = Plain(code[..index]);
        var suffix = Plain(code[(index + Keyword.Length)..]);
        string Section(string word) => $"\"{prefix}{word}{suffix}\"";
        return $"{Section("TRUE")};{Section("TRUE")};{Section("FALSE")}";
    }

    private string Plain(string literal) => literal.Replace("\"", string.Empty, StringComparison.Ordinal).Replace("\\", string.Empty, StringComparison.Ordinal);
}
