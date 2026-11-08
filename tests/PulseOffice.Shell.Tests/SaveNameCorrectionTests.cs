using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class SaveNameCorrectionTests
{
    private readonly SaveNameCorrection _names = new();

    private readonly IReadOnlyList<FileFilter> _filters =
    [
        new("ODS", [".ods"]),
        new("XLSX", [".xlsx"]),
    ];

    [Theory]
    [InlineData("/tmp/bilancio.xlsx.ods", "/tmp/bilancio.xlsx")]
    [InlineData("/tmp/bilancio.ods", "/tmp/bilancio.ods")]
    [InlineData("/tmp/bilancio.ods.ods", "/tmp/bilancio.ods.ods")]
    [InlineData("/tmp/bilancio.2026.ods", "/tmp/bilancio.2026.ods")]
    [InlineData("/tmp/bilancio", "/tmp/bilancio")]
    public void LEstensioneScrittaNelNomeVinceSuQuellaAggiuntaDalPannello(string chosen, string expected) =>
        Assert.Equal(expected, _names.Corrected(chosen, _filters));
}
