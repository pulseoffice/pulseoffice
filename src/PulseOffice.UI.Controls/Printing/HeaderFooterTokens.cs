using System.Text;

namespace PulseOffice.UI.Controls.Printing;

public sealed record HeaderFooterField(string Code, string Label, string Icon, string Sample);

public sealed record HeaderFooterPreset(string Label, string Left, string Center, string Right);

public interface IHeaderFooterTokens
{
    string ToDisplay(string code, IReadOnlyList<HeaderFooterField> fields);

    string ToCode(string display, IReadOnlyList<HeaderFooterField> fields);

    string Expand(string code, IReadOnlyList<HeaderFooterField> fields);

    string Token(HeaderFooterField field);
}

public sealed class HeaderFooterTokens : IHeaderFooterTokens
{
    private const char Open = '[';
    private const char Close = ']';

    public string Token(HeaderFooterField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return Open + field.Label + Close;
    }

    public string ToDisplay(string code, IReadOnlyList<HeaderFooterField> fields) => Walk(code, fields, Token);

    public string Expand(string code, IReadOnlyList<HeaderFooterField> fields) => Walk(code, fields, field => field.Sample);

    public string ToCode(string display, IReadOnlyList<HeaderFooterField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (string.IsNullOrEmpty(display)) return string.Empty;

        var result = new StringBuilder();
        var index = 0;
        while (index < display.Length)
        {
            if (display[index] == Open && fields.FirstOrDefault(field => Matches(display, index, Token(field))) is { } field)
            {
                result.Append(field.Code);
                index += Token(field).Length;
                continue;
            }

            if (display[index] == '&' && !IsFormattingCode(display, index)) result.Append("&&");
            else result.Append(display[index]);
            index++;
        }

        return result.ToString();
    }

    private static bool IsFormattingCode(string text, int index) =>
        index + 1 < text.Length && (text[index + 1] is '"' or 'B' or 'I' or 'U' or 'S' or 'E' or 'X' or 'Y' or 'K' or 'G' || char.IsAsciiDigit(text[index + 1]));

    private static bool Matches(string text, int index, string token) =>
        string.CompareOrdinal(text, index, token, 0, token.Length) == 0;

    private static string Walk(string code, IReadOnlyList<HeaderFooterField> fields, Func<HeaderFooterField, string> render)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (string.IsNullOrEmpty(code)) return string.Empty;

        var result = new StringBuilder();
        for (var index = 0; index < code.Length; index++)
        {
            if (code[index] == '&' && index + 1 < code.Length)
            {
                if (code[index + 1] == '&')
                {
                    result.Append('&');
                    index++;
                    continue;
                }

                var pair = code.Substring(index, 2);
                if (fields.FirstOrDefault(field => string.Equals(field.Code, pair, StringComparison.OrdinalIgnoreCase)) is { } field)
                {
                    result.Append(render(field));
                    index++;
                    continue;
                }
            }

            result.Append(code[index]);
        }

        return result.ToString();
    }
}
