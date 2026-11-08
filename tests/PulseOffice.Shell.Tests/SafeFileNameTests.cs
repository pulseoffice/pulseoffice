using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class SafeFileNameTests
{
    [Theory]
    [InlineData("../../../../tmp/evil", ".._.._.._.._tmp_evil")]
    [InlineData("..\\..\\x", ".._.._x")]
    [InlineData("..", "versione")]
    [InlineData("", "versione")]
    [InlineData("Versione 2", "Versione 2")]
    [InlineData("C:x", "C_x")]
    public void Un_nome_preso_dal_documento_non_esce_mai_dalla_cartella(string name, string expected)
    {
        var folder = Path.Combine(Path.GetTempPath(), "cartella");
        var safe = new SafeFileName().Of(name, "versione");

        Assert.Equal(expected, safe);
        Assert.Equal(folder, Path.GetDirectoryName(Path.GetFullPath(Path.Combine(folder, safe + ".odt"))));
    }
}
