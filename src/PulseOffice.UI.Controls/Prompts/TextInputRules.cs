namespace PulseOffice.UI.Controls.Prompts;

public interface ITextInputRule
{
    string? Check(string text);
}

public sealed class RequiredTextRule(string message) : ITextInputRule
{
    public string? Check(string text) => string.IsNullOrWhiteSpace(text) ? message : null;
}

public sealed class ForbiddenCharactersRule(IReadOnlyCollection<char> forbidden, Func<string, string> message) : ITextInputRule
{
    public string? Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = text.Where(forbidden.Contains).Distinct().ToArray();
        return found.Length == 0 ? null : message(string.Join(' ', found));
    }
}

public sealed class UniqueNameRule(Func<string, bool> taken, Func<string, string> message) : ITextInputRule
{
    public string? Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        return trimmed.Length > 0 && taken(trimmed) ? message(trimmed) : null;
    }
}

public sealed class MaximumLengthRule(int maximum, Func<int, string> message) : ITextInputRule
{
    public string? Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Trim().Length > maximum ? message(maximum) : null;
    }
}
