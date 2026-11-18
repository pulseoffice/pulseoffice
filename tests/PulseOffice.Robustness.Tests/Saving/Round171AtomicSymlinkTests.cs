using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Robustness.Tests.Saving;

public sealed class Round171AtomicSymlinkTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulse-r171-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Saving_through_a_symbolic_link_writes_the_target_and_keeps_the_link()
    {
        if (OperatingSystem.IsWindows()) return;
        var target = Path.Combine(_folder, "real.ods");
        var link = Path.Combine(_folder, "link.ods");
        await File.WriteAllTextAsync(target, "old", TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(link, target);

        using var content = new MemoryStream("new"u8.ToArray());
        await new AtomicFileWriter().ReplaceAsync(link, content, TestContext.Current.CancellationToken);

        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("new", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
    }
}
