using PulseOffice.Scripting;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public class ScriptTokenizerTests
{
    private readonly ScriptTokenizer _tokenizer = new();

    private static readonly IReadOnlySet<string> Api = new HashSet<string>(StringComparer.Ordinal) { "sheet" };

    private IReadOnlyList<ScriptToken> Tokens(string source, ScriptLanguage language) =>
        _tokenizer.Tokenize(source, language, Api);

    private string TextOf(string source, ScriptToken token) =>
        source.Substring(token.Start, token.Length);

    [Fact]
    public void UnCommentoDiRigaArrivaFinoAllaFine()
    {
        const string source = "// nota\nsheet";

        var comment = Tokens(source, ScriptLanguage.JavaScript)
            .First(token => token.Kind == ScriptTokenKind.Comment);

        Assert.Equal("// nota", TextOf(source, comment));
    }

    [Fact]
    public void ILCancellettoECommentoSoloInPython()
    {
        Assert.Contains(Tokens("# nota", ScriptLanguage.Python),
            token => token.Kind == ScriptTokenKind.Comment);

        Assert.DoesNotContain(Tokens("# nota", ScriptLanguage.JavaScript),
            token => token.Kind == ScriptTokenKind.Comment);
    }

    [Fact]
    public void UnaStringaSiChiudeSullaVirgolettaGiusta()
    {
        const string source = "\"ciao 'mondo'\"";

        var text = Tokens(source, ScriptLanguage.JavaScript)
            .First(token => token.Kind == ScriptTokenKind.Text);

        Assert.Equal(source, TextOf(source, text));
    }

    [Fact]
    public void LApiceSfuggitoNonChiudeLaStringa()
    {
        const string source = "\"a\\\"b\"";

        var text = Tokens(source, ScriptLanguage.JavaScript)
            .First(token => token.Kind == ScriptTokenKind.Text);

        Assert.Equal(source, TextOf(source, text));
    }

    [Fact]
    public void LeParoleChiaveDipendonoDalLinguaggio()
    {
        Assert.Contains(Tokens("def x", ScriptLanguage.Python),
            token => token.Kind == ScriptTokenKind.Keyword);

        Assert.DoesNotContain(Tokens("def x", ScriptLanguage.JavaScript),
            token => token.Kind == ScriptTokenKind.Keyword);
    }

    [Fact]
    public void ISimboliDellApiSiRiconoscono()
    {
        const string source = "sheet.Read(\"A1\")";

        var api = Tokens(source, ScriptLanguage.JavaScript)
            .First(token => token.Kind == ScriptTokenKind.Api);

        Assert.Equal("sheet", TextOf(source, api));
    }

    [Fact]
    public void UnaStringaNonChiusaNonFaCadereNulla()
    {
        Assert.NotEmpty(Tokens("\"senza fine", ScriptLanguage.JavaScript));
        Assert.NotEmpty(Tokens("/* senza fine", ScriptLanguage.JavaScript));
    }
}
