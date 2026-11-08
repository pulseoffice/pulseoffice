using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class FolioCorpusParityTests(ITestOutputHelper output)
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly BlockTree _tree = new();
    private readonly StoryCatalog _stories = new(new BlockTree());

    private Dictionary<string, long> Measure(TextDocument document)
    {
        var removed = document.Changes.All.OfType<Deletion>().SelectMany(deletion => _tree.Paragraphs(deletion.Removed));
        return new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["notes"] = document.Notes.All.Count,
            ["comments"] = document.Comments.All.Count,
            ["objects"] = document.Objects.All.Count,
            ["tables"] = _stories.Stories(document).SelectMany(_tree.Blocks).OfType<TableBlock>().Count(),
            ["text"] = string.Concat(_tree.Paragraphs(document.Body).Concat(removed).Select(paragraph => paragraph.Text)).Count(c => !char.IsWhiteSpace(c) && c != '￼'),
        };
    }

    private TextDocument? Read(string extension, Stream stream)
    {
        var read = extension == ".docx" ? _composer.Reader(DocxFlavour.Document).Read(stream) : new OdtReader().Read(stream);
        return read.Succeeded ? read.Value : null;
    }

    [Fact]
    public void NoteCommentiOggettiTabelleETestoNonSiPerdonoInSilenzioFraOdtEDocx()
    {
        Assert.SkipUnless(Directory.Exists(Corpus), "Reference corpus not available.");
        var files = Directory.EnumerateFiles(Corpus, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".odt", StringComparison.Ordinal) || path.EndsWith(".fodt", StringComparison.Ordinal) || path.EndsWith(".docx", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToList();
        var silent = new List<string>();
        var compared = 0;
        foreach (var file in files)
        {
            var extension = file.EndsWith(".docx", StringComparison.Ordinal) ? ".docx" : ".odt";
            var source = Read(extension, new MemoryStream(File.ReadAllBytes(file)));
            if (source is null) continue;
            var before = Measure(source);
            foreach (var target in new[] { ".odt", ".docx" })
            {
                var losses = target == ".odt" ? new OdtWriter().CheckFidelity(source).Losses : _composer.Writer(DocxFlavour.Document).CheckFidelity(source).Losses;
                using var stream = new MemoryStream();
                var written = target == ".odt" ? new OdtWriter().Write(source, stream) : _composer.Writer(DocxFlavour.Document).Write(source, stream);
                Assert.True(written.Succeeded, file);
                stream.Position = 0;
                var copy = Read(target, stream);
                Assert.True(copy is not null, $"{file} -> {target} cannot be read again");
                compared++;
                if (losses.Count > 0) continue;
                var after = Measure(copy!);
                foreach (var (metric, value) in before)
                    if (after[metric] < value) silent.Add($"{Path.GetFileName(file)} -> {target}: {metric} {value} -> {after[metric]}");
            }
        }

        output.WriteLine($"{compared} round trips");
        Assert.True(silent.Count == 0, string.Join(Environment.NewLine, silent));
    }
}
