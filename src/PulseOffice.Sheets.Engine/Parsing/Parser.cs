using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Parsing;

public interface IFormulaExpressionParser
{
    FormulaNode Parse(string formula);

    FormulaNode Parse(string formula, string language, IFunctionNameVocabulary vocabulary);
}

public sealed class FormulaParser(IFormulaPunctuationConvention punctuation) : IFormulaExpressionParser
{
    public FormulaParser() : this(new FormulaPunctuationConvention()) { }

    public FormulaNode Parse(string formula) => Parser.Parse(formula, null, "en", punctuation.Canonical);

    public FormulaNode Parse(string formula, string language, IFunctionNameVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        return Parser.Parse(formula, vocabulary, language, punctuation.For(language));
    }
}

internal sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly IFunctionNameVocabulary? _vocabulary;
    private readonly string _language;
    private readonly FormulaPunctuation _punctuation;
    private int _index;
    private int _depth;

    public const int MaxDepth = 2048;

    private const int NestingCost = 8;

    private Parser(IReadOnlyList<Token> tokens, IFunctionNameVocabulary? vocabulary, string language, FormulaPunctuation punctuation)
    {
        _tokens = tokens;
        _vocabulary = vocabulary;
        _language = language;
        _punctuation = punctuation;
    }

    public static FormulaNode Parse(string formula, IFunctionNameVocabulary? vocabulary, string language, FormulaPunctuation punctuation)
    {
        var text = formula.TrimStart();
        if (text.StartsWith('=')) text = text[1..];

        var parser = new Parser(Lexer.Tokenize(text, punctuation.Decimal), vocabulary, language, punctuation);
        var node = parser.ParseComparison();
        if (parser.Current.Kind != TokenKind.EndOfInput)
            throw new FormulaSyntaxException($"Token inatteso '{parser.Current.Text}'", parser.Current.Position);
        return node;
    }

    private Token Current => _tokens[_index];
    private Token Advance() => _tokens[_index++];
    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind) return false;
        _index++;
        return true;
    }

    private FormulaNode ParseComparison()
    {
        var entered = _depth;
        Deepen(NestingCost);
        try
        {
            return ParseComparisonChain();
        }
        finally
        {
            _depth = entered;
        }
    }

    private void Deepen(int cost = 1)
    {
        _depth += cost;
        if (_depth > MaxDepth)
            throw new FormulaSyntaxException("Formula nested too deeply", Current.Position);
    }

    private FormulaNode ParseComparisonChain()
    {
        var left = ParseConcat();
        while (true)
        {
            var op = Current.Kind switch
            {
                TokenKind.Equal => BinaryOperator.Equal,
                TokenKind.NotEqual => BinaryOperator.NotEqual,
                TokenKind.Less => BinaryOperator.Less,
                TokenKind.LessOrEqual => BinaryOperator.LessOrEqual,
                TokenKind.Greater => BinaryOperator.Greater,
                TokenKind.GreaterOrEqual => BinaryOperator.GreaterOrEqual,
                _ => (BinaryOperator?)null,
            };
            if (op is null) return left;
            Advance();
            Deepen();
            left = new BinaryNode(op.Value, left, ParseConcat());
        }
    }

    private FormulaNode ParseConcat()
    {
        var left = ParseAdditive();
        while (Match(TokenKind.Concat) && Deepened())
            left = new BinaryNode(BinaryOperator.Concat, left, ParseAdditive());
        return left;
    }

    private FormulaNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (true)
        {
            if (Match(TokenKind.Plus) && Deepened()) left = new BinaryNode(BinaryOperator.Add, left, ParseMultiplicative());
            else if (Match(TokenKind.Minus) && Deepened()) left = new BinaryNode(BinaryOperator.Subtract, left, ParseMultiplicative());
            else return left;
        }
    }

    private FormulaNode ParseMultiplicative()
    {
        var left = ParsePower();
        while (true)
        {
            if (Match(TokenKind.Multiply) && Deepened()) left = new BinaryNode(BinaryOperator.Multiply, left, ParsePower());
            else if (Match(TokenKind.Divide) && Deepened()) left = new BinaryNode(BinaryOperator.Divide, left, ParsePower());
            else return left;
        }
    }

    private FormulaNode ParsePower()
    {
        var left = ParseUnary();
        while (Match(TokenKind.Power) && Deepened()) left = new BinaryNode(BinaryOperator.Power, left, ParseUnary());
        return left;
    }

    private FormulaNode ParseUnary()
    {
        if (Current.Kind is TokenKind.Minus or TokenKind.Plus or TokenKind.At) Deepen();
        if (Match(TokenKind.Minus)) return new UnaryNode(UnaryOperator.Minus, ParseUnary());
        if (Match(TokenKind.Plus)) return new UnaryNode(UnaryOperator.Plus, ParseUnary());
        if (Match(TokenKind.At)) return new UnaryNode(UnaryOperator.Single, ParseUnary());
        return ParsePostfix();
    }

    private FormulaNode ParsePostfix()
    {
        var node = ParsePrimary();
        while (Match(TokenKind.Tilde) && Deepened())
            node = new BinaryNode(BinaryOperator.ReferenceConcat, node, ParsePrimary());
        while (Match(TokenKind.Percent) && Deepened())
            node = new UnaryNode(UnaryOperator.Percent, node);
        return node;
    }

    private FormulaNode ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Advance();
                return new LiteralNode(CellValue.Number(
                    double.Parse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture)));

            case TokenKind.String:
                Advance();
                return new LiteralNode(CellValue.Text(token.Text));

            case TokenKind.Boolean:
                Advance();
                return new LiteralNode(CellValue.Boolean(
                    string.Equals(token.Text, "TRUE", StringComparison.OrdinalIgnoreCase)));

            case TokenKind.Error:
                Advance();
                return new LiteralNode(CellValue.FromError(ParseErrorCode(token.Text)));

            case TokenKind.OpenParen:
                Advance();
                var inner = ParseComparison();
                Expect(TokenKind.CloseParen, ")");
                return inner;

            case TokenKind.OpenBrace:
                return ParseArray();

            case TokenKind.Reference:
                Advance();
                return ParseReferenceOrRange(token);

            case TokenKind.WholeRange:
                Advance();
                return DecodeWholeRange(token);

            case TokenKind.SpillReference:
                {
                    Advance();
                    var (sheet, address, absoluteColumn, absoluteRow) = DecodeReference(token);
                    return new SpillReferenceNode(sheet, address) { AbsoluteColumn = absoluteColumn, AbsoluteRow = absoluteRow };
                }

            case TokenKind.Identifier:
                Advance();
                if (Current.Kind != TokenKind.OpenParen) return new NameNode(token.Text);
                FormulaNode call = ParseFunctionCall(token.Text);
                while (Current.Kind == TokenKind.OpenParen && Deepened()) call = new InvocationNode(call, ParseArguments());
                return call;

            default:
                throw new FormulaSyntaxException($"Expected an expression, found '{token.Text}'", token.Position);
        }
    }

    private FormulaNode ParseReferenceOrRange(Token first)
    {
        var (sheet, address, absCol, absRow) = DecodeReference(first);

        if (Current.Kind != TokenKind.Colon)
            return new ReferenceNode(sheet, address, absCol, absRow);

        Advance();
        if (Current.Kind != TokenKind.Reference)
            throw new FormulaSyntaxException("Expected the end of the range", Current.Position);

        var second = Advance();
        var (sheet2, address2, absCol2, absRow2) = DecodeReference(second);

        var lastSheet = sheet is not null && sheet2 is not null && !string.Equals(sheet, sheet2, StringComparison.OrdinalIgnoreCase) ? sheet2 : null;
        return new RangeNode(sheet, new CellRange(address, address2), absCol, absRow, absCol2, absRow2) { LastSheetName = lastSheet };
    }

    private RangeNode DecodeWholeRange(Token token)
    {
        var text = token.Text;
        string? sheet = null;
        var colon = text.LastIndexOf(':');
        var separator = text.LastIndexOfAny(['!', '.'], colon);
        if (separator >= 0)
        {
            sheet = text[..separator];
            if (sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\'') sheet = sheet[1..^1].Replace("''", "'", StringComparison.Ordinal);
            text = text[(separator + 1)..];
            colon = text.IndexOf(':');
        }

        var first = text[..colon];
        var second = text[(colon + 1)..];
        var firstAbsolute = first.StartsWith('$');
        var secondAbsolute = second.StartsWith('$');
        first = first.TrimStart('$');
        second = second.TrimStart('$');

        if (char.IsAsciiDigit(first[0]))
        {
            var top = int.Parse(first, CultureInfo.InvariantCulture) - 1;
            var bottom = int.Parse(second, CultureInfo.InvariantCulture) - 1;
            if (top < 0 || bottom < 0 || top >= CellAddress.MaxRows || bottom >= CellAddress.MaxRows)
                throw new FormulaSyntaxException($"Invalid reference '{token.Text}'", token.Position);
            return new RangeNode(sheet,
                new CellRange(new CellAddress(Math.Min(top, bottom), 0), new CellAddress(Math.Max(top, bottom), CellAddress.MaxColumns - 1)),
                true, firstAbsolute, true, secondAbsolute);
        }

        var left = CellAddress.ParseColumn(first);
        var right = CellAddress.ParseColumn(second);
        if (left < 0 || right < 0 || left >= CellAddress.MaxColumns || right >= CellAddress.MaxColumns)
            throw new FormulaSyntaxException($"Invalid reference '{token.Text}'", token.Position);
        return new RangeNode(sheet,
            new CellRange(new CellAddress(0, Math.Min(left, right)), new CellAddress(CellAddress.MaxRows - 1, Math.Max(left, right))),
            firstAbsolute, true, secondAbsolute, true);
    }

    private (string? Sheet, CellAddress Address, bool AbsoluteColumn, bool AbsoluteRow) DecodeReference(Token token)
    {
        var text = token.Text.AsSpan();
        string? sheet = null;

        var separator = text.LastIndexOfAny('!', '.');
        if (separator >= 0)
        {
            sheet = text[..separator].ToString();
            text = text[(separator + 1)..];
        }

        var absoluteColumn = text.Length > 0 && text[0] == '$';
        var afterColumn = absoluteColumn ? text[1..] : text;
        var letters = 0;
        while (letters < afterColumn.Length && char.IsAsciiLetter(afterColumn[letters])) letters++;
        var absoluteRow = letters < afterColumn.Length && afterColumn[letters] == '$';

        if (!CellAddress.TryParseA1(text, out var address))
            throw new FormulaSyntaxException($"Invalid reference '{token.Text}'", token.Position);

        return (sheet, address, absoluteColumn, absoluteRow);
    }

    private FormulaNode ParseFunctionCall(string name)
    {
        var arguments = ParseArguments();
        var canonical = _vocabulary?.ToCanonical(name, _language) ?? name;
        return new FunctionNode(canonical.ToUpperInvariant(), arguments);
    }

    private List<FormulaNode> ParseArguments()
    {
        Expect(TokenKind.OpenParen, "(");
        var arguments = new List<FormulaNode>();

        if (Current.Kind != TokenKind.CloseParen)
        {
            while (true)
            {
                arguments.Add(IsArgumentSeparator(Current.Kind) || Current.Kind == TokenKind.CloseParen
                    ? new MissingArgumentNode()
                    : ParseComparison());

                if (IsArgumentSeparator(Current.Kind)) { Advance(); continue; }
                break;
            }
        }

        Expect(TokenKind.CloseParen, ")");
        return arguments;
    }

    private FormulaNode ParseArray()
    {
        Expect(TokenKind.OpenBrace, "{");
        var rows = new List<IReadOnlyList<FormulaNode>>();
        var current = new List<FormulaNode>();

        while (Current.Kind != TokenKind.CloseBrace)
        {
            current.Add(ParseComparison());
            if (IsArrayColumnSeparator(Current.Kind)) { Advance(); continue; }

            if (IsArrayRowSeparator(Current.Kind))
            {
                Advance();
                rows.Add(current);
                current = [];
                continue;
            }
            break;
        }

        Expect(TokenKind.CloseBrace, "}");
        if (current.Count > 0) rows.Add(current);
        return new ArrayNode(rows);
    }

    private bool IsArgumentSeparator(TokenKind kind) =>
        kind is TokenKind.Comma or TokenKind.Semicolon;

    private bool IsArrayRowSeparator(TokenKind kind) =>
        kind == TokenKind.ArrayRowSeparator || (_punctuation.ArrayRowIsSemicolon && kind == TokenKind.Semicolon);

    private bool IsArrayColumnSeparator(TokenKind kind) =>
        IsArgumentSeparator(kind) && !IsArrayRowSeparator(kind);

    private bool Deepened()
    {
        Deepen();
        return true;
    }

    private void Expect(TokenKind kind, string what)
    {
        if (!Match(kind))
            throw new FormulaSyntaxException($"Atteso '{what}'", Current.Position);
    }

    private CellError ParseErrorCode(string code) => code.ToUpperInvariant() switch
    {
        "#NULL!" => CellError.Null,
        "#DIV/0!" => CellError.DivideByZero,
        "#VALUE!" => CellError.Value,
        "#REF!" => CellError.Reference,
        "#NAME?" => CellError.Name,
        "#NUM!" => CellError.Number,
        "#N/A" => CellError.NotAvailable,
        "#SPILL!" => CellError.Spill,
        "#CALC!" => CellError.Calc,
        _ => CellError.Value,
    };
}
