using System.Text.RegularExpressions;
using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Localization.Tests;

public class SourceKeyTests
{
    private static readonly Regex KeyUse = new(
        @"(?:TitleKey\s*=\s*|\b[tT]exts\s*\[\s*|\b[tT]exts\.Format\(\s*)""([a-zA-Z][\w]*(?:\.[\w]+)+)""",
        RegexOptions.Compiled);

    [Fact]
    public void OgniChiaveCitataNelCodiceEsiste()
    {
        var catalog = new TextCatalog(UiLanguage.English);
        var keys = catalog.KeysIn(UiLanguage.English).ToHashSet(StringComparer.Ordinal);
        var source = Path.Combine(RepositoryRoot(), "src");

        var missing = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => KeyUse.Matches(File.ReadAllText(path))
                .Select(match => (Key: match.Groups[1].Value, File: Path.GetFileName(path))))
            .Where(use => !keys.Contains(use.Key))
            .Select(use => $"{use.Key} ({use.File})")
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) return directory.FullName;

        throw new DirectoryNotFoundException("PulseOffice.slnx not found above the test output.");
    }
}
