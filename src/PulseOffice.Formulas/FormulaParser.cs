using System.Text;

namespace PulseOffice.Formulas;

public interface IFormulaParser
{
    MathFormula Parse(string source);

    ParsedFormula ParseWithSpans(string source);
}

public sealed class FormulaParser : IFormulaParser
{
    private readonly IFormulaVocabulary _vocabulary;

    public FormulaParser() : this(FormulaVocabulary.Default) { }

    public FormulaParser(IFormulaVocabulary vocabulary) => _vocabulary = vocabulary;

    public MathFormula Parse(string source) => ParseWithSpans(source).Formula;

    public ParsedFormula ParseWithSpans(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Session(_vocabulary).Run(source);
    }

    private sealed class Session(IFormulaVocabulary vocabulary)
    {
        private const int MaxDepth = 256;

        private const string PlaceholderMark = "<?>";

        private const string PlaceholderGlyph = "\u25A1";

        private readonly IFormulaVocabulary _vocabulary = vocabulary;
        private int _depth;

        private IReadOnlyDictionary<string, string> Symbols => _vocabulary.Symbols;

        private IReadOnlyDictionary<string, string> Large => _vocabulary.LargeOperators;

        private IReadOnlySet<string> Functions => _vocabulary.Functions;

        private List<Token> _tokens = [];
        private int _position;
        private FormulaSpans _spans = new();

        public ParsedFormula Run(string source)
        {
            _tokens = [.. Tokenise(source, _vocabulary.CompoundOperators)];
            _position = 0;
            _spans = new FormulaSpans();

            var start = _position;
            var root = ParseTopLevel();

            Record(start, root);

            return new ParsedFormula(new MathFormula(root) { Source = source }, _spans);
        }

        private T? Record<T>(int firstToken, T? node)
        where T : MathNode
        {
            if (node is null || firstToken >= _tokens.Count || _position <= firstToken) return node;

            var start = _tokens[firstToken].Start;
            var end = _tokens[_position - 1].End;

            _spans.Record(node, new FormulaSpan(start, end - start));

            return node;
        }

        private MathNode ParseTopLevel()
        {
            var opening = _position;
            var children = new List<MathNode>();

            while (_position < _tokens.Count)
            {
                if (_tokens[_position].Kind == TokenKind.Close)
                {
                    _position++;
                    continue;
                }

                var node = ParseSequence();

                if (node is MathRow row) children.AddRange(row.Children);
                else children.Add(node);
            }

            return children.Count == 1 ? children[0] : Record(opening, new MathRow(children))!;
        }

        private MathNode ParseSequence(string? stop = null)
        {
            var opening = _position;

            var children = new List<MathNode>();
            var starts = new List<int>();

            while (_position < _tokens.Count)
            {
                var token = _tokens[_position];

                if (stop is not null && token.Kind == TokenKind.Word && token.Text == stop) break;
                if (token.Kind == TokenKind.Close) break;

                if (token.Kind == TokenKind.Word && token.Text == "over" && children.Count > 0)
                {
                    var numeratorStart = starts[^1];
                    _position++;
                    var denominator = ParseScripted() ?? MathRow.Empty;
                    var numerator = children[^1];
                    children[^1] = Record(numeratorStart, new MathFraction(numerator, denominator))!;
                    continue;
                }

                var start = _position;
                var node = ParseScripted();
                if (node is null) break;

                children.Add(node);
                starts.Add(start);
            }

            return children.Count == 1 ? children[0] : Record(opening, new MathRow(children))!;
        }

        private MathNode? ParseScripted()
        {
            var opening = _position;
            var value = ParseTerm();

            if (value is null)
            {
                if (_position >= _tokens.Count || _tokens[_position].Kind != TokenKind.Script) return null;

                value = MathRow.Empty;
            }

            MathNode? lower = null;
            MathNode? upper = null;

            while (_position < _tokens.Count)
            {
                var token = _tokens[_position];

                if (token.Kind != TokenKind.Script) break;

                _position++;

                var script = ParseTerm() ?? MathRow.Empty;

                if (token.Text == "_") lower = script;
                else upper = script;
            }

            return Record(opening, (lower, upper) switch
            {
                (null, null) => value,
                (not null, null) => new MathSubscript(value, lower),
                (null, not null) => new MathSuperscript(value, upper),

                _ => new MathSubSuperscript(value, lower!, upper!),
            });
        }

        private MathNode? ParseTerm()
        {
            if (_depth >= MaxDepth)
            {
                if (_position >= _tokens.Count) return null;

                var token = _tokens[_position++];
                return new MathText(token.Text);
            }

            _depth++;

            try
            {
                return Record(_position, ParseTermCore());
            }
            finally
            {
                _depth--;
            }
        }

