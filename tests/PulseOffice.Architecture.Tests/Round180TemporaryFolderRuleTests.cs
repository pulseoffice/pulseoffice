using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class Round180TemporaryFolderRuleTests
{
    private static readonly string[] SharedTemporaryCalls = ["Path.GetTempPath(", "Path.GetTempFileName(", "Directory.CreateTempSubdirectory("];

    [Fact]
    public void Only_the_platform_projects_touch_the_shared_temporary_folder()
    {
        var sources = Sources();
        var found = sources.EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !Path.GetRelativePath(sources.FullName, file.FullName).StartsWith("PulseOffice.Platform", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file.FullName) is var text && SharedTemporaryCalls.Any(call => text.Contains(call, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(sources.FullName, file.FullName))
            .ToList();

        Assert.True(found.Count == 0, string.Join(";", found));
    }

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
