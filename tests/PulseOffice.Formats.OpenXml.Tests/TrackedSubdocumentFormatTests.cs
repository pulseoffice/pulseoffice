using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class TrackedSubdocumentFormatTests
{
    private sealed class FixedAuthor : IRevisionAuthor
    {
        public RevisionSignature Sign() => new("Anna", new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero));
    }

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ServiceProvider _services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(new FixedAuthor()).BuildServiceProvider();

    private ISubdocuments Subdocuments => _services.GetRequiredService<ISubdocuments>();

    private IBlockTree Tree => _services.GetRequiredService<IBlockTree>();

    private TextDocument Chapter(params string[] lines)
    {
        var chapter = _services.GetRequiredService<ITextDocumentFactory>().Create();
        chapter.Body.Replace(lines.Select((line, index) => new ParagraphBlock(chapter.NextId("p"), line, index == 0 ? DefaultStyleNames.HeadingLevel(1) : DefaultStyleNames.Standard)));
        return chapter;
    }

    private TextDocument Master()
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        var history = new UndoStack();
        var first = (ParagraphBlock)document.Body[0];
        _services.GetRequiredService<IDocumentEditor>().InsertText(document, history, new DocumentPosition(first.Id, 0), "Master");
        document.Changes.IsRecording = true;
        return document;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private IEnumerable<TextDocument> BothFormats(TextDocument document) =>
    [
        Through(document, new OdtWriter(), new OdtReader()),
        Through(document, _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
    ];

    private IEnumerable<string> Texts(TextDocument document) => Tree.Paragraphs(document.Body).Select(paragraph => paragraph.Text);

    private void Resolve(TextDocument document, RevisionVerdict verdict) =>
        _services.GetRequiredService<IRevisionResolver>().ResolveAll(document, new UndoStack(), verdict);

    [Fact]
    public void IlSottodocumentoInseritoConLeRevisioniSiRileggeComeInserimentoInOdtEDocx()
    {
        var document = Master();
        Subdocuments.Insert(document, new UndoStack(), ((ParagraphBlock)document.Body[0]).Id, Chapter("Capitolo", "Testo"), "c1.odt", "c1");

        foreach (var read in BothFormats(document))
        {
            Assert.Equal(["Master", "Capitolo", "Testo"], Texts(read));
            var insertion = Assert.IsType<Insertion>(Assert.Single(read.Changes.All));
            Assert.Equal("Anna", insertion.Author);
            Assert.Single(Subdocuments.Linked(read));

            Resolve(read, RevisionVerdict.Reject);

            Assert.Equal(["Master"], Texts(read));
            Assert.Empty(read.Changes.All);
        }
    }

    [Fact]
    public void LAggiornamentoRegistratoSiRileggeInOdtEDocxESiRifiutaOSiAccetta()
    {
        var document = Master();
        Subdocuments.Insert(document, new UndoStack(), ((ParagraphBlock)document.Body[0]).Id, Chapter("Capitolo", "Vecchio"), "c1.odt", "c1");
        Resolve(document, RevisionVerdict.Accept);
        var section = Assert.Single(Subdocuments.Linked(document));
        Subdocuments.Refresh(document, new UndoStack(), section.Id, Chapter("Capitolo nuovo", "Nuovo"));

        foreach (var verdict in new[] { RevisionVerdict.Accept, RevisionVerdict.Reject })
        {
            foreach (var read in BothFormats(document))
            {
                Assert.Equal("c1.odt", Assert.Single(Subdocuments.Linked(read)).LinkedFile);
                Assert.Contains(read.Changes.All, change => change is Deletion);
                Assert.Contains(read.Changes.All, change => change is Insertion);

                Resolve(read, verdict);

                Assert.Equal(verdict == RevisionVerdict.Accept ? ["Master", "Capitolo nuovo", "Nuovo"] : ["Master", "Capitolo", "Vecchio"], Texts(read));
                Assert.Empty(read.Changes.All);
                Assert.Equal("c1.odt", Assert.Single(Subdocuments.Linked(read)).LinkedFile);
            }
        }
    }
}