        private MathNode? ParseTermCore()
        {
            if (_position >= _tokens.Count) return null;

            var token = _tokens[_position];

            switch (token.Kind)
            {
                case TokenKind.Number:
                    _position++;
                    return new MathNumber(token.Text);

                case TokenKind.Operator:
                    _position++;
                    return new MathOperator(token.Text);

                case TokenKind.Quoted:
                    _position++;
                    return new MathText(token.Text);

                case TokenKind.Symbol:
                    _position++;
                    return SymbolNamed(token.Text);

                case TokenKind.Open:
                    _position++;
                    var content = ParseSequence();
                    var close = Closing(token.Text);

                    if (_position < _tokens.Count && _tokens[_position].Kind == TokenKind.Close) _position++;

                    return token.Text == "{" ? content : new MathFenced(content, token.Text, close);

                case TokenKind.Close:
                    return null;

                case TokenKind.Script:
                    return null;
            }

            _position++;

            return Word(token.Text);
        }

        private MathNode SymbolNamed(string name)
        {
            var key = Symbols.ContainsKey(name) || name.Length < 2 || name[0] != 'i' ? name : name[1..];

            return Symbols.TryGetValue(key, out var glyph) ? Glyph(key, glyph) : new MathIdentifier("%" + name);
        }

        private MathNode Glyph(string word, string glyph) =>
            word.Length > 0 && char.IsUpper(word[0]) || glyph.Length > 0 && char.IsLetter(glyph[0])
                ? new MathIdentifier(glyph)
                : new MathOperator(glyph);

        private MathNode Word(string word)
        {
            switch (word)
            {
                case "over":
                    return new MathOperator("/");

                case "left":
                    return ParseDelimited();

                case "frac":
                {
                    var numerator = ParseTerm() ?? MathRow.Empty;
                    var denominator = ParseTerm() ?? MathRow.Empty;

                    return new MathFraction(numerator, denominator);
                }

                case "sqrt":
                    return new MathRoot(ParseTerm() ?? MathRow.Empty);

                case "nroot":
                {
                    var index = ParseTerm() ?? MathRow.Empty;
                    var radicand = ParseTerm() ?? MathRow.Empty;

                    return new MathRoot(radicand, index);
                }

                case var accent when _vocabulary.Accents.ContainsKey(accent):
                    return new MathAccent(ParseTerm() ?? MathRow.Empty, _vocabulary.Accents[word]);

                case "matrix":
                    return ParseMatrix();
            }

            if (Large.TryGetValue(word, out var symbol))
            {
                MathNode? lower = null;
                MathNode? upper = null;

                while (_position < _tokens.Count && _tokens[_position].Kind == TokenKind.Script)
                {
                    var script = _tokens[_position].Text;
                    _position++;

                    var value = ParseTerm() ?? MathRow.Empty;

                    if (script == "_") lower = value;
                    else upper = value;
                }

                return new MathLargeOperator(symbol, ParseScripted() ?? MathRow.Empty, lower, upper);
            }

            if (Functions.Contains(word))
                return new MathFunction(word, ParseScripted() ?? MathRow.Empty);

            if (Symbols.TryGetValue(word, out var glyph))
                return Glyph(word, glyph);

            return new MathIdentifier(word);
        }

        private MathNode ParseDelimited()
        {
            var open = Delimiter();
            var content = ParseSequence("right");
            var close = string.Empty;

            if (_position < _tokens.Count && _tokens[_position].Kind == TokenKind.Word && _tokens[_position].Text == "right")
            {
                _position++;
                close = Delimiter();
            }

            return new MathFenced(content, open, close);
        }

        private string Delimiter()
        {
            if (_position >= _tokens.Count) return string.Empty;

            var token = _tokens[_position];

            if (token.Kind is TokenKind.Open or TokenKind.Close or TokenKind.Operator)
            {
                _position++;
                return token.Text;
            }

            if (token.Kind == TokenKind.Word && _vocabulary.Delimiters.TryGetValue(token.Text, out var glyph))
            {
                _position++;
                return glyph;
            }

            return string.Empty;
        }

