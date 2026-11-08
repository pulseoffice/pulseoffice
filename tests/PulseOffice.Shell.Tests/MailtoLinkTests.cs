using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class MailtoLinkTests
{
    private readonly MailtoLink _link = new();

    [Fact]
    public void IDestinatariStannoSeparatiDaVirgola()
    {
        var link = _link.Build(new MailDraft { To = ["ada@example.org", "rossi@example.org"] });

        Assert.Equal("mailto:ada@example.org,rossi@example.org", link);
    }

    [Fact]
    public void OggettoECorpoSonoCodificati()
    {
        var link = _link.Build(new MailDraft
        {
            To = ["ada@example.org"],
            Subject = "Conti & spese",
            Body = "Ecco il foglio",
        });

        Assert.Contains("subject=Conti%20%26%20spese", link, StringComparison.Ordinal);
        Assert.Contains("body=Ecco%20il%20foglio", link, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaBozzaVuotaNonPortaPuntoInterrogativo()
    {
        Assert.Equal("mailto:", _link.Build(new MailDraft()));
    }

    [Fact]
    public async Task LAssenzaDiUnClientNonFaCadereNulla()
    {
        var result = await new UnavailableMailComposer()
            .ComposeAsync(new MailDraft(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("MAIL003", result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task SenzaFileIlTrasferimentoBluetoothFallisceInModoDichiarato()
    {
        var result = await new UnavailableFileSharing()
            .SendAsync([], TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("SHARE003", result.Diagnostics[0].Code);
    }
}
