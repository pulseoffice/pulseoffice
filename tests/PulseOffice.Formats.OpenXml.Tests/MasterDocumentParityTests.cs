using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Formats.Packaging;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class MasterDocumentParityTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Master()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.IsMaster = true;
        document.Body.Add(new SectionBlock(document.NextId("s"), "capitolo1",
            new BlockContainer([new ParagraphBlock(document.NextId("p"), "Capitolo uno")])) { LinkedFile = "capitolo1.odt" });
        return document;
    }

    private static TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader, out byte[] bytes)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        bytes = stream.ToArray();
        stream.Position = 0;
        Assert.True(reader.CanRead(stream));
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    [Fact]
    public void IlDocumentoMasterAttraversaOdmEOdtConISottodocumenti()
    {
        foreach (var writer in new[] { new OdtWriter(OdtFlavour.Master), new OdtWriter() })
        {
            var back = Through(Master(), writer, new OdtReader(), out var bytes);
            Assert.True(back.IsMaster);
            var section = Assert.Single(back.Body.Blocks.OfType<SectionBlock>());
            Assert.Equal("capitolo1.odt", section.LinkedFile);
            Assert.Equal("Capitolo uno", Assert.IsType<ParagraphBlock>(Assert.Single(section.Content.Blocks)).Text);
            using var package = OdfPackage.OpenRead(new MemoryStream(bytes));
            Assert.Equal(writer.Format.MimeType, package.ReadMimeType());
        }
    }

    [Fact]
    public void InDocxIlMasterDiventaUnDocumentoNormaleEloDichiara()
    {
        var back = Through(Master(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document), out _);

        Assert.False(back.IsMaster);
        Assert.Contains("Capitolo uno", back.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text).Concat(
            back.Body.Blocks.OfType<SectionBlock>().SelectMany(section => section.Content.Blocks.OfType<ParagraphBlock>()).Select(paragraph => paragraph.Text)));
        var losses = _composer.Writer(DocxFlavour.Document).CheckFidelity(Master()).Losses.Select(loss => loss.Code).ToList();
        Assert.Contains("DOCX029", losses);
        Assert.DoesNotContain("DOCX003", losses);
        Assert.Equal("capitolo1.odt", Assert.Single(back.Body.Blocks.OfType<SectionBlock>()).LinkedFile);
        Assert.DoesNotContain(new OdtWriter(OdtFlavour.Master).CheckFidelity(Master()).Losses, loss => loss.Code == "DOCX029");
    }

    [Fact]
    public void IFileDiPartenzaDelMasterSonoQuelliDelCampione()
    {
        if (Environment.GetEnvironmentVariable("PULSE_MASTER_FIXTURES") is not { Length: > 0 } folder) return;
        var chapter = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        chapter.Body.Replace([new ParagraphBlock(chapter.NextId("p"), "Capitolo uno")]);
        using (var file = File.Create(Path.Combine(folder, "capitolo1.odt"))) Assert.True(new OdtWriter().Write(chapter, file).Succeeded);
        using (var file = File.Create(Path.Combine(folder, "master.odm"))) Assert.True(new OdtWriter(OdtFlavour.Master).Write(Master(), file).Succeeded);
    }

    [Fact]
    public void UnAltraSuiteRileggeIlMasterEIlCollegamento()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Master");
        foreach (var name in new[] { "master.lo.odm", "master.lo.odt" })
        {
            using var stream = File.OpenRead(Path.Combine(folder, name));
            var read = new OdtReader().Read(stream);
            Assert.True(read.Succeeded, name);
            var section = read.Value!.Body.Blocks.OfType<SectionBlock>().Single(candidate => candidate.LinkedFile is not null);
            Assert.EndsWith("capitolo1.odt", section.LinkedFile, StringComparison.Ordinal);
            if (name.EndsWith(".odt", StringComparison.Ordinal))
                Assert.Contains("Capitolo uno", section.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
            else
                Assert.True(read.Value.IsMaster);
        }
    }
}
