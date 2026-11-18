using System.Text.RegularExpressions;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class Round201IconButtonNameTests
{
    private static readonly Regex IconOnlyFlat = new(@"var (\w+) = Context\.Flat\(Context\.(?:Chevron|Icon)\([^()]*(?:\([^()]*\))?[^()]*\)[,)]", RegexOptions.Compiled);
    private static readonly Regex IconOnlyField = new(@"(_\w+) = Context\.Flat\(Context\.(?:Chevron|Icon)\([^()]*(?:\([^()]*\))?[^()]*\)[,)]", RegexOptions.Compiled);
    private static readonly Regex IconOnlyQuiet = new(@"var (\w+) = _parts\.Quiet\(string\.Empty,", RegexOptions.Compiled);

    [Fact]
    public void Icon_only_buttons_built_from_shared_parts_have_an_accessible_name()
    {
        var sources = Sources();
        var missing = new List<string>();
        foreach (var file in sources.EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            if (file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            var text = File.ReadAllText(file.FullName);
            foreach (var regex in new[] { IconOnlyFlat, IconOnlyField, IconOnlyQuiet })
                foreach (Match match in regex.Matches(text))
                {
                    var name = match.Groups[1].Value;
                    if (!text.Contains($"AutomationProperties.SetName({name},", StringComparison.Ordinal)
                        && !IsNamedInsideFactory(text, match.Index))
                        missing.Add($"{Path.GetRelativePath(sources.FullName, file.FullName)}:{name}");
                }
        }

        Assert.True(missing.Count == 0, string.Join(";", missing));
    }

    private static bool IsNamedInsideFactory(string text, int index)
    {
        var end = text.IndexOf("return button;", index, StringComparison.Ordinal);
        return end > 0 && text.AsSpan(index, end - index).Contains("AutomationProperties.SetName(button,", StringComparison.Ordinal);
    }

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
