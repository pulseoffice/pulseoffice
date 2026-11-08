using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class ExecutableLocatorTests
{
    [Fact]
    public void UnaCartellaRelativaNelPathNonSiUsa()
    {
        var name = "pulseoffice-finto-" + Guid.NewGuid().ToString("N");
        var planted = Path.Combine(Environment.CurrentDirectory, name);
        File.WriteAllText(planted, string.Empty);

        try
        {
            Assert.False(new PathExecutableLocator(() => ".").Exists(name));
        }
        finally
        {
            File.Delete(planted);
        }
    }

    [Fact]
    public void UnaCartellaAssolutaNelPathSiUsa()
    {
        var directory = Path.GetTempPath();
        var name = "pulseoffice-vero-" + Guid.NewGuid().ToString("N");
        var file = Path.Combine(directory, name);
        File.WriteAllText(file, string.Empty);

        try
        {
            Assert.Equal(file, new PathExecutableLocator(() => directory).Resolve(name));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
