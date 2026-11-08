using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class DocxReferenceCorpusTests(ITestOutputHelper output)
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa";

    [Fact]
    public void IDocxEsternoSiLeggonoSenzaEccezioni()
    {
        if (!Directory.Exists(Corpus)) Assert.Skip("Reference corpus not available.");
        var composer = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory());
        var files = Directory.EnumerateFiles(Corpus, "*.docx", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Take(400).ToList();
        var opened = 0;
        var failures = new List<string>();
        foreach (var file in files)
        {
            var reader = composer.Reader(DocxFlavour.Document);
            try
            {
                using var stream = File.OpenRead(file);
                var result = reader.Read(stream);
                if (result.Succeeded)
                {
                    opened++;
                    using var copy = new MemoryStream();
                    Assert.True(composer.Writer(DocxFlavour.Document).Write(result.Value!, copy).Succeeded);
                    copy.Position = 0;
                    Assert.True(reader.Read(copy).Succeeded, file);
                }
                else output.WriteLine($"{Path.GetFileName(file)}: {result.Diagnostics[0].Message}");
            }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                failures.Add($"{Path.GetFileName(file)}: {ex.GetType().Name} {ex.Message}");
            }
        }

        output.WriteLine($"Opened {opened} of {files.Count}");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(opened >= files.Count * 0.95, $"Opened {opened} of {files.Count}");
    }
}
