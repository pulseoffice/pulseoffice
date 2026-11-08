using PulseOffice.Localization;
using PulseOffice.Scripting.JavaScript;
using PulseOffice.Scripting.Python;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class MacroSampleTests
{
    public static TheoryData<UiLanguage, string> Esempi()
    {
        var data = new TheoryData<UiLanguage, string>();
        foreach (var language in Enum.GetValues<UiLanguage>())
            foreach (var key in new[] { "macro.sample", "macro.sample.deck", "macro.sample.folio" })
                data.Add(language, key);
        return data;
    }

    [Theory]
    [MemberData(nameof(Esempi))]
    public void Gli_esempi_delle_macro_si_compilano_in_JavaScript_e_in_Python(UiLanguage language, string key)
    {
        var texts = new TextCatalog(language);

        Assert.Empty(new JavaScriptEngine().Analyze(texts[key]));
        Assert.Empty(new IronPythonScriptEngine().Analyze(texts[key + ".python"]));
        Assert.NotEqual(texts[key], texts[key + ".python"]);
    }

    [Fact]
    public void Solo_JavaScript_si_dichiara_in_sandbox_e_IronPython_mostra_la_propria_versione()
    {
        Assert.True(new JavaScriptEngine().IsSandboxed);
        Assert.False(new IronPythonScriptEngine().IsSandboxed);
        Assert.StartsWith("3.", new IronPythonScriptEngine().RuntimeVersion, StringComparison.Ordinal);
        Assert.Contains("sandbox", new TextCatalog(UiLanguage.Italian)["macro.runtime.unsandboxed"], StringComparison.Ordinal);
    }
}
