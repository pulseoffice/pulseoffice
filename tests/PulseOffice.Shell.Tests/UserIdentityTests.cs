using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Documents.Revisions;
using PulseOffice.Foundation.Identity;
using PulseOffice.Localization;
using PulseOffice.Settings;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class UserIdentityTests
{
    private SettingsService Settings() =>
        new([new UserIdentitySettings()],
            [new InMemorySettingsStore(SettingScope.User), new InMemorySettingsStore(SettingScope.Document), new InMemorySettingsStore(SettingScope.Policy)],
            new TextCatalog(UiLanguage.Italian), NullLogger<SettingsService>.Instance);

    [Fact]
    public void Il_nome_scelto_firma_commenti_e_revisioni_e_le_iniziali_vengono_dal_nome()
    {
        var settings = Settings();
        settings.Set(UserIdentitySettingKeys.Name, "  Ada Lovelace ");
        var identity = new SettingsUserIdentity(settings, new SystemUserIdentity());

        Assert.Equal("Ada Lovelace", identity.Name);
        Assert.Equal("AL", identity.Initials);
        Assert.Equal("Ada Lovelace", new IdentityRevisionAuthor(identity).Sign().Author);

        settings.Set(UserIdentitySettingKeys.Initials, "AdL");
        Assert.Equal("AdL", identity.Initials);
    }

    [Fact]
    public void Senza_un_nome_vale_l_account_di_sistema()
    {
        var identity = new SettingsUserIdentity(Settings(), new SystemUserIdentity());

        Assert.Equal(Environment.UserName, identity.Name);
        Assert.Equal(new InitialsOfName().Of(Environment.UserName), identity.Initials);
    }

    [Fact]
    public void UnaLetteraFuoriDalPianoBaseNonSiSpezza()
    {
        Assert.Equal("𝐀B", new InitialsOfName().Of("𝐀lfa Beta"));
        Assert.Equal("😀", new InitialsOfName().Of("😀"));
    }
}
