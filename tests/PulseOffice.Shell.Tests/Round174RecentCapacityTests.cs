using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round174RecentCapacityTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-recent-174-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Lowering_the_shown_count_for_a_while_does_not_forget_the_older_documents()
    {
        var shown = 1;
        var recent = new RecentDocuments(NullLogger<RecentDocuments>.Instance, Path.Combine(_folder, "recent.json"), () => shown);
        foreach (var name in new[] { "a", "b", "c" })
        {
            var path = Path.Combine(_folder, name + ".ods");
            File.WriteAllText(path, string.Empty);
            recent.Remember(path, name);
        }

        Assert.Single(recent.Items);
        shown = 3;
        Assert.Equal(["c", "b", "a"], recent.Items.Select(item => item.Name));
    }
}
