using System.IO.Compression;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlDiagramAuditTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/smartart1.pptx";
    private const string Document = "{9E51BB61-55F5-4BAC-A962-B83ED7F2911C}";
    private const string FirstNode = "{6A927B50-9A97-4D47-85E1-72A566B6C5F9}";

    private static MemoryStream WithExtraConnections(params string[] connections) =>
        Edited("ppt/diagrams/data1.xml", "<dgm:cxnLst>", "<dgm:cxnLst>" + string.Concat(connections));

    private static MemoryStream Edited(string part, params string[] replacements)
    {
        var buffer = new MemoryStream();
        using (var source = File.OpenRead(Corpus)) source.CopyTo(buffer);
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(part)!;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            for (var index = 0; index + 1 < replacements.Length; index += 2)
                xml = xml.Replace(replacements[index], replacements[index + 1], StringComparison.Ordinal);
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(part).Open());
            writer.Write(xml);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static string ParentOf(string source, string destination, int order) =>
        $"<dgm:cxn modelId=\"{{0000000{order + 2}-0000-0000-0000-000000000000}}\" srcId=\"{source}\" destId=\"{destination}\" srcOrd=\"{order}\" destOrd=\"0\"/>";

    [Fact]
    public void A_cycle_of_parents_in_the_diagram_data_does_not_hang_or_crash_the_reader()
    {
        Assert.SkipWhen(!File.Exists(Corpus), "Reference corpus not available.");
        using var stream = WithExtraConnections(ParentOf(FirstNode, Document, -1));

        var result = new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(stream);

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Value!.Slides[0].Elements);
    }

    [Fact]
    public async Task An_enormous_font_size_constraint_does_not_stall_the_text_fitting()
    {
        Assert.SkipWhen(!File.Exists(Corpus), "Reference corpus not available.");
        using var stream = Edited("ppt/diagrams/layout1.xml", "forName=\"node\" op=\"equ\" val=\"65\"", "forName=\"node\" op=\"equ\" val=\"1e300\"");

        var reading = Task.Run(() => new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(stream), TestContext.Current.CancellationToken);

        var finished = await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.Same(reading, finished);
        Assert.True((await reading).Succeeded);
    }
}
