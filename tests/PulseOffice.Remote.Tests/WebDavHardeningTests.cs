using PulseOffice.Remote;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class WebDavHardeningTests
{
    private readonly WebDavResponseReader _reader = new();

    [Fact]
    public void UnGettoneConUnACapoNonDiventaUnIntestazione()
    {
        const string xml = """
            <D:prop xmlns:D="DAV:"><D:lockdiscovery><D:activelock><D:locktoken>
            <D:href>opaque:1&#13;&#10;X-Injected: yes</D:href>
            </D:locktoken></D:activelock></D:lockdiscovery></D:prop>
            """;

        Assert.Null(_reader.ReadLockToken(xml));
    }

    [Fact]
    public void UnGettoneValidoSiLegge()
    {
        const string xml = """
            <D:prop xmlns:D="DAV:"><D:lockdiscovery><D:activelock><D:locktoken>
            <D:href>opaquelocktoken:e71d4fae-5dec-22d6</D:href>
            </D:locktoken></D:activelock></D:lockdiscovery></D:prop>
            """;

        Assert.Equal("opaquelocktoken:e71d4fae-5dec-22d6", _reader.ReadLockToken(xml));
    }

    [Fact]
    public void UnaRispostaConDtdNonSiEspande()
    {
        const string xml = """
            <?xml version="1.0"?><!DOCTYPE l [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">]>
            <D:multistatus xmlns:D="DAV:"><D:response><D:href>&b;</D:href></D:response></D:multistatus>
            """;

        Assert.Empty(_reader.ReadListing(xml, "/"));
    }
}
