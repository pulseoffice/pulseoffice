using System.Text.RegularExpressions;
using PulseOffice.Foundation.Diagnostics;
using Xunit;

namespace PulseOffice.Localization.Tests;

public sealed class DiagnosticTextsTests
{
    private static readonly Regex CodeUse = new(
        @"Diagnostic\.(?:Error|Warning|Info)\(\s*""([A-Za-z.]+[0-9]*)""",
        RegexOptions.Compiled);

    [Fact]
    public void UnDiagnosticoSiMostraNellaLinguaDellInterfaccia()
    {
        var texts = new DiagnosticTexts(new TextCatalog(UiLanguage.Italian));

        Assert.Equal("Accesso negato.", texts.Describe(Diagnostic.Error("REM011", "Access denied.")));
        Assert.Equal("Il server ha risposto con il codice 502.",
            texts.Describe(Diagnostic.Error("REM001", "The server answered 502.").With(502)));
    }

    [Fact]
    public void ConUnaSolaOccorrenzaIlMessaggioEAlSingolare()
    {
        var texts = new DiagnosticTexts(new TextCatalog(UiLanguage.Italian));

        Assert.StartsWith("1 oggetto non viene", texts.Describe(Diagnostic.Warning("HTML002", "Objects are not written.").With(1)));
        Assert.StartsWith("3 oggetti non vengono", texts.Describe(Diagnostic.Warning("HTML002", "Objects are not written.").With(3)));
    }

    [Fact]
    public void UnCodiceSenzaTraduzioneMostraIlMessaggioCanonico()
    {
        var texts = new DiagnosticTexts(new TextCatalog(UiLanguage.Italian));

        Assert.Equal("Something went wrong.", texts.Describe(Diagnostic.Error("NOPE999", "Something went wrong.")));
    }

    [Theory]
    [InlineData(UiLanguage.Italian)]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.Spanish)]
    [InlineData(UiLanguage.French)]
    public void OgniCodiceDiagnosticoHaLaSuaTraduzione(UiLanguage language)
    {
        var keys = new TextCatalog(language).KeysIn(language).ToHashSet(StringComparer.Ordinal);
        var source = Path.Combine(RepositoryRoot(), "src");

        var missing = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => CodeUse.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
            .Where(code => !code.StartsWith("PVT", StringComparison.Ordinal))
            .Select(code => Diagnostic.Error(code, string.Empty).MessageKey)
            .Where(key => !keys.Contains(key))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void UnCodiceDiPerditaNonSiUsaPerDuePerditeDiverse()
    {
        var losses = new Regex(@"Diagnostic\.(?:Warning|Info)\(\s*""([A-Z]+[0-9]+)"",\s*\$?""([^""]*)""");
        var source = Path.Combine(RepositoryRoot(), "src");
        var clashes = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => losses.Matches(File.ReadAllText(path)).Select(match => (Code: match.Groups[1].Value, Message: match.Groups[2].Value)))
            .Distinct()
            .GroupBy(use => use.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(clashes);
    }

    [Fact]
    public void NessunaChiaveDelleTraduzioniCompareDueVolte()
    {
        var resources = Path.Combine(RepositoryRoot(), "src", "PulseOffice.Localization", "Resources");
        var key = new Regex(@"^\s*""([^""]+)""\s*:", RegexOptions.Multiline);
        var duplicated = Directory.EnumerateFiles(resources, "*.json")
            .SelectMany(path => key.Matches(File.ReadAllText(path)).Select(match => Path.GetFileName(path) + ":" + match.Groups[1].Value)
                .GroupBy(entry => entry, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
            .ToList();

        Assert.Empty(duplicated);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) return directory.FullName;

        throw new DirectoryNotFoundException("PulseOffice.slnx not found above the test output.");
    }
}
