using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtReferenceCorpusTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa";

    [Fact]
    public void ITestDiWriterSiLeggonoESiRiscrivonoSenzaEccezioni()
    {
        if (!Directory.Exists(Corpus)) Assert.Skip("Reference corpus not available.");

        var files = Directory.EnumerateFiles(Corpus, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".odt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".fodt", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();

        var crashes = new List<string>();
        var read = 0;
        var rejected = new List<string>();
        foreach (var path in files)
        {
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                var result = new OdtReader().Read(stream);
                if (!result.Succeeded)
                {
                    rejected.Add($"{Path.GetFileName(path)}: {result.Diagnostics.FirstOrDefault()?.Code} {result.Diagnostics.FirstOrDefault()?.Message}");
                    continue;
                }

                read++;
                using var output = new MemoryStream();
                var written = new OdtWriter().Write(result.Value!, output);
                Assert.True(written.Succeeded, path);
                output.Position = 0;
                var again = new OdtReader().Read(output);
                if (!again.Succeeded) crashes.Add($"{path}: reread {again.Diagnostics.FirstOrDefault()?.Message}");
            }
            catch (Exception ex)
            {
                crashes.Add($"{path}: {ex.GetType().Name} {ex.Message}");
            }
        }

        TestContext.Current.SendDiagnosticMessage($"ODT corpus: {files.Count} files, {read} read, {rejected.Count} rejected ({string.Join(", ", rejected.Take(40))})");
        Assert.Empty(crashes);
        Assert.True(read >= 100, $"only {read} files read");
    }
}
