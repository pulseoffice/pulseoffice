using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round290RecentInstancesTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recent290-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void Two_instances_do_not_overwrite_each_other_s_recent_documents()
    {
        Directory.CreateDirectory(_directory);
        var store = Path.Combine(_directory, "recent.json");
        var first = new RecentDocuments(NullLogger<RecentDocuments>.Instance, store);
        var second = new RecentDocuments(NullLogger<RecentDocuments>.Instance, store);

        first.Remember(Path.Combine(_directory, "a.ods"), "a.ods");
        second.Remember(Path.Combine(_directory, "b.ods"), "b.ods");

        var reloaded = new RecentDocuments(NullLogger<RecentDocuments>.Instance, store);
        Assert.Equal(["b.ods", "a.ods"], reloaded.Items.Select(item => item.Name));
    }
}
