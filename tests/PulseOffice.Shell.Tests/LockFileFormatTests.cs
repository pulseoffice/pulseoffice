using Xunit;

namespace PulseOffice.Shell.Tests;

public class LockFileFormatTests
{
    private readonly LockFileFormat _format = new();

    [Fact]
    public void UnBloccoEsternoSiLegge()
    {
        var claim = _format.Read(",Ada Rossi,studio-mac,03.10.2026 14:05,file:///Users/ada/Library/Application%20Support/OfficeSuite/4;");

        Assert.NotNull(claim);
        Assert.Equal("Ada Rossi", claim.User);
        Assert.Equal("studio-mac", claim.Machine);
        Assert.Equal(14, claim.Taken.Hour);
    }

    [Fact]
    public void IlNostroBloccoTornaUgualeConVirgoleNelNome()
    {
        var written = _format.Write(new ShareClaim("Rossi, Ada", "pc;1", new DateTimeOffset(2026, 10, 3, 9, 30, 0, TimeSpan.Zero)));
        var read = _format.Read(written);

        Assert.Equal("Rossi, Ada", read!.User);
        Assert.Equal("pc;1", read.Machine);
    }
}
