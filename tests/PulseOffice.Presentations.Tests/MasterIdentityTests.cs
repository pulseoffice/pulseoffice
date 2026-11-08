using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class MasterIdentityTests
{
    [Fact]
    public void ASecondMasterWithAnExistingIdIsRefused()
    {
        var deck = new Presentation();
        var masters = new MasterFactory();
        deck.AddMaster(masters.CreateStandard(deck.Theme) with { Id = "master2" });

        Assert.Throws<InvalidOperationException>(() => deck.AddMaster(masters.CreateStandard(deck.Theme) with { Id = "master2" }));
        Assert.Throws<InvalidOperationException>(() => deck.InsertMaster(1, masters.CreateStandard(deck.Theme) with { Id = deck.Master.Id }));
        Assert.Equal(2, deck.Masters.Count);
    }
}
