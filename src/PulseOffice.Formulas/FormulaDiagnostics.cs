namespace PulseOffice.Formulas;

public sealed record FormulaProblem(string Key, int Position, string Detail = "");

public interface IFormulaDiagnostics
{
    IReadOnlyList<FormulaProblem> Check(string source);
}

public sealed class FormulaDiagnostics : IFormulaDiagnostics
{
    private readonly IFormulaVocabulary _vocabulary;

    public FormulaDiagnostics() : this(FormulaVocabulary.Default) { }

    public FormulaDiagnostics(IFormulaVocabulary vocabulary) => _vocabulary = vocabulary;

    public IReadOnlyList<FormulaProblem> Check(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var problems = new List<FormulaProblem>();
        var open = new Stack<(char Character, int Position)>();
        var quoted = false;
        var quoteStart = -1;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];

            if (character == '"')
            {
                quoted = !quoted;
                if (quoted) quoteStart = index;
                continue;
            }

            if (quoted) continue;

            if (AfterDelimiterKeyword(source, index)) continue;

            switch (character)
            {
                case '{' or '(' or '[':
                    open.Push((character, index));
                    break;

                case '}' or ')' or ']':
                    if (open.Count == 0) problems.Add(new FormulaProblem("unexpectedClose", index, character.ToString()));
                    else if (Closing(open.Pop().Character) != character)
                        problems.Add(new FormulaProblem("mismatched", index, character.ToString()));
                    break;
            }
        }

        if (quoted) problems.Add(new FormulaProblem("unclosedText", quoteStart));

        foreach (var (character, position) in open)
            problems.Add(new FormulaProblem("unclosed", position, character.ToString()));

        problems.AddRange(UnknownWords(source));

        return [.. problems.OrderBy(problem => problem.Position)];
    }

    private IEnumerable<FormulaProblem> UnknownWords(string source)
    {
        var index = 0;
        var quoted = false;

        while (index < source.Length)
        {
            if (source[index] == '"') { quoted = !quoted; index++; continue; }
            if (quoted) { index++; continue; }

            if (!char.IsLetter(source[index])) { index++; continue; }

            var start = index;
            while (index < source.Length && char.IsLetter(source[index])) index++;

            var word = source[start..index];

            if (word.Length < 2) continue;
            if (start > 0 && source[start - 1] == '%' && word[0] == 'i' && _vocabulary.Symbols.ContainsKey(word[1..])) continue;
            if (_vocabulary.Delimiters.ContainsKey(word)) continue;
            if (_vocabulary.Symbols.ContainsKey(word)) continue;
            if (_vocabulary.LargeOperators.ContainsKey(word)) continue;
            if (_vocabulary.Functions.Contains(word)) continue;
            if (_vocabulary.Constructs.Contains(word)) continue;

            yield return new FormulaProblem("unknownWord", start, word);
        }
    }

    private bool AfterDelimiterKeyword(string source, int index)
    {
        var end = index;
        while (end > 0 && char.IsWhiteSpace(source[end - 1])) end--;

        var start = end;
        while (start > 0 && char.IsLetter(source[start - 1])) start--;

        return source[start..end] is "left" or "right";
    }

    private static char Closing(char open) => open switch
    {
        '{' => '}',
        '[' => ']',

        _ => ')',
    };
}
