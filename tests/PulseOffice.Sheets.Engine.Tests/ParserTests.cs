using PulseOffice.Sheets.Engine.Parsing;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ParserTests
{
    [Theory]
    [InlineData("1+2*3", "(1 + (2 * 3))")]
    [InlineData("(1+2)*3", "((1 + 2) * 3)")]
    [InlineData("1-2-3", "((1 - 2) - 3)")]
    [InlineData("2^3^2", "((2 ^ 3) ^ 2)")]
    [InlineData("2^(3^2)", "(2 ^ (3 ^ 2))")]
    [InlineData("-2^2", "(-(2) ^ 2)")]
    [InlineData("-(2^2)", "-((2 ^ 2))")]
    [InlineData("1<2", "(1 < 2)")]
    [InlineData("1&2", "(1 & 2)")]
    [InlineData("1+2<3*4", "((1 + 2) < (3 * 4))")]
    public void RispettaLePrecedenze(string formula, string atteso)
    {
        Assert.Equal(atteso, Dump(new FormulaParser().Parse(formula)));
    }

    [Fact]
    public void AccettaSiaVirgolaSiaPuntoEVirgola()
    {
        var conVirgola = (FunctionNode)new FormulaParser().Parse("SUM(1,2,3)");
        var conPuntoEVirgola = (FunctionNode)new FormulaParser().Parse("SUM(1;2;3)");
        Assert.Equal(3, conVirgola.Arguments.Count);
        Assert.Equal(3, conPuntoEVirgola.Arguments.Count);
    }

    [Fact]
    public void ConservaLAssolutezzaDeiRiferimenti()
    {
        var node = (ReferenceNode)new FormulaParser().Parse("$B$7");
        Assert.True(node.AbsoluteColumn);
        Assert.True(node.AbsoluteRow);
        Assert.Equal(6, node.Address.Row);
        Assert.Equal(1, node.Address.Column);

        var misto = (ReferenceNode)new FormulaParser().Parse("$B7");
        Assert.True(misto.AbsoluteColumn);
        Assert.False(misto.AbsoluteRow);
    }

    [Fact]
    public void RiconosceIntervalliEFogli()
    {
        var range = (RangeNode)new FormulaParser().Parse("A1:B10");
        Assert.Equal("A1:B10", range.Range.ToString());

        var conFoglio = (RangeNode)new FormulaParser().Parse("Foglio2.A1:A5");
        Assert.Equal("Foglio2", conFoglio.SheetName);

        var conApici = (ReferenceNode)new FormulaParser().Parse("'Foglio con spazi'.C3");
        Assert.Equal("Foglio con spazi", conApici.SheetName);
    }

    [Fact]
    public void RaddoppiaGliApiciDentroLeStringhe()
    {
        var node = (LiteralNode)new FormulaParser().Parse("\"dice \"\"ciao\"\"\"");
        Assert.Equal("dice \"ciao\"", node.Value.AsText());
    }

    [Fact]
    public void ArgomentiOmessiDiventanoNodiMancanti()
    {
        var node = (FunctionNode)new FormulaParser().Parse("IF(A1;1;)");
        Assert.Equal(3, node.Arguments.Count);
        Assert.IsType<MissingArgumentNode>(node.Arguments[2]);
    }

    [Fact]
    public void SegnalaLaPosizioneDellErrore()
    {
        var ex = Assert.Throws<FormulaSyntaxException>(() => new FormulaParser().Parse("1+*2"));
        Assert.Equal(2, ex.Position);
    }

    [Fact]
    public void StringaNonChiusaEUnErrore() =>
        Assert.Throws<FormulaSyntaxException>(() => new FormulaParser().Parse("\"senza fine"));

    private static string Dump(FormulaNode node) => node switch
    {
        LiteralNode l => l.Value.ToString(),
        ReferenceNode r => r.Address.ToA1(r.AbsoluteColumn, r.AbsoluteRow),
        RangeNode r => r.Range.ToString(),
        NameNode n => n.Name,
        UnaryNode u => u.Operator switch
        {
            UnaryOperator.Minus => $"-({Dump(u.Operand)})",
            UnaryOperator.Plus => $"+({Dump(u.Operand)})",
            _ => $"({Dump(u.Operand)}%)",
        },
        BinaryNode b => $"({Dump(b.Left)} {Symbol(b.Operator)} {Dump(b.Right)})",
        FunctionNode f => $"{f.Name}({string.Join(", ", f.Arguments.Select(Dump))})",
        _ => "?",
    };

    private static string Symbol(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Power => "^",
        BinaryOperator.Concat => "&",
        BinaryOperator.Equal => "=",
        BinaryOperator.NotEqual => "<>",
        BinaryOperator.Less => "<",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.Greater => ">",
        _ => ">=",
    };
}
