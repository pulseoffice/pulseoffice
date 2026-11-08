using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Localization;
using PulseOffice.Security;
using PulseOffice.Settings;
using PulseOffice.UI.Controls.Macros;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Macros;

public sealed class DocumentMacroGateTests
{
    private sealed class MacroLevel : ISettingsProvider
    {
        public IEnumerable<SettingDefinition> GetSettings() =>
        [
            new SettingDefinition
            {
                Key = MacroTrustPolicy.SettingKey,
                Kind = SettingKind.Choice,
                DefaultValue = "high",
                Category = "security/macros",
                Choices = [new("veryHigh"), new("high"), new("medium"), new("low")],
            },
        ];
    }

    private sealed class Prompt(bool answer) : IMacroConsentPrompt
    {
        public int Asked { get; private set; }

        public Task<bool> AskAsync(string macroName, SignatureStatus signature, CancellationToken ct)
        {
            Asked++;
            return Task.FromResult(answer);
        }
    }

    private sealed class Document : IMacroSignatureHolder
    {
        public MacroSignature? MacroSignature { get; set; }

        public IReadOnlyList<(string Name, string Language, string Source)> SignableMacros { get; set; } = [("Main", "javascript", "sheet.Twice(1)")];
    }

    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.Italian);

    private (DocumentMacroGate Gate, Prompt Prompt) Gate(string level, bool answer)
    {
        var user = new InMemorySettingsStore(SettingScope.User);
        var settings = new SettingsService([new MacroLevel()], [user, new InMemorySettingsStore(SettingScope.Document), new InMemorySettingsStore(SettingScope.Policy)], _texts, NullLogger<SettingsService>.Instance);
        settings.Set(MacroTrustPolicy.SettingKey, level);
        var prompt = new Prompt(answer);
        var status = new StatusBarService(TimeProvider.System, new NotificationCenter(TimeProvider.System), new NotificationHistory(TimeProvider.System));
        return (new DocumentMacroGate(settings, new MacroSigner(), new MacroTrustPolicy(), prompt, status, _texts), prompt);
    }

    [Fact]
    public async Task An_unsigned_document_macro_does_not_run_without_consent_at_the_default_level()
    {
        var (gate, prompt) = Gate("high", answer: false);

        Assert.False(await gate.AllowAsync(new Document(), "Main", TestContext.Current.CancellationToken));
        Assert.Equal(1, prompt.Asked);
    }

    [Fact]
    public async Task Very_high_blocks_unsigned_macros_without_asking()
    {
        var (gate, prompt) = Gate("veryHigh", answer: true);

        Assert.False(await gate.AllowAsync(new Document(), "Main", TestContext.Current.CancellationToken));
        Assert.Equal(0, prompt.Asked);
    }

    [Fact]
    public async Task Medium_asks_once_per_document()
    {
        var (gate, prompt) = Gate("medium", answer: true);
        var document = new Document();

        Assert.True(await gate.AllowAsync(document, "Main", TestContext.Current.CancellationToken));
        Assert.True(await gate.AllowAsync(document, "Other", TestContext.Current.CancellationToken));
        Assert.Equal(1, prompt.Asked);
    }

    [Fact]
    public async Task Low_runs_without_asking()
    {
        var (gate, prompt) = Gate("low", answer: false);

        Assert.True(await gate.AllowAsync(new Document(), "Main", TestContext.Current.CancellationToken));
        Assert.Equal(0, prompt.Asked);
    }

    [Fact]
    public async Task A_signature_broken_by_a_changed_macro_blocks_even_at_low_risk_levels()
    {
        var (gate, prompt) = Gate("medium", answer: true);
        using var certificate = SelfSigned();
        var document = new Document();
        document.MacroSignature = new MacroSigner().Sign(document.SignableMacros, certificate);
        document.SignableMacros = [("Main", "javascript", "sheet.Twice(2)")];

        Assert.False(await gate.AllowAsync(document, "Main", TestContext.Current.CancellationToken));
        Assert.Equal(0, prompt.Asked);
    }

    [Fact]
    public void A_self_signed_macro_signature_is_not_trusted()
    {
        using var certificate = SelfSigned();
        var macros = new Document().SignableMacros;

        Assert.Equal(SignatureStatus.UntrustedCertificate, new MacroSigner().Verify(macros, new MacroSigner().Sign(macros, certificate)));
    }

    [Fact]
    public void A_macro_signature_made_before_the_certificate_was_valid_is_not_valid()
    {
        using var certificate = SelfSigned(DateTimeOffset.Now.AddDays(10));
        var macros = new Document().SignableMacros;
        var signature = new MacroSigner().Sign(macros, certificate) with { SignedOn = DateTimeOffset.Now };

        Assert.Equal(SignatureStatus.ExpiredCertificate, new MacroSigner().Verify(macros, signature));
    }

    private X509Certificate2 SelfSigned(DateTimeOffset? from = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Macro author", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var start = from ?? DateTimeOffset.Now.AddDays(-1);
        return request.CreateSelfSigned(start, start.AddYears(1));
    }
}
