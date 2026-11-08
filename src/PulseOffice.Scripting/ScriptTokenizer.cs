namespace PulseOffice.Scripting;

public enum ScriptTokenKind { Plain, Keyword, Type, Number, Text, Comment, Symbol, Operator, Api }

public sealed record ScriptToken(int Start, int Length, ScriptTokenKind Kind);

public interface IScriptTokenizer
{
    IReadOnlyList<ScriptToken> Tokenize(string source, ScriptLanguage language, IReadOnlySet<string> apiNames);
}

public sealed class ScriptTokenizer : IScriptTokenizer
{
    private static readonly HashSet<string> JavaScriptKeywords = new(StringComparer.Ordinal)
    {
        "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default",
        "delete", "do", "else", "export", "extends", "finally", "for", "function", "if", "import",
        "in", "instanceof", "let", "new", "of", "return", "static", "super", "switch", "this",
        "throw", "try", "typeof", "var", "void", "while", "yield", "async",
    };

    private static readonly HashSet<string> JavaScriptLiterals = new(StringComparer.Ordinal)
    {
        "true", "false", "null", "undefined", "NaN", "Infinity",
    };

    private static readonly HashSet<string> PythonKeywords = new(StringComparer.Ordinal)
    {
        "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
        "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in", "is",
        "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while", "with", "yield",
    };

    private static readonly HashSet<string> PythonLiterals = new(StringComparer.Ordinal)
    {
        "True", "False", "None", "self",
    };

    public IReadOnlyList<ScriptToken> Tokenize(
        string source, ScriptLanguage language, IReadOnlySet<string> apiNames)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(apiNames);

        var keywords = language == ScriptLanguage.Python ? PythonKeywords : JavaScriptKeywords;
        var literals = language == ScriptLanguage.Python ? PythonLiterals : JavaScriptLiterals;

        var tokens = new List<ScriptToken>();
        var index = 0;

        while (index < source.Length)
        {
            var current = source[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (Comment(source, index, language) is { } comment)
            {
                tokens.Add(comment);
                index = comment.Start + comment.Length;
                continue;
            }

            if (current is '"' or '\'' or '`')
            {
                var text = language == ScriptLanguage.Python && IsTriple(source, index)
                    ? TripleText(source, index)
                    : Text(source, index);
                tokens.Add(text);
                index = text.Start + text.Length;
                continue;
            }

            if (char.IsAsciiDigit(current))
            {
                var number = Number(source, index);
                tokens.Add(number);
                index = number.Start + number.Length;
                continue;
            }

            if (char.IsLetter(current) || current == '_' || current == '$')
            {
                var start = index;

                while (index < source.Length
                       && (char.IsLetterOrDigit(source[index]) || source[index] == '_' || source[index] == '$'))
                    index++;

                var word = source[start..index];

                var kind = keywords.Contains(word) ? ScriptTokenKind.Keyword
                    : literals.Contains(word) ? ScriptTokenKind.Type
                    : apiNames.Contains(word) ? ScriptTokenKind.Api
                    : ScriptTokenKind.Plain;

                if (kind != ScriptTokenKind.Plain) tokens.Add(new ScriptToken(start, index - start, kind));

                continue;
            }

            if (!char.IsLetterOrDigit(current))
            {
                tokens.Add(new ScriptToken(index, 1, ScriptTokenKind.Operator));
                index++;
                continue;
            }

            index++;
        }

        return tokens;
    }

    private static ScriptToken? Comment(string source, int index, ScriptLanguage language)
    {
        if (language == ScriptLanguage.Python)
        {
            if (source[index] != '#') return null;

            var end = source.IndexOf('\n', index);

            return new ScriptToken(index, (end < 0 ? source.Length : end) - index, ScriptTokenKind.Comment);
        }

        if (index + 1 >= source.Length || source[index] != '/') return null;

        if (source[index + 1] == '/')
        {
            var end = source.IndexOf('\n', index);

            return new ScriptToken(index, (end < 0 ? source.Length : end) - index, ScriptTokenKind.Comment);
        }

        if (source[index + 1] != '*') return null;

        var close = source.IndexOf("*/", index + 2, StringComparison.Ordinal);

        return new ScriptToken(
            index,
            (close < 0 ? source.Length : close + 2) - index,
            ScriptTokenKind.Comment);
    }

    private static bool IsTriple(string source, int index) =>
        source[index] is '"' or '\'' && index + 2 < source.Length
        && source[index + 1] == source[index] && source[index + 2] == source[index];

    private static ScriptToken TripleText(string source, int index)
    {
        var quote = source[index];
        var position = index + 3;

        while (position < source.Length)
        {
            if (source[position] == '\\')
            {
                position += 2;
                continue;
            }

            if (position + 2 < source.Length && source[position] == quote
                && source[position + 1] == quote && source[position + 2] == quote)
                return new ScriptToken(index, position + 3 - index, ScriptTokenKind.Text);

            position++;
        }

        return new ScriptToken(index, source.Length - index, ScriptTokenKind.Text);
    }

    private static ScriptToken Text(string source, int index)
    {
        var quote = source[index];
        var position = index + 1;

        while (position < source.Length)
        {
            if (source[position] == '\\')
            {
                position += 2;
                continue;
            }

            if (source[position] == quote)
            {
                position++;
                break;
            }

            if (quote != '`' && source[position] == '\n') break;

            position++;
        }

        return new ScriptToken(index, Math.Min(position, source.Length) - index, ScriptTokenKind.Text);
    }

    private static ScriptToken Number(string source, int index)
    {
        var position = index;

        if (source[position] == '0' && position + 1 < source.Length && source[position + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            position += 2;
            while (position < source.Length && (char.IsAsciiHexDigit(source[position]) || source[position] == '_')) position++;

            return new ScriptToken(index, position - index, ScriptTokenKind.Number);
        }

        while (position < source.Length && (char.IsAsciiDigit(source[position]) || source[position] is '.' or '_')) position++;

        if (position < source.Length && source[position] is 'e' or 'E')
        {
            var exponent = position + 1;
            if (exponent < source.Length && source[exponent] is '+' or '-') exponent++;

            if (exponent < source.Length && char.IsAsciiDigit(source[exponent]))
            {
                position = exponent;
                while (position < source.Length && char.IsAsciiDigit(source[position])) position++;
            }
        }

        if (position < source.Length && source[position] is 'n' or 'j' or 'J' or 'L' or 'l') position++;

        return new ScriptToken(index, position - index, ScriptTokenKind.Number);
    }
}
