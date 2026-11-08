using System.Reflection;
using PulseOffice.Localization;
using PulseOffice.Presentations.UI.Scripting;
using PulseOffice.Scripting.Editor;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.UI;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class ScriptApiCatalogTests
{
    private static readonly IReadOnlyDictionary<string, Type> Facades = new Dictionary<string, Type>
    {
        ["app"] = typeof(AppFacade),
        ["deck"] = typeof(DeckFacade),
        ["doc"] = typeof(PulseOffice.Sheets.UI.Scripting.DocumentFacade),
        ["sheet"] = typeof(PulseOffice.Sheets.UI.Scripting.SheetFacade),
        ["pivot"] = typeof(PulseOffice.Sheets.UI.Scripting.PivotFacade),
        ["page"] = typeof(PulseOffice.Sheets.UI.Scripting.PrintFacade),
        ["rules"] = typeof(PulseOffice.Sheets.UI.Scripting.FormatRuleFacade),
        ["validation"] = typeof(PulseOffice.Sheets.UI.Scripting.ValidationFacade),
        ["filter"] = typeof(PulseOffice.Sheets.UI.Scripting.FilterFacade),
        ["scenarios"] = typeof(PulseOffice.Sheets.UI.Scripting.ScenarioFacade),
        ["files"] = typeof(PulseOffice.Scripting.ScriptFilesFacade),
    };

    private static IEnumerable<ApiRootSpec> Roots() =>
        new IScriptApiSection[] { new ShellScriptApi(), new SheetScriptApi(), new DeckScriptApi(), new PulseOffice.Documents.UI.Scripting.FolioScriptApi() }
            .SelectMany(section => section.Roots);

    [Fact]
    public void OgniMembroDichiaratoEsisteNellaFacciataConLaStessaArieta()
    {
        var problems = new List<string>();
        foreach (var root in Roots())
        {
            var facade = root.Module == PulseOffice.Foundation.Modules.ModuleKind.Documents
                ? typeof(PulseOffice.Documents.UI.Scripting.FolioFacade)
                : Assert.Contains(root.Name, Facades);
            foreach (var member in root.Members)
            {
                if (member.Signature.Contains('('))
                {
                    var arity = member.Parameters.Length;
                    var found = facade!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Any(method => method.Name == member.Member && method.GetParameters().Length == arity);
                    if (!found) problems.Add($"{root.Name}.{member.Member}/{arity}");
                }
                else if (facade!.GetProperty(member.Member, BindingFlags.Public | BindingFlags.Instance) is null)
                {
                    problems.Add($"{root.Name}.{member.Member}");
                }
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void LaDocumentazioneDellApiETradottaNelleCinqueLingue()
    {
        var keys = new HashSet<string>();
        foreach (var root in Roots())
        {
            var type = root.Type.ToLowerInvariant();
            keys.Add($"api.{type}.summary");
            foreach (var member in root.Members)
            {
                keys.Add($"api.{type}.{member.Member}.summary");
                foreach (var parameter in member.Parameters)
                    keys.Add($"api.param.{parameter.Split(':', 2)[1]}");
            }
        }

        var catalog = new TextCatalog(UiLanguage.English);
        var missing = UiLanguages.Translated
            .SelectMany(language => keys.Except(catalog.KeysIn(language)).Select(key => $"{language}:{key}"))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_member_has_a_short_title_in_the_five_languages_for_undo_labels_and_confirmations()
    {
        var keys = Roots().SelectMany(root => root.Members.Select(member => $"api.{root.Type.ToLowerInvariant()}.{member.Member}.title")).ToHashSet();
        var catalog = new TextCatalog(UiLanguage.English);
        var missing = UiLanguages.Translated
            .SelectMany(language => keys.Except(catalog.KeysIn(language)).Select(key => $"{language}:{key}"))
            .ToList();

        Assert.Empty(missing);
        var api = new ScriptApiProvider([new SheetScriptApi()], new TextCatalog(UiLanguage.Italian));
        var write = api.Catalog.Roots.Single(root => root.Name == "sheet").Members.Single(member => member.Name == "Write");
        Assert.Equal("Scrivi cella", write.Title);
        Assert.True(write.Title!.Length <= 40);
    }

    [Fact]
    public void Every_object_has_a_localized_title_for_its_properties_tool()
    {
        var keys = Roots().Select(root => $"api.{root.Type.ToLowerInvariant()}.readProperties.title").ToHashSet();
        var catalog = new TextCatalog(UiLanguage.English);
        var missing = UiLanguages.Translated
            .SelectMany(language => keys.Except(catalog.KeysIn(language)).Select(key => $"{language}:{key}"))
            .ToList();

        Assert.Empty(missing);
        var api = new ScriptApiProvider([new SheetScriptApi()], new TextCatalog(UiLanguage.Italian));
        Assert.Equal("Leggi proprietà del foglio", api.Catalog.Roots.Single(root => root.Name == "sheet").Title);
    }

    [Fact]
    public void LaDescrizioneDellApiSegueIlCambioDiLingua()
    {
        var texts = new TextCatalog(UiLanguage.Italian);
        var api = new ScriptApiProvider([new ShellScriptApi()], texts);
        var documentation = new ScriptDocumentation(api);

        var italian = documentation.Topics(PulseOffice.Foundation.Modules.ModuleKind.Sheets)[0].Summary;
        texts.Use(UiLanguage.German);
        var german = documentation.Topics(PulseOffice.Foundation.Modules.ModuleKind.Sheets)[0].Summary;

        Assert.Equal(new TextCatalog(UiLanguage.Italian)["api.application.summary"], italian);
        Assert.Equal(new TextCatalog(UiLanguage.German)["api.application.summary"], german);
        Assert.Equal(german, api.Catalog.Roots[0].Documentation);
    }
    [Theory]
    [InlineData(UiLanguage.Italian)]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.Spanish)]
    [InlineData(UiLanguage.French)]
    public void IlTestoDiAppendParagraphNonEDescrittoComeTestoCercato(UiLanguage language)
    {
        var texts = new TextCatalog(language);
        var api = new ScriptApiProvider([new PulseOffice.Documents.UI.Scripting.FolioScriptApi()], texts);
        var doc = api.Catalog.Roots.Single(root => root.Name == "doc");
        string Text(string member) => doc.Members.Single(m => m.Name == member).Parameters.Single(p => p.Name == "text").Documentation!;

        var searched = Text("Find");
        Assert.NotEqual(searched, Text("AppendParagraph"));
        Assert.NotEqual(searched, Text("SetParagraph"));
        Assert.NotEqual(searched, Text("InsertText"));
        Assert.NotEqual(searched, Text("SetCell"));
        Assert.Equal(texts["api.param.paragraphText"], Text("AppendParagraph"));
        Assert.Equal(texts["api.param.replacement"], doc.Members.Single(m => m.Name == "Replace").Parameters.Single(p => p.Name == "replacement").Documentation);
    }
}
