using System.Collections.Specialized;
using PulseOffice.Remote.Cloud;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class LoopbackStateTests
{
    private readonly LoopbackListener _listener = new();

    [Fact]
    public void UnaRichiestaEstraneaNonInterrompeLAccesso()
    {
        Assert.False(_listener.BelongsToSignIn(new NameValueCollection { ["error"] = "access_denied" }, "atteso"));
        Assert.False(_listener.BelongsToSignIn(new NameValueCollection { ["code"] = "x", ["state"] = "altro" }, "atteso"));
        Assert.False(_listener.BelongsToSignIn(new NameValueCollection { ["state"] = "atteso" }, "atteso"));
    }

    [Fact]
    public void LaRispostaGiustaSiAccetta()
    {
        Assert.True(_listener.BelongsToSignIn(new NameValueCollection { ["code"] = "c", ["state"] = "atteso" }, "atteso"));
        Assert.True(_listener.BelongsToSignIn(new NameValueCollection { ["error"] = "access_denied", ["state"] = "atteso" }, "atteso"));
    }
}
