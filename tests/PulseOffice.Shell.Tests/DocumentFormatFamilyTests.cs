using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class DocumentFormatFamilyTests
{
    private readonly DocumentFormatFamily _family = new();

    [Theory]
    [InlineData(null, "ODF")]
    [InlineData("/tmp/bilancio.ods", "ODF")]
    [InlineData("/tmp/bilancio.xlsx", "OOXML")]
    [InlineData("/tmp/Relazione.DOCX", "OOXML")]
    [InlineData("/tmp/dati.csv", "CSV")]
    public void LaBarraDiStatoDiceIlFormatoDelFileAperto(string? path, string expected) =>
        Assert.Equal(expected, _family.Of(path));
}
