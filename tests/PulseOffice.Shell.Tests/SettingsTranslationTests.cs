using PulseOffice.Localization;
using PulseOffice.Settings;
using PulseOffice.Settings.Navigation;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class SettingsTranslationTests
{
    private static readonly ISettingsProvider[] Providers = [new ShellSettings()];

    private static IReadOnlyList<SettingDefinition> Schema => [.. Providers.SelectMany(provider => provider.GetSettings())];

    public static TheoryData<UiLanguage> Languages =>
        [UiLanguage.Italian, UiLanguage.English, UiLanguage.German, UiLanguage.French, UiLanguage.Spanish];

    [Theory]
    [MemberData(nameof(Languages))]
    public void OgniImpostazioneEOgniSceltaHannoIlTitolo(UiLanguage language)
    {
        var texts = new TextCatalog(language);

        var missing = Schema
            .SelectMany(definition => definition.Choices
                .Select(choice => definition.ChoiceKey(choice.Value))
                .Prepend(definition.TitleKey))
            .Where(key => !texts.Contains(key))
            .ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void OgniCategoriaHaIlTitolo(UiLanguage language)
    {
        var texts = new TextCatalog(language);
        var pages = new SettingsPageBuilder().Build(Schema);

        var missing = pages
            .Where(page => !page.IsModule)
            .Select(page => page.TitleKey)
            .Concat(pages.SelectMany(page => page.Sections).Select(section => section.TitleKey).OfType<string>())
            .Where(key => !texts.Contains(key))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void OgniCategoriaHaLaSuaIcona()
    {
        var icons = new SettingsIcons();

        var missing = new SettingsPageBuilder().Build(Schema)
            .Select(page => page.IconKey)
            .Where(key => !icons.Contains(key))
            .ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void ITestiDellaFinestraSonoTradotti(UiLanguage language)
    {
        var texts = new TextCatalog(language);
        string[] keys =
        [
            "settings.search", "settings.advanced", "settings.modules", "settings.resetAll",
            "settings.resetAll.description", "settings.done", "settings.reset", "settings.modified",
            "settings.range", "settings.option.none", "settings.option.unavailable", "settings.options.none",
            "settings.browse", "settings.restart", "settings.locked", "settings.empty",
        ];

        var missing = keys.Where(key => !texts.Contains(key)).ToList();

        Assert.Empty(missing);
    }
}
