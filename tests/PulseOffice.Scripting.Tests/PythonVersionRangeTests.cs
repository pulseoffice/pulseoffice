using PulseOffice.Scripting.CPython;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class PythonVersionRangeTests
{
    [Theory]
    [InlineData("3.9.6", false)]
    [InlineData("3.10.0", true)]
    [InlineData("3.13.18", true)]
    [InlineData("3.14.10", true)]
    [InlineData("3.15.0", false)]
    public void Solo_le_versioni_che_pythonnet_sa_avviare_sono_supportate(string version, bool supported) =>
        Assert.Equal(supported, new PythonVersionRange().IsSupported(Version.Parse(version)));

    [Fact]
    public void La_scelta_automatica_non_propone_mai_un_Python_che_non_si_avvia()
    {
        var best = new PythonDiscovery().FindBest();

        if (best is not null)
            Assert.True(new PythonVersionRange().IsSupported(best.Version), best.ToString());
    }

    [Fact]
    public void Un_interprete_che_non_risponde_si_abbandona_entro_il_tetto_della_sonda()
    {
        if (OperatingSystem.IsWindows()) return;
        var folder = Directory.CreateTempSubdirectory("pulse-probe-");
        var script = Path.Combine(folder.FullName, "python3");
        File.WriteAllText(script, "#!/bin/sh\nprintf 'noise\\n' 1>&2\nexec sleep 40\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = new PythonDiscovery().Inspect(script);

        Assert.Null(result.Runtime);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(25), watch.Elapsed.ToString());
    }
}