        private MathNode ParseMatrix()
        {
            var rows = new List<IReadOnlyList<MathNode>>();

            if (_position >= _tokens.Count || _tokens[_position].Kind != TokenKind.Open) return new MathMatrix(rows);

            _position++;

            var starMath = UsesStarMathRows();
            var current = new List<MathNode>();
            var cell = new List<MathNode>();

            void EndCell()
            {
                current.Add(cell.Count == 1 ? cell[0] : new MathRow([.. cell]));
                cell = [];
            }

            while (_position < _tokens.Count && _tokens[_position].Kind != TokenKind.Close)
            {
                if (IsOperator(_position, "#"))
                {
                    _position++;

                    if (!starMath || IsOperator(_position, "#"))
                    {
                        if (starMath) _position++;
                        if (starMath || cell.Count > 0) EndCell();
                        rows.Add(current);
                        current = [];
                        continue;
                    }

                    EndCell();
                    continue;
                }

                if (!starMath && IsOperator(_position, "&"))
                {
                    _position++;
                    if (cell.Count > 0) EndCell();
                    continue;
                }

                var node = ParseScripted();
                if (node is null) break;

                cell.Add(node);
            }

            if (_position < _tokens.Count) _position++;
            if (cell.Count > 0 || (starMath && current.Count > 0)) EndCell();
            if (current.Count > 0) rows.Add(current);

            return new MathMatrix(rows);
        }

        private bool IsOperator(int index, string text) =>
            index < _tokens.Count && _tokens[index].Kind == TokenKind.Operator && _tokens[index].Text == text;

        private bool UsesStarMathRows()
        {
            var depth = 0;

            for (var index = _position; index < _tokens.Count; index++)
            {
                var kind = _tokens[index].Kind;

                if (kind == TokenKind.Open) depth++;
                else if (kind == TokenKind.Close && depth-- == 0) return false;
                else if (depth == 0 && IsOperator(index, "#") && IsOperator(index + 1, "#")) return true;
                else if (depth == 0 && IsOperator(index, "&")) return false;
            }

            return false;
        }

        private static string Closing(string open) => open switch
        {
            "(" => ")",
            "[" => "]",
            "{" => "}",
            "|" => "|",

            _ => ")",
        };

        private enum TokenKind { Number, Word, Operator, Open, Close, Script, Quoted, Symbol }

        private readonly record struct Token(TokenKind Kind, string Text, int Start, int End);

        private static bool IsLetterPair(string source, int index) =>
            char.IsSurrogatePair(source, index) && Rune.IsLetter(Rune.GetRuneAt(source, index));

        private static IEnumerable<Token> Tokenise(string source, IReadOnlyDictionary<string, string> compounds)
        {
            var index = 0;

            while (index < source.Length)
            {
                var character = source[index];

                if (char.IsWhiteSpace(character)) { index++; continue; }

                if (char.IsDigit(character))
                {
                    var start = index;

                    while (index < source.Length && (char.IsDigit(source[index]) || source[index] == '.'))
                        index++;

                    yield return new Token(TokenKind.Number, source[start..index], start, index);
                    continue;
                }

                if (char.IsLetter(source, index) || IsLetterPair(source, index))
                {
                    var start = index;

                    while (index < source.Length && (char.IsLetter(source, index) || IsLetterPair(source, index)))
                        index += char.IsSurrogatePair(source, index) ? 2 : 1;

                    yield return new Token(TokenKind.Word, source[start..index], start, index);
                    continue;
                }

                if (character == '%' && index + 1 < source.Length && char.IsLetter(source[index + 1]))
                {
                    var start = index;
                    index++;

                    while (index < source.Length && char.IsLetter(source[index])) index++;

                    yield return new Token(TokenKind.Symbol, source[(start + 1)..index], start, index);
                    continue;
                }

                if (string.CompareOrdinal(source, index, PlaceholderMark, 0, PlaceholderMark.Length) == 0)
                {
                    yield return new Token(TokenKind.Quoted, PlaceholderGlyph, index, index + PlaceholderMark.Length);
                    index += PlaceholderMark.Length;
                    continue;
                }

                if (character == '"')
                {
                    var quoteStart = index;
                    var text = new StringBuilder();
                    index++;

                    while (index < source.Length && source[index] != '"') text.Append(source[index++]);

                    if (index < source.Length) index++;

                    yield return new Token(TokenKind.Quoted, text.ToString(), quoteStart, index);
                    continue;
                }

                var symbolStart = index;

                if (index + 1 < source.Length && compounds.TryGetValue(source.Substring(index, 2), out var compound))
                {
                    index += 2;
                    yield return new Token(TokenKind.Operator, compound, symbolStart, index);
                    continue;
                }

                index += char.IsSurrogatePair(source, index) ? 2 : 1;

                yield return character switch
                {
                    '_' or '^' => new Token(TokenKind.Script, character.ToString(), symbolStart, index),
                    '(' or '[' or '{' => new Token(TokenKind.Open, character.ToString(), symbolStart, index),
                    ')' or ']' or '}' => new Token(TokenKind.Close, character.ToString(), symbolStart, index),

                    _ => new Token(TokenKind.Operator, source[symbolStart..index], symbolStart, index),
                };
            }
        }
    }
}
