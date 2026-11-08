using PulseOffice.Platform.MacOS;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class KeychainTextTests
{
    [Fact]
    public void UnNomeConUnACapoNonPuoAggiungereUnSecondoComando()
    {
        Assert.False(KeychainText.CanQuote("PulseOffice\ndelete-keychain login.keychain"));
        Assert.Throws<ArgumentException>(() => KeychainText.AddCommand("PulseOffice\nx", "utente", "segreto"));
    }

    [Fact]
    public void IlComandoEUnaRigaSola()
    {
        var command = KeychainText.AddCommand("Pulse \"Office\"", "utente", "pà");

        Assert.Single(command.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("pà", KeychainText.Decode(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("pà"))));
    }

    [Fact]
    public void UnaPasswordEsadecimaleNonSiDecodificaInSpazzatura()
    {
        Assert.Equal("deadbeef", KeychainText.Decode("deadbeef"));
        Assert.Equal("cafe", KeychainText.Decode("cafe"));
    }
}
