using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Presentations;

namespace PulseOffice.Robustness.Tests.Fuzzing;

public sealed record Seed(string Name, DocumentFamily Family, byte[] Content);

public sealed class SeedCorpus
{
    private const int MaximumSeedBytes = 160_000;

    private static readonly Dictionary<string, DocumentFamily> Families = new(StringComparer.OrdinalIgnoreCase)
    {
        [".ods"] = DocumentFamily.Spreadsheet, [".fods"] = DocumentFamily.Spreadsheet, [".xlsx"] = DocumentFamily.Spreadsheet,
        [".xlsm"] = DocumentFamily.Spreadsheet, [".csv"] = DocumentFamily.Spreadsheet,
        [".odt"] = DocumentFamily.Text, [".fodt"] = DocumentFamily.Text, [".docx"] = DocumentFamily.Text, [".docm"] = DocumentFamily.Text,
        [".dotx"] = DocumentFamily.Text, [".rtf"] = DocumentFamily.Text, [".html"] = DocumentFamily.Text, [".htm"] = DocumentFamily.Text,
        [".xhtml"] = DocumentFamily.Text, [".txt"] = DocumentFamily.Text,
        [".odp"] = DocumentFamily.Presentation, [".fodp"] = DocumentFamily.Presentation, [".pptx"] = DocumentFamily.Presentation,
    };

    public IReadOnlyList<Seed> Load(int perExtension)
    {
        var root = RepositoryRoot();
        var fixtures = Directory.EnumerateDirectories(Path.Combine(root, "tests"), "Fixtures", SearchOption.AllDirectories)
            .Where(directory => !directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            .Where(file => Families.ContainsKey(Path.GetExtension(file)))
            .Select(file => new FileInfo(file))
            .Where(file => file.Length is > 0 and <= MaximumSeedBytes)
            .OrderBy(file => file.FullName, StringComparer.Ordinal)
            .GroupBy(file => file.Extension.ToLowerInvariant())
            .SelectMany(group => group.Take(perExtension))
            .Select(file => new Seed(Path.GetRelativePath(root, file.FullName), Families[file.Extension], File.ReadAllBytes(file.FullName)))
            .ToList();

        fixtures.AddRange(Generated(fixtures));
        fixtures.Add(new Seed("generated.txt", DocumentFamily.Text, "Prima riga\nSeconda riga\n\tcon tabulazione\n"u8.ToArray()));
        return fixtures;
    }

    private IEnumerable<Seed> Generated(IReadOnlyList<Seed> fixtures)
    {
        using var readers = new DocumentReaders();
        var masters = new MasterFactory();
        var themes = new ThemeCatalog();

        if (fixtures.FirstOrDefault(seed => seed.Name.EndsWith(".pptx", StringComparison.Ordinal)) is { } deckSeed
            && new PptxReader(themes, masters).Read(new MemoryStream(deckSeed.Content)).Value is { } deck)
        {
            yield return new Seed("generated.odp", DocumentFamily.Presentation, Write(stream => new OdpWriter().Write(deck, stream)));
            yield return new Seed("generated.fodp", DocumentFamily.Presentation, Write(stream => new FlatOdpWriter().Write(deck, stream)));
        }

        if (fixtures.FirstOrDefault(seed => seed.Name.EndsWith(".odt", StringComparison.Ordinal)) is { } textSeed
            && new OdtReader().Read(new MemoryStream(textSeed.Content)).Value is { } text)
        {
            var composer = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory());
            yield return new Seed("generated.fodt", DocumentFamily.Text, Write(stream => new FlatOdtWriter(new OdtWriter()).Write(text, stream)));
            yield return new Seed("generated.docm", DocumentFamily.Text, Write(stream => composer.Writer(DocxFlavour.MacroEnabled).Write(text, stream)));
            yield return new Seed("generated.dotx", DocumentFamily.Text, Write(stream => composer.Writer(DocxFlavour.Template).Write(text, stream)));
        }
    }

    private byte[] Write(Action<Stream> write)
    {
        using var stream = new MemoryStream();
        write(stream);
        return stream.ToArray();
    }

    public string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
