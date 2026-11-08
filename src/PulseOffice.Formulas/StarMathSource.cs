using System.Text;

namespace PulseOffice.Formulas;

public interface IStarMathSource
{
    string ToStarMath(string source);
}

public sealed class StarMathSource(IFormulaVocabulary vocabulary) : IStarMathSource
{
    public StarMathSource() : this(FormulaVocabulary.Default) { }

    public string ToStarMath(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new StringBuilder(source.Length + 8);
        var matrices = new Stack<int>();
        var depth = 0;
        var pendingMatrix = false;
        var quoted = false;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];

            if (character == '"') quoted = !quoted;

            if (quoted || character == '"')
            {
                builder.Append(character);
                continue;
            }

            if (char.IsLetter(character))
            {
                var start = index;
                while (index < source.Length && char.IsLetter(source[index])) index++;

                var word = source[start..index];
                var symbolic = start > 0 && source[start - 1] == '%';
                builder.Append(symbolic ? word : Translate(word));
                index--;
                pendingMatrix = word == "matrix" && (start == 0 || source[start - 1] != '%');
                continue;
            }

            if (character == '{')
            {
                depth++;
                if (pendingMatrix)
                {
                    matrices.Push(depth);
                    if (IsStarMath(source, index + 1)) matrices.Pop();
                }
                pendingMatrix = false;
                builder.Append(character);
                continue;
            }

            if (!char.IsWhiteSpace(character)) pendingMatrix = false;

            if (character == '}')
            {
                if (matrices.Count > 0 && matrices.Peek() == depth) matrices.Pop();
                depth--;
                builder.Append(character);
                continue;
            }

            var inMatrix = matrices.Count > 0 && matrices.Peek() == depth;

            builder.Append(inMatrix ? character switch
            {
                '&' => "#",
                '#' => "##",
                _ => character.ToString(),
            } : character.ToString());
        }

        return builder.ToString();
    }

    private string Translate(string word)
    {
        if (vocabulary.StarMathNames.TryGetValue(word, out var name)) return name;

        return vocabulary.Symbols.TryGetValue(word, out var glyph) && glyph.Length == 1 && glyph[0] is >= '\u0391' and <= '\u03C9'
            ? "%" + word
            : word;
    }

    private bool IsStarMath(string source, int start)
    {
        var depth = 0;

        for (var index = start; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '{': depth++; break;
                case '}' when depth-- == 0: return false;
                case '#' when depth == 0 && index + 1 < source.Length && source[index + 1] == '#': return true;
                case '&' when depth == 0: return false;
            }
        }

        return false;
    }
}
