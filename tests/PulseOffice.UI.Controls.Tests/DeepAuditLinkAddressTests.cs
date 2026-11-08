using PulseOffice.UI.Controls.Links;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditLinkAddressTests
{
    [Theory]
    [InlineData("//")]
    [InlineData("//:")]
    public void Un_percorso_assoluto_che_non_diventa_un_indirizzo_non_lancia(string typed)
    {
        var address = new LinkAddresses().File(typed);

        Assert.Equal(typed, address);
    }
}
