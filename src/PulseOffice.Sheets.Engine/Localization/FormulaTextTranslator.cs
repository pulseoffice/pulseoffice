using System.Text;

namespace PulseOffice.Sheets.Engine.Localization;

public interface IFormulaTextTranslator
{
    string ToLocal(string canonical, FormulaPunctuation local, Func<string, string> functionName, Func<string, string> bareName);

    string ToCanonical(string typed, FormulaPunctuation local, Func<string, string> functionName, Func<string, string> bareName);
}

public sealed class FormulaTextTranslator : IFormulaTextTranslator
{
    private const char CanonicalSeparator = ';';
    private const char CanonicalArrayRow = '|';
    private const string ParameterPrefix = "_xlpm.";

    public string ToLocal(string canonical, FormulaPunctuation local, Func<string, string> functionName, Func<string, string> bareName)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(local);
        return Translate(canonical, functionName, name => bareName(Parameter(name)), (text, index, inArray) => text[index] switch
        {
            CanonicalSeparator => inArray ? local.ArrayColumn : local.Argument,
            CanonicalArrayRow when inArray => local.ArrayRow,
            '.' when IsDecimalPoint(text, index) => local.Decimal,
            _ => text[index],
        });
    }

    public string ToCanonical(string typed, FormulaPunctuation local, Func<string, string> functionName, Func<string, string> bareName)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(local);
        return Translate(typed, functionName, bareName, (text, index, inArray) =>
        {
            var character = text[index];
            if (local.Decimal != '.' && character == local.Decimal && IsDecimalPoint(text, index)) return '.';
            if (inArray && character == local.ArrayRow) return CanonicalArrayRow;
            if (character == CanonicalArrayRow && inArray) return CanonicalArrayRow;
            if (inArray && character == local.ArrayColumn) return CanonicalSeparator;
            if (!inArray && character == local.Argument) return CanonicalSeparator;
            if (character is ',' or ';') return CanonicalSeparator;
            return character;
        });
    }

    private string Parameter(string name) =>
        name.StartsWith(ParameterPrefix, StringComparison.OrdinalIgnoreCase) ? name[ParameterPrefix.Length..] : name;

    private string Translate(string text, Func<string, string> functionName, Func<string, string> otherName, Func<string, int, bool, char> punctuation)
    {
        var builder = new StringBuilder(text.Length);
        var arrayDepth = 0;
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (character is '"' or '\'')
            {
                var end = QuotedEnd(text, index, character);
                builder.Append(text, index, end - index);
                index = end;
                continue;
            }
            if (character == '#')
            {
                var end = index + 1;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '/' or '!' or '?' or '_' or '.')) end++;
                builder.Append(text, index, end - index);
                index = end;
                continue;
            }
            if (IsNameStart(character) && !IsNamePart(Previous(text, index)))
            {
                var end = index + 1;
                while (end < text.Length && (IsNamePart(text[end]) || text[end] == '.')) end++;
                var name = text[index..end];
                builder.Append(end < text.Length && text[end] == '(' ? functionName(name.ToUpperInvariant()) : otherName(name));
                index = end;
                continue;
            }
            if (character == '{') arrayDepth++;
            if (character == '}' && arrayDepth > 0) arrayDepth--;
            builder.Append(punctuation(text, index, arrayDepth > 0));
            index++;
        }
        return builder.ToString();
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

    private bool IsDecimalPoint(string text, int index)
    {
        if (index + 1 >= text.Length || !char.IsAsciiDigit(text[index + 1])) return false;
        var start = index;
        while (start > 0 && char.IsAsciiDigit(text[start - 1])) start--;
        return start < index && !IsNamePart(Previous(text, start)) && Previous(text, start) != '.';
    }

    private char Previous(string text, int index) => index > 0 ? text[index - 1] : ' ';

    private bool IsNameStart(char character) => char.IsLetter(character) || character is '_' or '$';

    private bool IsNamePart(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';
}
