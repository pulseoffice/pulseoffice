using System.Text;

namespace PulseOffice.Data;

public sealed record SqlLexicalRules(
    bool BackslashEscapes,
    bool HashComments,
    bool DashCommentNeedsSpace,
    bool DollarQuotes,
    bool BracketIdentifiers,
    bool BacktickIdentifiers);

public sealed class SqlWordReader(SqlLexicalRules rules)
{
    private const string Literal = "'LITERAL'";
    private const string Identifier = "\"IDENTIFIER\"";

    public const string QuotedCall = "\"QUOTED CALL\"";

    public IReadOnlyList<string>? Words(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var words = new List<string>();
        var word = new StringBuilder();
        var index = 0;

        void Flush()
        {
            if (word.Length == 0) return;
            words.Add(word.ToString().ToUpperInvariant());
            word.Clear();
        }

        while (index < sql.Length)
        {
            var character = sql[index];
            if (IsDashComment(sql, index) || (rules.HashComments && character == '#'))
            {
                Flush();
                while (index < sql.Length && sql[index] != '\n') index++;
                continue;
            }

            if (character == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                if (index + 2 < sql.Length && sql[index + 2] == '!') return null;
                Flush();
                var end = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0) return null;
                index = end + 2;
                continue;
            }

            if (Closing(character) is { } closing)
            {
                Flush();
                index = SkipQuoted(sql, index + 1, closing, character is '\'' or '"' && rules.BackslashEscapes);
                if (index < 0) return null;
                words.Add(character == '\'' ? Literal : NextSignificant(sql, index) == '(' ? QuotedCall : Identifier);
                continue;
            }

            if (rules.DollarQuotes && character == '$' && DollarTagEnd(sql, index) is > 0 and var tagEnd)
            {
                Flush();
                var tag = sql[index..(tagEnd + 1)];
                var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                if (close < 0) return null;
                index = close + tag.Length;
                words.Add(Literal);
                continue;
            }

            if (char.IsLetterOrDigit(character) || character is '_' or '@' or '$')
            {
                word.Append(character);
                index++;
                continue;
            }

            Flush();
            if (character == ';') words.Add(";");
            index++;
        }

        Flush();
        return words;
    }

    private char? NextSignificant(string sql, int index)
    {
        while (index < sql.Length)
        {
            if (char.IsWhiteSpace(sql[index]))
            {
                index++;
                continue;
            }
            if (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                var end = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0) return null;
                index = end + 2;
                continue;
            }
            if (IsDashComment(sql, index) || (rules.HashComments && sql[index] == '#'))
            {
                while (index < sql.Length && sql[index] != '\n') index++;
                continue;
            }
            return sql[index];
        }
        return null;
    }

    private bool IsDashComment(string sql, int index)
    {
        if (sql[index] != '-' || index + 1 >= sql.Length || sql[index + 1] != '-') return false;
        return !rules.DashCommentNeedsSpace || index + 2 >= sql.Length || char.IsWhiteSpace(sql[index + 2]);
    }

    private char? Closing(char character) => character switch
    {
        '\'' or '"' => character,
        '`' when rules.BacktickIdentifiers => '`',
        '[' when rules.BracketIdentifiers => ']',
        _ => null,
    };

    private int DollarTagEnd(string sql, int index)
    {
        if (index + 1 >= sql.Length || !(sql[index + 1] == '$' || char.IsLetter(sql[index + 1]))) return -1;
        var tagEnd = sql.IndexOf('$', index + 1);
        return tagEnd > 0 && sql[(index + 1)..tagEnd].All(char.IsLetterOrDigit) ? tagEnd : -1;
    }

    private int SkipQuoted(string sql, int index, char closing, bool backslashEscapes)
    {
        while (index < sql.Length)
        {
            if (backslashEscapes && sql[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (sql[index] == closing)
            {
                if (index + 1 < sql.Length && sql[index + 1] == closing)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return -1;
    }
}
