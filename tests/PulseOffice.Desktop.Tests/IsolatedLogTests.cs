using PulseOffice.Diagnostics;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class IsolatedLogTests
{
    [Fact]
    public void Con_una_cartella_dati_usa_e_getta_anche_il_registro_ci_finisce_dentro()
    {
        var previous = Environment.GetEnvironmentVariable(LogPaths.DataHomeVariable);
        var home = Path.Combine(Path.GetTempPath(), "pulse-isolated-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable(LogPaths.DataHomeVariable, home);
            Assert.Equal(Path.Combine(home, "logs"), LogPaths.LogDirectory);
            Assert.Equal(PulseOffice.Settings.UserDataPaths.DataHomeVariable, LogPaths.DataHomeVariable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LogPaths.DataHomeVariable, previous);
        }
    }
}
