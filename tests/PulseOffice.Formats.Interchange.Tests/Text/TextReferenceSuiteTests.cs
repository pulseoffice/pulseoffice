using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.Odf.Tests.Odt;
using PulseOffice.Drawing.Assets;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class TextReferenceSuiteTests
{
    private const string ReferenceFixtures = "Fixtures/Text/Reference";
    private const string Ours = "Fixtures/Text/Ours";
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa";

    private readonly TextInterchange _formats = new();
    private readonly TextDocumentEssence _essence = new();

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(ReferenceFixtures, "*.fodt").Order(StringComparer.Ordinal)) data.Add(Path.GetFileNameWithoutExtension(path));
        return data;
    }

    private TextDocument Read(string path, ITextDocumentReader reader) => TextInterchange.Read(File.ReadAllBytes(path), reader);

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void LRtfScrittoDaUnAltraSuiteSiLeggeSenzaPerdite(string name)
    {
        var original = _essence.Describe(Read(Path.Combine(ReferenceFixtures, name + ".fodt"), new OdtReader()));
        Assert.Equal(original, _essence.Describe(Read(Path.Combine(ReferenceFixtures, name + ".rtf"), _formats.RtfReader)));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void LHtmlScrittoDaUnAltraSuiteSiLeggeSenzaPerditeNonDichiarate(string name)
    {
        var expected = Read(Path.Combine(ReferenceFixtures, name + ".fodt"), new OdtReader());
        var read = _formats.HtmlReader.Read(new MemoryStream(File.ReadAllBytes(Path.Combine(ReferenceFixtures, name + ".html"))));
        Assert.True(read.Succeeded);
        if (read.Diagnostics.Any(diagnostic => diagnostic.Code == "HTML211"))
            expected.Objects.Replace(expected.Objects.All.Where(item => item.Content is not EmbeddedContent { Value: RasterImageObject }));
        Assert.Equal(_essence.Describe(expected), _essence.Describe(read.Value!));
    }

    [Fact]
    public void IlNostroRtfApertoDaUnAltraSuiteNonPerdeNulla() =>
        Assert.Equal(_essence.Describe(TextInterchange.Rich()), _essence.Describe(Read(Path.Combine(Ours, "rtf-via-lo.odt"), new OdtReader())));

    [Fact]
    public void IlNostroHtmlApertoDaUnAltraSuiteNonPerdeNulla()
    {
        var collapsed = new TextDocumentEssence(collapseWhitespace: true);
        Assert.Equal(collapsed.Describe(TextInterchange.Rich()), collapsed.Describe(Read(Path.Combine(Ours, "html-via-lo.odt"), new OdtReader())));
    }

    [Fact]
    public void IlNostroTestoApertoDaUnAltraSuiteHaLeStesseRighe()
    {
        var lines = File.ReadAllText(Path.Combine(Ours, "sample.txt")).TrimEnd('\n').Split('\n');
        var document = Read(Path.Combine(Ours, "txt-via-lo.odt"), new OdtReader());
        Assert.Equal(lines, document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
    }

    [Fact]
    public void IlTestoEstrattoDaUnAltraSuiteContieneTuttiIParagrafi()
    {
        foreach (var file in new[] { "rtf-via-lo.txt", "html-via-lo.txt" })
        {
            var text = File.ReadAllText(Path.Combine(Ours, file), Encoding.UTF8);
            foreach (var word in new[] { "Titolo", "Ciao", "bel", "mondo è", "Primo", "Secondo", "Punto", "Pagina ", " di ", "Unita", "Fine", "a capo" })
                Assert.Contains(word, text);
        }
    }

    [Fact]
    public void GliRtfEHtmlDiWriterSiLeggonoSenzaEccezioni()
    {
        if (!Directory.Exists(Corpus)) Assert.Skip("Reference corpus not available.");
        var files = Directory.EnumerateFiles(Corpus, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();
        var crashes = new List<string>();
        var read = 0;
        foreach (var path in files)
        {
            try
            {
                var reader = path.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase) ? (ITextDocumentReader)_formats.RtfReader : _formats.HtmlReader;
                var result = reader.Read(new MemoryStream(File.ReadAllBytes(path)));
                if (!result.Succeeded) continue;
                read++;
                var writer = reader == _formats.RtfReader ? (ITextDocumentWriter)_formats.RtfWriter : _formats.HtmlWriter;
                var again = reader.Read(new MemoryStream(TextInterchange.Bytes(result.Value!, writer)));
                if (!again.Succeeded) crashes.Add($"{path}: reread {again.Diagnostics.FirstOrDefault()?.Message}");
            }
            catch (Exception exception)
            {
                crashes.Add($"{path}: {exception.GetType().Name} {exception.Message}");
            }
        }
        TestContext.Current.SendDiagnosticMessage($"RTF/HTML corpus: {files.Count} files, {read} read");
        Assert.Empty(crashes);
        Assert.True(read >= 600, $"only {read} files read");
    }
}
