using System.Text.RegularExpressions;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class Round253TextDecorationTests
{
    private static readonly Regex StaticCollection = new(
        @"(?<![\w.])TextDecorations\.(Underline|Strikethrough|Overline|Baseline)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void Source_never_uses_the_shared_static_text_decoration_collections()
    {
        var sources = Sources();
        var offenders = new List<string>();
        foreach (var file in sources.EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sources.FullName, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (StaticCollection.IsMatch(File.ReadAllText(file.FullName))) offenders.Add(relative);
        }

        Assert.True(offenders.Count == 0, "Use ITextDecorationFactory instead of the static TextDecorations collections:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
