using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Localization;
using PulseOffice.Settings;
using Xunit;

namespace PulseOffice.Settings.Tests;

public class SettingsServiceTests
{
    private sealed class Provider(params SettingDefinition[] definitions) : ISettingsProvider
    {
        public IEnumerable<SettingDefinition> GetSettings() => definitions;
    }

    private static readonly ITextCatalog Texts = new ProbeCatalog();

    private sealed class ProbeCatalog : ITextCatalog
    {
        public UiLanguage Language => UiLanguage.Italian;

        public event EventHandler? LanguageChanged;

        public string this[string key] => key[(key.LastIndexOf('.') + 1)..];

        public bool Contains(string key) => true;

        public string Format(string key, params object?[] arguments) => this[key];

        public void Use(UiLanguage language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static SettingDefinition Boolean(string key, bool value = false) => new()
    {
        Key = key,
        Kind = SettingKind.Boolean,
        DefaultValue = value,
        Category = "Prova",
    };

    private static SettingDefinition Number(string key, int value, double? min = null, double? max = null) => new()
    {
        Key = key,
        Kind = SettingKind.Integer,
        DefaultValue = value,
        Minimum = min,
        Maximum = max,
        Category = "Prova",
    };

    private static (SettingsService Service, ISettingsStore Policy, ISettingsStore User) Build(
        params SettingDefinition[] definitions)
    {
        var policy = new InMemorySettingsStore(SettingScope.Policy);
        var user = new InMemorySettingsStore(SettingScope.User);
        var document = new InMemorySettingsStore(SettingScope.Document);

        var service = new SettingsService(
            [new Provider(definitions)],
            [user, document, policy],
            Texts,
            NullLogger<SettingsService>.Instance);

        return (service, policy, user);
    }

    [Fact]
    public void SenzaScelteSiUsaIlValoreDiFabbrica()
    {
        var (service, _, _) = Build(Boolean("a", value: true));

        Assert.True(service.Get<bool>("a"));
        Assert.Equal(SettingScope.Factory, service.EffectiveScope("a"));
    }

    [Fact]
    public void LaSceltaDellUtenteVinceSulValoreDiFabbrica()
    {
        var (service, _, _) = Build(Boolean("a", value: true));

        service.Set("a", false);

        Assert.False(service.Get<bool>("a"));
        Assert.Equal(SettingScope.User, service.EffectiveScope("a"));
    }

    [Fact]
    public void IlCriterioVinceSullaSceltaDellUtente()
    {
        var (service, policy, _) = Build(Boolean("a"));

        service.Set("a", true);
        policy.Set("a", false);

        Assert.False(service.Get<bool>("a"));
        Assert.Equal(SettingScope.Policy, service.EffectiveScope("a"));
        Assert.True(service.IsLocked("a"));
    }

    [Fact]
    public void UnImpostazioneFissataDaUnCriterioNonSiPuoCambiare()
    {
        var (service, policy, _) = Build(Boolean("a"));
        policy.Set("a", true);

        Assert.Throws<InvalidOperationException>(() => service.Set("a", false));
    }

    [Fact]
    public void IlRipristinoTornaAllAmbitoSottostante()
    {
        var (service, _, _) = Build(Boolean("a", value: true));
        service.Set("a", false);

        service.Reset("a");

        Assert.True(service.Get<bool>("a"));
        Assert.Equal(SettingScope.Factory, service.EffectiveScope("a"));
    }

    [Fact]
    public void UnValoreFuoriIntervalloVieneRifiutato()
    {
        var (service, _, _) = Build(Number("n", 10, min: 0, max: 100));

        Assert.Throws<ArgumentException>(() => service.Set("n", 500));
        Assert.Equal(10, service.Get<int>("n"));
    }

    [Fact]
    public void UnaSceltaNonPrevistaVieneRifiutata()
    {
        var (service, _, _) = Build(new SettingDefinition
        {
            Key = "c",
            Kind = SettingKind.Choice,
            DefaultValue = "uno",
            Category = "Prova",
            Choices = [new("uno"), new("due")],
        });

        Assert.Throws<ArgumentException>(() => service.Set("c", "tre"));
        service.Set("c", "due");
        Assert.Equal("due", service.Get<string>("c"));
    }

    [Fact]
    public void UnValoreDelTipoSbagliatoNonFaCadereNulla()
    {
        var (service, _, user) = Build(Number("n", 42));

        user.Set("n", "non un numero");

        Assert.Equal(42, service.Get<int>("n"));
    }

    [Fact]
    public void UnImpostazioneSconosciutaEUnErrore()
    {
        var (service, _, _) = Build(Boolean("a"));
        Assert.Throws<KeyNotFoundException>(() => service.Get<bool>("inesistente"));
    }

    [Fact]
    public void ChiaviDuplicateSonoUnErrorediComposizione()
    {
        Assert.Throws<InvalidOperationException>(() => new SettingsService(
            [new Provider(Boolean("a")), new Provider(Boolean("a"))],
            [new InMemorySettingsStore(SettingScope.User)],
            Texts,
            NullLogger<SettingsService>.Instance));
    }

    [Fact]
    public void NonSiPuoScrivereOltreLAmbitoMassimo()
    {
        var (service, _, _) = Build(Boolean("a") with { MaximumScope = SettingScope.User });
        Assert.Throws<InvalidOperationException>(() => service.Set("a", true, SettingScope.Document));
    }

    [Fact]
    public void LaRicercaGuardaTitoloChiaveCategoriaEParoleChiave()
    {
        var (service, _, _) = Build(
            Boolean("appearance.theme") with { Keywords = ["scuro", "chiaro"] },
            Boolean("documents.autosave"));

        Assert.Single(service.Search("scuro"));
        Assert.Single(service.Search("autosave"));
        Assert.Equal(2, service.Search("").Count());
    }

    [Fact]
    public void UnInteroSiLeggeInQualunqueTipoNumerico()
    {
        var (service, _, _) = Build(Number("n", 10));

        Assert.Equal(10L, service.Get<long>("n"));
        Assert.Equal(10d, service.Get<double>("n"));
        Assert.Equal(10, service.Get<int>("n"));
    }

    [Fact]
    public void UnNumeroSalvatoComeDecimaleTornaIntero()
    {
        var (service, _, user) = Build(Number("n", 10));

        user.Set("n", 7d);

        Assert.Equal(7, service.Get<int>("n"));
    }

    [Fact]
    public void UnTestoNonDiventaUnNumero()
    {
        var (service, _, user) = Build(Number("n", 42));

        user.Set("n", "7");

        Assert.Equal(42, service.Get<int>("n"));
    }

    [Fact]
    public void IlCambiamentoVieneSegnalato()
    {
        var (service, _, _) = Build(Boolean("a"));
        SettingChangedEventArgs? seen = null;
        service.Changed += (_, e) => seen = e;

        service.Set("a", true);

        Assert.NotNull(seen);
        Assert.Equal("a", seen.Key);
        Assert.Equal(SettingScope.User, seen.Scope);
    }
}
