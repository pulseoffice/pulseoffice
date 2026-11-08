using PulseOffice.Remote;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class BoundedResponseTests
{
    [Fact]
    public async Task UnaRispostaOltreIlTettoSiRifiuta()
    {
        var limit = new BoundedResponse(10);
        using var content = new StreamContent(new MemoryStream(new byte[11]));

        await Assert.ThrowsAsync<HttpRequestException>(() => limit.BytesAsync(content, CancellationToken.None));
    }

    [Fact]
    public async Task UnaLunghezzaDichiarataTroppoGrandeSiRifiutaSubito()
    {
        var limit = new BoundedResponse(10);
        using var content = new ByteArrayContent(new byte[20]);

        await Assert.ThrowsAsync<HttpRequestException>(() => limit.BytesAsync(content, CancellationToken.None));
    }

    [Fact]
    public async Task UnaRispostaNelTettoSiLeggeIntera()
    {
        var limit = new BoundedResponse(10);
        using var content = new StringContent("ciao");

        Assert.Equal("ciao", await limit.TextAsync(content, CancellationToken.None));
    }
}
