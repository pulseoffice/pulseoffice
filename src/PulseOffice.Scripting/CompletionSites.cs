namespace PulseOffice.Scripting;

public enum CompletionSiteKind { None, Global, Member }

public sealed record CompletionSite(CompletionSiteKind Kind, string Path)
{
    public static readonly CompletionSite None = new(CompletionSiteKind.None, string.Empty);

    public static readonly CompletionSite Global = new(CompletionSiteKind.Global, string.Empty);
}

public sealed class CompletionSites
{
    private readonly SourcePositions _positions = new();
    private readonly ScriptTokenizer _tokenizer = new();

    public CompletionSite Find(string source, ScriptPosition position, ScriptLanguage language)
    {
        ArgumentNullException.ThrowIfNull(source);

        var offset = Math.Clamp(_positions.Offset(source, position), 0, source.Length);

        if (InsideTextOrComment(source, offset, language)) return CompletionSite.None;

        var end = offset;
        while (end > 0 && IsWordPart(source[end - 1], language)) end--;

        if (end < offset && char.IsAsciiDigit(source[end])) return CompletionSite.None;
        if (end == 0 || source[end - 1] != '.') return CompletionSite.Global;

        var start = end - 1;
        while (start > 0 && (IsWordPart(source[start - 1], language) || source[start - 1] == '.')) start--;

        var path = source[start..(end - 1)];
        var parts = path.Split('.');

        if (parts.Any(part => part.Length == 0 || char.IsAsciiDigit(part[0]))) return CompletionSite.None;

        return new CompletionSite(CompletionSiteKind.Member, path);
    }

    private bool InsideTextOrComment(string source, int offset, ScriptLanguage language) =>
        _tokenizer.Tokenize(source[..offset], language, new HashSet<string>())
            .Any(token => token.Kind is ScriptTokenKind.Text or ScriptTokenKind.Comment
                          && token.Start + token.Length == offset
                          && !ClosedText(source, token));

    private bool ClosedText(string source, ScriptToken token)
    {
        if (token.Kind == ScriptTokenKind.Comment)
            return token.Length >= 4 && source.AsSpan(token.Start, 2).SequenceEqual("/*")
                && source.AsSpan(token.Start + token.Length - 2, 2).SequenceEqual("*/");

        if (token.Length < 2) return false;

        var quote = source[token.Start];
        var last = token.Start + token.Length - 1;
        if (source[last] != quote) return false;

        if (token.Length >= 3 && source[token.Start + 1] == quote && source[token.Start + 2] == quote)
            return token.Length >= 6 && source[last - 1] == quote && source[last - 2] == quote;

        var escapes = 0;
        for (var index = last - 1; index > token.Start && source[index] == '\\'; index--) escapes++;
        return escapes % 2 == 0;
    }

    private bool IsWordPart(char character, ScriptLanguage language) =>
        char.IsLetterOrDigit(character) || character == '_' || (language == ScriptLanguage.JavaScript && character == '$');
}
