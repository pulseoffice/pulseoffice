using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxDefaultTabStopTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void LaDistanzaPredefinitaDelleTabulazioniDiventaDefaultTabStopESiRilegge()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.DefaultTabInterval = 35.45;
        document.Changes.IsRecording = true;
        document.Changes.ProtectionKey = [1, 2, 3];

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
        {
            var settings = XDocument.Load(zip.GetEntry("word/settings.xml")!.Open()).Root!;
            Assert.Equal("709", (string?)settings.Element(W + "defaultTabStop")?.Attribute(W + "val"));
            var order = settings.Elements().Select(element => element.Name.LocalName).ToList();
            Assert.True(order.IndexOf("trackRevisions") < order.IndexOf("documentProtection"));
            Assert.True(order.IndexOf("documentProtection") < order.IndexOf("defaultTabStop"));
        }
        stream.Position = 0;
        Assert.Equal(35.45, _composer.Reader(DocxFlavour.Document).Read(stream).Value!.Styles.DefaultTabInterval, 2);
    }

    [Fact]
    public void SenzaDefaultTabStopValeIlPredefinitoDiWord()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(new TextDocument(new DefaultStyleSheetFactory().Create()), stream).Succeeded);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("word/settings.xml")!;
            XDocument settings;
            using (var input = entry.Open()) settings = XDocument.Load(input);
            settings.Root!.Element(W + "defaultTabStop")!.Remove();
            entry.Delete();
            using var output = zip.CreateEntry("word/settings.xml").Open();
            settings.Save(output);
        }
        stream.Position = 0;
        Assert.Equal(36, _composer.Reader(DocxFlavour.Document).Read(stream).Value!.Styles.DefaultTabInterval, 3);
    }
}
