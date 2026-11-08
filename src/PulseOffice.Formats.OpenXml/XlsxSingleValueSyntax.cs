using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxSingleValueSyntax
{
    private const string Function = "_xlfn.SINGLE";

    private static readonly Regex Imported = new(@"_xlfn\.SINGLE\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    public string Import(string segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return Imported.Replace(segment, "@(");
    }

    private const int MaxNesting = 64;

    public string Export(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        return Export(formula, 0);
    }

    private string Export(string formula, int depth)
    {
        if (formula.IndexOf('@') < 0 || depth > MaxNesting) return formula;
        var builder = new StringBuilder(formula.Length + 16);
        var index = 0;
        while (index < formula.Length)
        {
            var character = formula[index];
            if (character == '"')
            {
                var end = QuotedEnd(formula, index, '"');
                builder.Append(formula, index, end - index);
                index = end;
                continue;
            }
            if (character != '@')
            {
                builder.Append(character);
                index++;
                continue;
            }
            var start = index + 1;
            while (start < formula.Length && (char.IsWhiteSpace(formula[start]) || formula[start] == '@')) start++;
            var operandEnd = OperandEnd(formula, start);
            var operand = Export(formula[start..operandEnd], depth + 1);
            builder.Append(Function);
            if (operand.StartsWith('(') && Closing(operand, 0) == operand.Length - 1) builder.Append(operand);
            else builder.Append('(').Append(operand).Append(')');
            index = operandEnd;
        }
        return builder.ToString();
    }

    private int OperandEnd(string text, int start)
    {
        var index = start;
        if (index < text.Length && text[index] == '(') return Math.Min(Closing(text, index) + 1, text.Length);
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '\'')
            {
                index = QuotedEnd(text, index, '\'');
                continue;
            }
            if (character == '[')
            {
                var end = text.IndexOf(']', index);
                index = end < 0 ? text.Length : end + 1;
                continue;
            }
            if (character == '(') return Math.Min(Closing(text, index) + 1, text.Length);
            if (char.IsLetterOrDigit(character) || character is '_' or '.' or '$' or ':' or '!' or '#' or '\\') { index++; continue; }
            break;
        }
        return index;
    }

    private int Closing(string text, int open)
    {
        var depth = 0;
        var index = open;
        while (index < text.Length)
        {
            var character = text[index];
            if (character is '"' or '\'')
            {
                index = QuotedEnd(text, index, character);
                continue;
            }
            if (character == '(') depth++;
            else if (character == ')' && --depth == 0) return index;
            index++;
        }
        return text.Length;
    }

    private int QuotedEnd(string text, int start, char quote)
    {
        var index = start + 1;
        while (index < text.Length)
        {
            if (text[index] == quote)
            {
                if (index + 1 < text.Length && text[index + 1] == quote) { index += 2; continue; }
                return index + 1;
            }
            index++;
        }
        return text.Length;
    }
}
