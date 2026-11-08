using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class MailMergeTests
{
    private readonly ServiceProvider _services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();

    private static readonly MergeData Data = new(["Nome", "Città"], [["Anna", "Roma"], ["Bruno", "Torino"], ["Carla", "Bari"]]);

    private TextDocument Letter()
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        var runs = new[]
        {
            new TextRun("Cara "),
            new TextRun("«Nome»") { Field = TextFieldKind.Database, FieldArgument = "Nome" },
            new TextRun(" di "),
            new TextRun("«Città»") { Field = TextFieldKind.Database, FieldArgument = "città" },
        };
        var greeting = new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs, StyleName = "Heading 1" })
        {
            Marks = [new BookmarkMark(22, "fine", MarkEdge.Point)],
        };
        document.Body.Replace([greeting, new ParagraphBlock(document.NextId("p"), "Saluti.")]);
        return document;
    }

    private IMailMergeEngine Engine => _services.GetRequiredService<IMailMergeEngine>();

    [Fact]
    public void UnRecordSostituisceICampiESpostaISegni()
    {
        var record = Engine.Record(Letter(), Data, 1);
        var first = record.Body.Blocks.OfType<ParagraphBlock>().First();

        Assert.Equal("Cara Bruno di Torino", first.Text);
        Assert.All(first.Content.Runs, run => Assert.Equal(TextFieldKind.None, run.Field));
        Assert.Equal(20, first.Marks.OfType<BookmarkMark>().Single().Offset);
    }

    [Fact]
    public void IlDocumentoUnicoMetteOgniLetteraSuUnaPaginaNuova()
    {
        var combined = Engine.Combine(Letter(), Data, [0, 1, 2]);
        var paragraphs = combined.Body.Blocks.OfType<ParagraphBlock>().ToList();

        Assert.Equal(["Cara Anna di Roma", "Saluti.", "Cara Bruno di Torino", "Saluti.", "Cara Carla di Bari", "Saluti."], paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal(paragraphs.Count, paragraphs.Select(paragraph => paragraph.Id).Distinct().Count());
        Assert.True(paragraphs[2].Content.PageBreakBefore);
        Assert.Equal("Heading 1", paragraphs[4].Content.StyleName);
    }

    [Fact]
    public void IlCampoMostraIlSegnapostoOIlValoreInAnteprima()
    {
        var document = Letter();
        var records = _services.GetRequiredService<IMergeRecords>();
        var updater = _services.GetRequiredService<IFieldUpdater>();
        records.Load(document, Data);

        records.Preview(document, true);
        records.Move(document, 2);
        updater.Update(document, new UndoStack(), new FieldUpdate(new NoFieldLayoutFacts(), DateTime.Now));
        var previewed = document.Body.Blocks.OfType<ParagraphBlock>().First().Text;
        records.Preview(document, false);
        updater.Update(document, new UndoStack(), new FieldUpdate(new NoFieldLayoutFacts(), DateTime.Now));

        Assert.Equal("Cara Carla di Bari", previewed);
        Assert.Equal("Cara «Nome» di «città»", document.Body.Blocks.OfType<ParagraphBlock>().First().Text);
    }

    [Fact]
    public void ICampiDelModelloSiElencanoUnaVolta()
    {
        Assert.Equal(["Nome", "città"], Engine.Fields(Letter()));
    }
}
