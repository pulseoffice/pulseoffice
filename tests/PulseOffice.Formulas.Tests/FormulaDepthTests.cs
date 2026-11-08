using System.Xml.Linq;
using PulseOffice.Formulas;
using Xunit;

namespace PulseOffice.Formulas.Tests;

public sealed class FormulaDepthTests
{
    private static readonly XNamespace M = OmmlSyntax.Namespace;
    private static readonly XNamespace Ml = MathMlSyntax.Namespace;

    private static XElement Nested(XName name, int depth)
    {
        var inner = new XElement(name);
        for (var level = 0; level < depth; level++) inner = new XElement(name, inner);
        return inner;
    }

    private static T OnSmallStack<T>(Func<T> work)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception exception) { failure = exception; }
        }, 256 * 1024);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        return result;
    }

    [Fact]
    public void UnMathMlAnnidatoMoltoInProfonditaNonEsaurisceLoStack()
    {
        var math = new XElement(Ml + "math", Nested(Ml + "mrow", 100_000));

        var formula = OnSmallStack(() => new MathMlSyntax().Read(math));

        Assert.NotNull(formula.Root);
    }

    [Fact]
    public void UnOmmlAnnidatoMoltoInProfonditaNonEsaurisceLoStack()
    {
        var math = new XElement(M + "oMath", Nested(M + "e", 100_000));

        var formula = OnSmallStack(() => new OmmlSyntax().Read(math));

        Assert.NotNull(formula.Root);
    }

    [Fact]
    public void IlTestoNormaleOmmlRestaTestoEFaIlGiro()
    {
        var syntax = new OmmlSyntax();
        var written = syntax.Write(new MathFormula(new MathText("a over b")));

        var read = syntax.Read(written);

        Assert.Equal("a over b", Assert.IsType<MathText>(read.Root).Content);
    }
}
