using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class WindowPlacementStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-window-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void UnFileIlleggibileNonFermaLAvvio()
    {
        if (OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "window.json");
        File.WriteAllText(path, "{}");
        File.SetUnixFileMode(path, UnixFileMode.None);

        try
        {
            using var probe = File.OpenRead(path);
            return;
        }
        catch (UnauthorizedAccessException)
        {
        }

        Assert.Null(new WindowPlacementStore(NullLogger<WindowPlacementStore>.Instance, path).Load());
    }

    [Fact]
    public void UnaMisuraNonFinitaNonFaCadereLaChiusura()
    {
        var path = Path.Combine(_directory, "window.json");
        var store = new WindowPlacementStore(NullLogger<WindowPlacementStore>.Instance, path);

        store.Save(new PulseOffice.Foundation.Windowing.WindowPlacement(0, 0, double.NaN, double.PositiveInfinity, false));

        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_directory)) return;
        if (!OperatingSystem.IsWindows())
            foreach (var file in Directory.GetFiles(_directory))
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.Delete(_directory, true);
    }
}
