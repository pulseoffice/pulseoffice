using System.Globalization;

namespace PulseOffice.Sheets.Engine.Parsing;

public enum TokenKind
{
    EndOfInput,
    Number, String, Boolean, Error,
    Identifier,
    Reference,
    WholeRange,
    SpillReference,
    Plus, Minus, Multiply, Divide, Power, Percent, Concat,
    Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual,
    OpenParen, CloseParen, OpenBrace, CloseBrace,
    Comma, Semicolon, Colon, Space, ArrayRowSeparator, Tilde, At,
}

public readonly record struct Token(TokenKind Kind, string Text, int Position)
{
    public override string ToString() => $"{Kind}('{Text}')";
}

public sealed class FormulaSyntaxException(string message, int position)
    : Exception($"{message} (position {position})")
{
    public int Position { get; } = position;
}

public sealed class Lexer(string input, char decimalSeparator = '.')
{
    private readonly string _text = input;
    private int _pos;

    private static readonly System.Text.RegularExpressions.Regex WholeRange = new(
        @"\G(?:(?:'(?:[^']|'')+'|[\p{L}_][\p{L}\p{Nd}_]*)[.!])?(?:\$?[A-Za-z]{1,3}:\$?[A-Za-z]{1,3}|\$?[0-9]{1,7}:\$?[0-9]{1,7})(?![A-Za-z0-9_(.!:'])",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static IReadOnlyList<Token> Tokenize(string formula, char decimalSeparator = '.')
    {
        var lexer = new Lexer(formula, decimalSeparator);
        var tokens = new List<Token>();
        Token token;
        do
        {
            token = lexer.Next();
            if (token.Kind != TokenKind.Space) tokens.Add(token);
        } while (token.Kind != TokenKind.EndOfInput);
        return tokens;
    }

    public Token Next()
    {
        if (_pos >= _text.Length) return new Token(TokenKind.EndOfInput, string.Empty, _pos);

        var start = _pos;
        var ch = _text[_pos];

        if (char.IsWhiteSpace(ch))
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            return new Token(TokenKind.Space, _text[start.._pos], start);
        }

        if (WholeRange.Match(_text, _pos) is { Success: true } whole)
        {
            _pos += whole.Length;
            return new Token(TokenKind.WholeRange, whole.Value, start);
        }

        switch (ch)
        {
            case '+': _pos++; return new Token(TokenKind.Plus, "+", start);
            case '-': _pos++; return new Token(TokenKind.Minus, "-", start);
            case '*': _pos++; return new Token(TokenKind.Multiply, "*", start);
            case '/': _pos++; return new Token(TokenKind.Divide, "/", start);
            case '^': _pos++; return new Token(TokenKind.Power, "^", start);
            case '%': _pos++; return new Token(TokenKind.Percent, "%", start);
            case '&': _pos++; return new Token(TokenKind.Concat, "&", start);
            case '=': _pos++; return new Token(TokenKind.Equal, "=", start);
            case '(': _pos++; return new Token(TokenKind.OpenParen, "(", start);
            case ')': _pos++; return new Token(TokenKind.CloseParen, ")", start);
            case '{': _pos++; return new Token(TokenKind.OpenBrace, "{", start);
            case '}': _pos++; return new Token(TokenKind.CloseBrace, "}", start);
            case ',': _pos++; return new Token(TokenKind.Comma, ",", start);
            case ';': _pos++; return new Token(TokenKind.Semicolon, ";", start);
            case ':': _pos++; return new Token(TokenKind.Colon, ":", start);
            case '|': _pos++; return new Token(TokenKind.ArrayRowSeparator, "|", start);
            case '~': _pos++; return new Token(TokenKind.Tilde, "~", start);
            case '@': _pos++; return new Token(TokenKind.At, "@", start);

            case '<':
                _pos++;
                if (_pos < _text.Length && _text[_pos] == '>') { _pos++; return new Token(TokenKind.NotEqual, "<>", start); }
                if (_pos < _text.Length && _text[_pos] == '=') { _pos++; return new Token(TokenKind.LessOrEqual, "<=", start); }
                return new Token(TokenKind.Less, "<", start);

            case '>':
                _pos++;
                if (_pos < _text.Length && _text[_pos] == '=') { _pos++; return new Token(TokenKind.GreaterOrEqual, ">=", start); }
                return new Token(TokenKind.Greater, ">", start);

            case '"': return ReadString(start);
            case '#': return ReadError(start);
            case '\'': return ReadQuotedSheetReference(start);
        }

        if (char.IsAsciiDigit(ch) || (ch == decimalSeparator && _pos + 1 < _text.Length && char.IsAsciiDigit(_text[_pos + 1])))
            return ReadNumber(start);

        if (char.IsLetter(ch) || ch is '_' or '$')
            return ReadWord(start);

        throw new FormulaSyntaxException($"Unrecognised character '{ch}'", start);
    }

