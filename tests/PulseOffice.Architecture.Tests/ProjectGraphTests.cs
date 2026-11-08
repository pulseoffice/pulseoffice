using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class ProjectGraphTests
{
    private readonly Regex core = new(
        "^PulseOffice\\.(Foundation|Document(\\..+)?|Drawing(\\..+)?|Text(\\..+)?|Charts|Sheets(\\..+)?|Documents(\\..+)?|Presentations(\\..+)?|Formats(\\..+)?)$",
        RegexOptions.CultureInvariant);

    private DirectoryInfo sources => LocateSources();

    [Fact]
    public void IlNucleoNonReferenziaAvaloniaNeancheTransitivamente()
    {
        var cores = sources.GetDirectories("PulseOffice.*")
            .Select(directory => directory.Name)
            .Where(name => core.IsMatch(name) && !name.EndsWith(".UI", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(cores);

        var problems = new List<string>();
        foreach (var project in cores)
        {
            foreach (var (owner, package) in PackagesInClosure(project))
                if (package.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase)) problems.Add($"{project} -> {owner} -> {package}");
            foreach (var reached in ProjectClosure(project))
                if (reached.EndsWith(".UI", StringComparison.Ordinal) || reached.StartsWith("PulseOffice.UI", StringComparison.Ordinal) || reached == "PulseOffice.Shell")
                    problems.Add($"{project} -> {reached}");
        }

        Assert.Empty(problems);
    }

    private IEnumerable<(string Owner, string Package)> PackagesInClosure(string project) =>
        ProjectClosure(project).Append(project).SelectMany(name => Load(name)
            .Descendants("PackageReference")
            .Select(element => (name, (string?)element.Attribute("Include") ?? "")));

    private IReadOnlySet<string> ProjectClosure(string project)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(References(project));
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (!seen.Add(next)) continue;
            foreach (var reference in References(next)) pending.Push(reference);
        }
        return seen;
    }

    private IEnumerable<string> References(string project) =>
        Load(project).Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(((string?)element.Attribute("Include") ?? "").Replace('\\', '/')));

    private XDocument Load(string project) =>
        XDocument.Load(Path.Combine(sources.FullName, project, project + ".csproj"));

    private DirectoryInfo LocateSources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
