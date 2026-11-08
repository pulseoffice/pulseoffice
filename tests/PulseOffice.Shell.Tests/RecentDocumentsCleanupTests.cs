using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class RecentDocumentsCleanupTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recent-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void UnaScritturaFallitaNonLasciaIlFileTemporaneo()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "recent.json");
        Directory.CreateDirectory(target);

        var recent = new RecentDocuments(NullLogger<RecentDocuments>.Instance, target);
        recent.Remember(Path.Combine(_directory, "a.ods"), "a");

        Assert.False(File.Exists(target + ".tmp"));
        Assert.Single(recent.Items);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
