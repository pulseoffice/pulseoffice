using System.Text;

namespace PulseOffice.Formats;

public sealed class LambdaParameterQualifier
{
    public const string RequiredPrefix = "_xlpm.";

    private const char Separator = ';';

    private const int DeepestNesting = 64;

    public string Qualify(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        if (formula.IndexOf("LET", StringComparison.OrdinalIgnoreCase) < 0
            && formula.IndexOf("LAMBDA", StringComparison.OrdinalIgnoreCase) < 0) return formula;
        return Qualify(formula, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
    }

    private string Qualify(string text, IReadOnlySet<string> scope, int depth)
    {
        var builder = new StringBuilder(text.Length + 16);
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
            if (character == '[')
            {
                var end = text.IndexOf(']', index);
                end = end < 0 ? text.Length : end + 1;
                builder.Append(text, index, end - index);
                index = end;
                continue;
            }
            if (char.IsAsciiDigit(character) && !IsNamePart(Previous(text, index)))
            {
                var end = index + 1;
                while (end < text.Length && (IsNamePart(text[end]) || text[end] == '.')) end++;
                builder.Append(text, index, end - index);
                index = end;
                continue;
            }
            if (IsNameStart(character) && !IsNamePart(Previous(text, index)) && Previous(text, index) is not ('.' or '$' or '!'))
            {
                var end = index + 1;
                while (end < text.Length && (IsNamePart(text[end]) || text[end] == '.')) end++;
                var name = text[index..end];
                var open = SkipSpaces(text, end);
                if (open < text.Length && text[open] == '(')
                {
                    var kind = Kind(name);
                    if (kind is not null)
                    {
                        var close = Closing(text, open);
                        if (depth >= DeepestNesting)
                        {
                            var stop = Math.Min(close + 1, text.Length);
                            builder.Append(text, index, stop - index);
                            index = stop;
                            continue;
                        }

                        builder.Append(text, index, open + 1 - index);
                        builder.Append(Call(text[(open + 1)..Math.Min(close, text.Length)], scope, kind.Value, depth + 1));
                        if (close < text.Length) builder.Append(')');
                        index = Math.Min(close + 1, text.Length);
                        continue;
                    }
                    builder.Append(name);
                    index = end;
                    continue;
                }
                builder.Append(scope.Contains(name) ? RequiredPrefix + name : name);
                index = end;
                continue;
            }
            builder.Append(character);
            index++;
        }
        return builder.ToString();
    }

    private string Call(string arguments, IReadOnlySet<string> outer, CallKind kind, int depth)
    {
        var parts = Split(arguments);
        var scope = new HashSet<string>(outer, StringComparer.OrdinalIgnoreCase);
        if (kind == CallKind.Lambda)
            foreach (var parameter in parts.Take(parts.Count - 1)) if (Bare(parameter) is { } name) scope.Add(name);

        var written = new List<string>(parts.Count);
        for (var position = 0; position < parts.Count; position++)
        {
            var declares = position < parts.Count - 1 && (kind == CallKind.Lambda || position % 2 == 0);
            if (declares && Bare(parts[position]) is { } name)
            {
                if (kind == CallKind.Let) scope.Add(name);
                written.Add(Rename(parts[position], name));
                continue;
            }
            written.Add(Qualify(parts[position], scope, depth));
        }
        return string.Join(Separator, written);
    }

    private string Rename(string part, string name)
    {
        var start = part.IndexOf(part.TrimStart(), StringComparison.Ordinal);
        var trimmed = part.Trim();
        return part[..start] + RequiredPrefix + name + part[(start + trimmed.Length)..];
    }

    private string? Bare(string part)
    {
        var trimmed = part.Trim();
        if (trimmed.StartsWith(RequiredPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.Length == 0 || !IsNameStart(trimmed[0])) return null;
        return trimmed.All(c => IsNamePart(c) || c == '.') ? trimmed : null;
    }

    private List<string> Split(string arguments)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        var index = 0;
        while (index < arguments.Length)
        {
            var character = arguments[index];
            if (character is '"' or '\'')
            {
                index = QuotedEnd(arguments, index, character);
                continue;
            }
            if (character is '(' or '{' or '[') depth++;
            else if (character is ')' or '}' or ']') depth--;
            else if (character == Separator && depth == 0)
            {
                parts.Add(arguments[start..index]);
                start = index + 1;
            }
            index++;
        }
        parts.Add(arguments[start..]);
        return parts;
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

    private CallKind? Kind(string name)
    {
        var bare = name[(name.LastIndexOf('.') + 1)..];
        if (bare.Equals("LET", StringComparison.OrdinalIgnoreCase)) return CallKind.Let;
        if (bare.Equals("LAMBDA", StringComparison.OrdinalIgnoreCase)) return CallKind.Lambda;
        return null;
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

    private int SkipSpaces(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
    }

    private char Previous(string text, int index) => index > 0 ? text[index - 1] : '\0';

    private bool IsNameStart(char character) => char.IsLetter(character) || character is '_' or '\\';

    private bool IsNamePart(char character) => char.IsLetterOrDigit(character) || character == '_';

    private enum CallKind { Let, Lambda }
}
