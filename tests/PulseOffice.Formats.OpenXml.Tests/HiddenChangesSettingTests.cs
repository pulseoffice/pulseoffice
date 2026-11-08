using System.IO.Compression;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class HiddenChangesSettingTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Hidden()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Changes.Add(new Insertion("ins1", "a", DateTimeOffset.UnixEpoch));
        document.Body.Replace([new ParagraphBlock("p1", "abc") { Marks = [new ChangeMark(0, "ins1", MarkEdge.Start), new ChangeMark(1, "ins1", MarkEdge.End)] }]);
        document.Changes.ShowChanges = false;
        return document;
    }

    [Fact]
    public void Le_revisioni_nascoste_restano_nascoste_in_ODT_anche_per_le_altre_suite_e_in_DOCX()
    {
        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(Hidden(), odt).Succeeded);
        using (var archive = new ZipArchive(new MemoryStream(odt.ToArray())))
        using (var reader = new StreamReader(archive.GetEntry("settings.xml")!.Open()))
            Assert.Contains("\"ShowRedlineChanges\" config:type=\"boolean\">false<", reader.ReadToEnd(), StringComparison.Ordinal);
        odt.Position = 0;
        Assert.False(new OdtReader().Read(odt).Value!.Changes.ShowChanges);

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Hidden(), docx).Succeeded);
        docx.Position = 0;
        Assert.False(_composer.Reader(DocxFlavour.Document).Read(docx).Value!.Changes.ShowChanges);
    }
}
