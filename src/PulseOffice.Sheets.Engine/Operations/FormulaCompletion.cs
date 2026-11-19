namespace PulseOffice.Sheets.Engine.Operations;

public readonly record struct FunctionCall(string Name, int Argument);

public interface IFormulaCompletion
{
    string? Complete(string text, int caret, IEnumerable<string> names);

    FunctionCall? CallAt(string text, int caret, char argumentSeparator);
}

public sealed class FormulaCompletion : IFormulaCompletion
{
    public string? Complete(string text, int caret, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(names);
        if (text.Length == 0 || text[0] != '=' || caret != text.Length || InsideQuotes(text, caret)) return null;

        var start = caret;
        while (start > 1 && IsNamePart(text[start - 1])) start--;
        if (start == caret || !char.IsLetter(text[start]) && text[start] != '_') return null;
        if (start > 1 && (text[start - 1] is '$' or '!' or '\'' || char.IsDigit(text[start - 1]))) return null;

        var typed = text[start..caret];
        var match = names
            .Where(name => name.Length > typed.Length && name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return match is null ? null : text + match[typed.Length..];
    }

    public FunctionCall? CallAt(string text, int caret, char argumentSeparator)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 || text[0] != '=') return null;
        caret = Math.Clamp(caret, 0, text.Length);

        var calls = new Stack<(string Name, int Argument)>();
        var braces = 0;
        var quote = '\0';
        for (var index = 1; index < caret; index++)
        {
            var current = text[index];
            if (quote != '\0')
            {
                if (current == quote) quote = '\0';
                continue;
            }

            switch (current)
            {
                case '"' or '\'':
                    quote = current;
                    break;
                case '{':
                    braces++;
                    break;
                case '}':
                    braces = Math.Max(0, braces - 1);
                    break;
                case '(':
                    calls.Push((NameBefore(text, index), 0));
                    break;
                case ')':
                    if (calls.Count > 0) calls.Pop();
                    break;
                default:
                    if (current == argumentSeparator && braces == 0 && calls.Count > 0)
                    {
                        var (name, argument) = calls.Pop();
                        calls.Push((name, argument + 1));
                    }
                    break;
            }
        }

        if (quote != '\0') return null;
        foreach (var call in calls)
            if (call.Name.Length > 0) return new FunctionCall(call.Name, call.Argument);
        return null;
    }

    private string NameBefore(string text, int open)
    {
        var start = open;
        while (start > 1 && IsNamePart(text[start - 1])) start--;
        return text[start..open];
    }

    private bool IsNamePart(char letter) => char.IsLetterOrDigit(letter) || letter is '_' or '.';

    private bool InsideQuotes(string text, int caret)
    {
        var quote = '\0';
        for (var index = 0; index < caret; index++)
        {
            var current = text[index];
            if (quote == '\0')
            {
                if (current is '"' or '\'') quote = current;
            }
            else if (current == quote) quote = '\0';
        }
        return quote != '\0';
    }
}