    private Token ReadString(int start)
    {
        _pos++;
        var sb = new System.Text.StringBuilder();
        while (_pos < _text.Length)
        {
            if (_text[_pos] == '"')
            {
                if (_pos + 1 < _text.Length && _text[_pos + 1] == '"') { sb.Append('"'); _pos += 2; continue; }
                _pos++;
                return new Token(TokenKind.String, sb.ToString(), start);
            }
            sb.Append(_text[_pos++]);
        }
        throw new FormulaSyntaxException("Unterminated string", start);
    }

    private Token ReadError(int start)
    {
        foreach (var code in (string[])["#NULL!", "#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A", "#SPILL!", "#CALC!"])
        {
            if (_text.AsSpan(_pos).StartsWith(code, StringComparison.OrdinalIgnoreCase))
            {
                _pos += code.Length;
                return new Token(TokenKind.Error, code, start);
            }
        }
        throw new FormulaSyntaxException("Unrecognised error code", start);
    }

    private Token ReadQuotedSheetReference(int start)
    {
        _pos++;
        var sb = new System.Text.StringBuilder();
        while (_pos < _text.Length)
        {
            if (_text[_pos] == '\'')
            {
                if (_pos + 1 < _text.Length && _text[_pos + 1] == '\'') { sb.Append('\''); _pos += 2; continue; }
                break;
            }
            sb.Append(_text[_pos++]);
        }
        if (_pos >= _text.Length) throw new FormulaSyntaxException("Unterminated sheet name", start);
        _pos++;

        if (_pos < _text.Length && (_text[_pos] == '.' || _text[_pos] == '!'))
        {
            _pos++;
            var refStart = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '$')) _pos++;
            return new Token(TokenKind.Reference, $"{sb}!{_text[refStart.._pos]}", start);
        }
        throw new FormulaSyntaxException("Expected a reference after the sheet name", _pos);
    }

    private Token ReadNumber(int start)
    {
        while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos])) _pos++;
        if (_pos + 1 < _text.Length && _text[_pos] == decimalSeparator && char.IsAsciiDigit(_text[_pos + 1])
            || _pos < _text.Length && decimalSeparator == '.' && _text[_pos] == '.')
        {
            _pos++;
            while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos])) _pos++;
        }
        if (_pos < _text.Length && (_text[_pos] is 'e' or 'E'))
        {
            var save = _pos;
            _pos++;
            if (_pos < _text.Length && _text[_pos] is '+' or '-') _pos++;
            if (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
                while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos])) _pos++;
            else
                _pos = save;
        }

        var text = _text[start.._pos].Replace(decimalSeparator, '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            throw new FormulaSyntaxException($"Invalid number '{text}'", start);
        return new Token(TokenKind.Number, text, start);
    }

    private Token ReadWord(int start)
    {
        while (_pos < _text.Length &&
               (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] is '_' or '$' or '.'))
            _pos++;

        var text = _text[start.._pos];

        var isCall = _pos < _text.Length && _text[_pos] == '(';

        if (!isCall && (string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase)))
            return new Token(TokenKind.Boolean, text, start);

        if (!isCall && LooksLikeReference(text))
        {
            if (_pos < _text.Length && _text[_pos] == '#')
            {
                _pos++;
                return new Token(TokenKind.SpillReference, text, start);
            }
            return new Token(TokenKind.Reference, text, start);
        }

        return new Token(TokenKind.Identifier, text, start);
    }

    private static bool LooksLikeReference(ReadOnlySpan<char> text)
    {
        var dot = text.LastIndexOf('.');
        if (dot >= 0) text = text[(dot + 1)..];

        var i = 0;
        if (i < text.Length && text[i] == '$') i++;
        var letters = 0;
        var firstLetter = i;
        while (i < text.Length && char.IsAsciiLetter(text[i])) { i++; letters++; }
        if (letters is 0 or > 3) return false;
        if (Sheets.Addressing.CellAddress.ParseColumn(text.Slice(firstLetter, letters)) is < 0 or >= Sheets.Addressing.CellAddress.MaxColumns) return false;
        if (i < text.Length && text[i] == '$') i++;
        var digits = 0;
        var row = 0L;
        while (i < text.Length && char.IsAsciiDigit(text[i])) { row = row * 10 + (text[i] - '0'); i++; digits++; }
        return digits is > 0 and <= 7 && i == text.Length && row is >= 1 and <= Sheets.Addressing.CellAddress.MaxRows;
    }
}
