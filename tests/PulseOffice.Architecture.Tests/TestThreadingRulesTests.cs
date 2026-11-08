using System.Xml.Linq;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class TestThreadingRulesTests
{
    private readonly IReadOnlySet<string> declarations = new HashSet<string>(StringComparer.Ordinal)
    {
        "SerialAssembly", "HeadlessCollection", "NoAvaloniaObjects",
    };

    private DirectoryInfo root => LocateRoot();

    [Fact]
    public void OgniProgettoDiTestCheRaggiungeAvaloniaDichiaraComeSerializzaIlThreadDellInterfaccia()
    {
        var tests = new DirectoryInfo(Path.Combine(root.FullName, "tests")).GetDirectories("PulseOffice.*.Tests")
            .Select(directory => Path.Combine(directory.FullName, directory.Name + ".csproj"))
            .Where(File.Exists)
            .ToList();
        Assert.NotEmpty(tests);

        var undeclared = tests
            .Where(ReachesAvalonia)
            .Where(project => !declarations.Contains(Threading(project)))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.True(undeclared.Count == 0,
            "Test projects that reach Avalonia without AvaloniaTestThreading (SerialAssembly, HeadlessCollection, NoAvaloniaObjects): " + string.Join(", ", undeclared));
    }

    [Fact]
    public void LaSerializzazioneDellAssemblyEUnSoloFileCondiviso()
    {
        var copies = new DirectoryInfo(Path.Combine(root.FullName, "tests")).GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file.FullName).Contains("[assembly: " + nameof(CollectionBehaviorAttribute)[..^"Attribute".Length], StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/'))
            .ToList();

        Assert.Equal(["tests/Shared/SingleUiThreadAssembly.cs"], copies);
    }

    private string Threading(string project) =>
        XDocument.Load(project).Descendants("AvaloniaTestThreading").Select(element => element.Value.Trim()).FirstOrDefault() ?? "";

    private bool ReachesAvalonia(string project)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([Path.GetFullPath(project)]);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (!seen.Add(next)) continue;
            var document = XDocument.Load(next);
            if (document.Descendants("PackageReference").Any(element => ((string?)element.Attribute("Include") ?? "").StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase)))
                return true;
            foreach (var reference in document.Descendants("ProjectReference"))
                pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(next)!, ((string?)reference.Attribute("Include") ?? "").Replace('\\', '/'))));
        }
        return false;
    }

    private DirectoryInfo LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return directory ?? throw new DirectoryNotFoundException("PulseOffice.slnx");
    }
}
