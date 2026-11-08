using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Settings.Navigation;
using Xunit;

namespace PulseOffice.Settings.Tests;

public class SettingsNavigationTests
{
    private sealed class Provider(params SettingDefinition[] definitions) : ISettingsProvider
    {
        public IEnumerable<SettingDefinition> GetSettings() => definitions;
    }

    private sealed class Catalog : ITextCatalog
    {
        public UiLanguage Language => UiLanguage.Italian;

        public event EventHandler? LanguageChanged;

        public string this[string key] => key switch
        {
            "setting.theme.title" => "Tema",
            "setting.font.title" => "Carattere",
            "setting.zoom.title" => "Ingrandimento",
            "setting.log.title" => "Registro",
            "setting.grid.title" => "Griglia",
            _ => key,
        };

        public bool Contains(string key) => key.EndsWith(".title", StringComparison.Ordinal);

        public string Format(string key, params object?[] arguments) => this[key];

        public void Use(UiLanguage language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static SettingDefinition Define(string key, string category, ModuleKind module = ModuleKind.Shell, bool advanced = false) => new()
    {
        Key = key,
        Kind = SettingKind.Boolean,
        DefaultValue = false,
        Category = category,
        Module = module,
        IsAdvanced = advanced,
    };

    private static readonly SettingDefinition[] Schema =
    [
        Define("grid", "grid", ModuleKind.Sheets),
        Define("theme", "appearance"),
        Define("font", "appearance/text"),
        Define("zoom", "appearance"),
        Define("log", "diagnostics", advanced: true),
    ];

    private static SettingsService Service(params SettingDefinition[] definitions) => new(
        [new Provider(definitions)],
        [new InMemorySettingsStore(SettingScope.User), new InMemorySettingsStore(SettingScope.Policy)],
        new Catalog(),
        NullLogger<SettingsService>.Instance);

    [Fact]
    public void LePagineDelGuscioVengonoPrimaDeiModuliNellOrdineDelloSchema()
    {
        var pages = new SettingsPageBuilder().Build(Schema);

        Assert.Equal(["appearance", "diagnostics", "module.sheets"], pages.Select(page => page.Id));
        Assert.True(pages[2].IsModule);
        Assert.Equal(ModuleKind.Sheets, pages[2].Module);
    }

    [Fact]
    public void LeSezioniSeguonoIlPercorsoELaRadiceVienePrima()
    {
        var appearance = new SettingsPageBuilder().Build(Schema)[0];

        Assert.Equal([null, "text"], appearance.Sections.Select(section => section.Id));
        Assert.Equal(["theme", "zoom"], appearance.Sections[0].Settings.Select(setting => setting.Key));
        Assert.Equal("settingCategory.text", appearance.Sections[1].TitleKey);
        Assert.Equal("settingCategory.appearance", appearance.TitleKey);
        Assert.Equal(3, appearance.Count);
    }

    [Fact]
    public void LeImpostazioniAvanzateSiVedonoSoloSeRichiesteOCercando()
    {
        var navigator = new SettingsNavigator(Service(Schema), new SettingsPageBuilder());

        Assert.DoesNotContain(navigator.Pages(new SettingsQuery("", false)), page => page.Id == "diagnostics");
        Assert.Contains(navigator.Pages(new SettingsQuery("", true)), page => page.Id == "diagnostics");
        Assert.Contains(navigator.Pages(new SettingsQuery("registro", false)), page => page.Id == "diagnostics");
        Assert.True(navigator.HasAdvanced);
    }

    [Fact]
    public void LaRicercaTieneSoloLePagineConRisultati()
    {
        var navigator = new SettingsNavigator(Service(Schema), new SettingsPageBuilder());

        var pages = navigator.Pages(new SettingsQuery("cara", false));

        var page = Assert.Single(pages);
        Assert.Equal("appearance", page.Id);
        Assert.Equal(["font"], page.Settings.Select(setting => setting.Key));
    }

    [Fact]
    public void LoStatoDistingueModificataBloccataEDaRiavviare()
    {
        var number = new SettingDefinition
        {
            Key = "count",
            Kind = SettingKind.Integer,
            DefaultValue = 20,
            Category = "general",
            RequiresRestart = true,
        };
        var policy = new InMemorySettingsStore(SettingScope.Policy);
        var service = new SettingsService(
            [new Provider(number, Define("theme", "appearance"))],
            [new InMemorySettingsStore(SettingScope.User), policy],
            new Catalog(),
            NullLogger<SettingsService>.Instance);
        var states = new SettingStates(service, new SettingValueComparer());

        Assert.False(states.Of(number).IsModified);
        Assert.True(states.Of(number).RequiresRestart);

        service.Set("count", 20L);
        Assert.False(states.Of(number).IsModified);

        service.Set("count", 30L);
        Assert.True(states.Of(number).CanReset);

        states.Reset(number);
        Assert.False(states.Of(number).IsModified);

        policy.Set("count", 40);
        Assert.True(states.Of(number).IsModified);
        Assert.True(states.Of(number).IsLocked);
        Assert.False(states.Of(number).CanReset);
    }

    [Fact]
    public void RipristinaTutteRiportaSoloCioCheSiPuo()
    {
        var service = Service(Schema);
        var states = new SettingStates(service, new SettingValueComparer());

        service.Set("theme", true);
        service.Set("zoom", true);

        Assert.True(states.AnyResettable(service.Definitions));
        Assert.Equal(2, states.ResetAll(service.Definitions));
        Assert.False(states.AnyResettable(service.Definitions));
        Assert.False(service.Get<bool>("theme"));
    }

    [Fact]
    public void LeSorgentiDelleOpzioniSiTrovanoPerNome()
    {
        var sources = new SettingOptionSources([new FixedSource()]);

        Assert.NotNull(sources.Find("devices"));
        Assert.Null(sources.Find("altro"));
        Assert.Null(sources.Find(null));
    }

    private sealed class FixedSource : ISettingOptionSource
    {
        public string Name => "devices";

        public Task<IReadOnlyList<SettingOption>> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SettingOption>>([new SettingOption("test:0", "Scanner di prova")]);
    }
}
