using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.Conditional;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class ConditionalRulesTests
{
    private readonly ConditionalRuleCatalog _catalog = new();
    private readonly ConditionalRuleCompletion _completion;
    private readonly CultureInfo _italian = CultureInfo.GetCultureInfo("it-IT");
    private readonly CellRange _range = new(new CellAddress(3, 2), new CellAddress(10, 13));

    public ConditionalRulesTests() =>
        _completion = new ConditionalRuleCompletion(_catalog, new FormulaLocalization(new FormulaParser(), new FunctionNameVocabulary()));

    private ConditionalRule? Complete(ConditionalRule rule, string[] operands, string formula = "") =>
        _completion.Complete(new ConditionalRuleDraft(rule, _range.ToString(), operands, formula), [_range], _italian, "it");

    [Fact]
    public void UnaFormulaScrittaInItalianoDiventaCanonica()
    {
        var rule = Complete(_catalog.Default(ConditionalRuleKind.Formula, _range), [], "E($C4>40000;VERO)");
        Assert.NotNull(rule);
        Assert.Equal("=AND($C4>40000;TRUE)", rule!.Formula);
        Assert.Equal(ConditionalRuleKind.Formula, _catalog.KindOf(rule));
    }

    [Fact]
    public void IValoriSiLegonoNellaLinguaESiScrivonoInvarianti()
    {
        var between = Complete(_catalog.Default(ConditionalRuleKind.CellValue, _range) with { Operator = ConditionOperator.Between }, ["1.000,5", "2000"]);
        Assert.Equal(["1000.5", "2000"], between!.Operands);
        Assert.Null(Complete(_catalog.Default(ConditionalRuleKind.CellValue, _range) with { Operator = ConditionOperator.Between }, ["1"]));
        Assert.Null(Complete(_catalog.Default(ConditionalRuleKind.Formula, _range), []));
        Assert.Null(Complete(_catalog.Default(ConditionalRuleKind.TopBottom, _range) with { Operator = ConditionOperator.TopPercent }, ["150"]));
        Assert.Empty(Complete(_catalog.Default(ConditionalRuleKind.Blanks, _range), ["x"])!.Operands);
    }

    [Theory]
    [InlineData("007")]
    [InlineData("1e3")]
    [InlineData("1.000")]
    public void IlTestoCercatoNonSiTrattaComeNumero(string typed)
    {
        var rule = Complete(_catalog.Default(ConditionalRuleKind.Text, _range), [typed]);
        Assert.Equal([typed], rule!.Operands);
    }

    [Fact]
    public void LeAreeMultipleFinisconoNelleAreeAggiuntive()
    {
        var second = new CellRange(new CellAddress(20, 0), new CellAddress(25, 0));
        var rule = _completion.Complete(new ConditionalRuleDraft(_catalog.Default(ConditionalRuleKind.DataBar, _range), "", [], ""), [_range, second], _italian, "it");
        Assert.Equal(_range, rule!.Range);
        Assert.Equal([second], rule.AdditionalRanges);
    }

    [Theory]
    [InlineData(ConditionalRuleKind.CellValue)]
    [InlineData(ConditionalRuleKind.TopBottom)]
    [InlineData(ConditionalRuleKind.Average)]
    [InlineData(ConditionalRuleKind.Duplicates)]
    [InlineData(ConditionalRuleKind.Text)]
    [InlineData(ConditionalRuleKind.Date)]
    [InlineData(ConditionalRuleKind.Blanks)]
    [InlineData(ConditionalRuleKind.Formula)]
    [InlineData(ConditionalRuleKind.DataBar)]
    [InlineData(ConditionalRuleKind.ColorScale)]
    [InlineData(ConditionalRuleKind.IconSet)]
    public void OgniTipoSiRiconosceDallaSuaRegola(ConditionalRuleKind kind) =>
        Assert.Equal(kind, _catalog.KindOf(_catalog.Default(kind, _range)));

    [Fact]
    public void LoStileSiRiconosceFraICampioni()
    {
        var preset = _catalog.StylePresets[2];
        Assert.Equal(preset, _catalog.PresetOf(_catalog.StyleOf(preset)));
    }

    [Fact]
    public void LeDeviazioniStandardSopravvivonoAlCompletamentoESiDescrivono()
    {
        var rule = Complete(_catalog.Default(ConditionalRuleKind.Average, _range) with { StandardDeviations = 2 }, []);
        Assert.Equal(2, rule!.StandardDeviations);
        foreach (var language in Enum.GetValues<UiLanguage>())
            Assert.Contains("2", _catalog.Describe(rule, new TextCatalog(language)));
    }

    [Fact]
    public void LaDescrizioneEInOgniLingua()
    {
        foreach (var language in Enum.GetValues<UiLanguage>())
        {
            var texts = new TextCatalog(language);
            foreach (var kind in _catalog.Kinds)
                Assert.False(string.IsNullOrWhiteSpace(_catalog.Describe(_catalog.Default(kind, _range) with { Operands = ["5"] }, texts)));
        }
    }
}
