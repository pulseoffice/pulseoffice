using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round288RecentPinningTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-recent-288-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private RecentDocuments Open(int capacity) =>
        new(NullLogger<RecentDocuments>.Instance, Path.Combine(_folder, "recent.json"), capacity);

    private string Touch(string name)
    {
        var path = Path.Combine(_folder, name + ".ods");
        File.WriteAllText(path, string.Empty);
        return path;
    }

    [Fact]
    public void A_pinned_document_stays_first_survives_capacity_clearing_and_restarts()
    {
        var recent = Open(1);
        var a = Touch("a");
        recent.Remember(a, "a");
        recent.SetPinned(a, true);
        recent.Remember(Touch("b"), "b");
        recent.Remember(Touch("c"), "c");

        Assert.Equal(["a", "c"], recent.Items.Select(item => item.Name));

        recent.Clear();
        File.Delete(a);
        var reopened = Open(1);

        var only = Assert.Single(reopened.Items);
        Assert.True(only.Pinned);

        reopened.SetPinned(only.Path, false);
        Assert.False(Assert.Single(reopened.Items).Pinned);
    }
}
