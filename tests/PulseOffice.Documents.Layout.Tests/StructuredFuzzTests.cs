using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class StructuredFuzzTests
{
    private string Signature(DocumentLayout layout) => string.Join('|', layout.Pages.Select(page =>
        $"{page.Index}:{page.StyleName}:{page.Start}:"
        + string.Join(',', page.AllLines.Select(line => $"{line.ParagraphId}/{line.Line.Start}-{line.Line.End}@{line.Left:F3},{line.Top:F3}"))
        + ";" + string.Join(',', page.Objects.Select(item => $"{item.Bounds.X:F3},{item.Bounds.Y:F3},{item.Bounds.Width:F3},{item.Bounds.Height:F3}"))));

    private string Text(Random random, int words) =>
        string.Join(' ', Enumerable.Range(0, words).Select(_ => new string((char)('a' + random.Next(26)), random.Next(1, 12))));

    private ParagraphBlock Paragraph(LayoutFixture f, Random random, int words)
    {
        var text = Text(random, words);
        return new ParagraphBlock(f.Document.NextId("p"), new TextParagraph(text) { Runs = text.Length == 0 ? [] : [new TextRun(text)] });
    }

    private TableBlock Table(LayoutFixture f, Random random)
    {
        var columns = random.Next(1, 4);
        var rows = Enumerable.Range(0, random.Next(1, 5))
            .Select(_ => new TableRow(Enumerable.Range(0, columns).Select(_ => new TableCell(new BlockContainer([Paragraph(f, random, random.Next(0, 30))])))))
            .ToList();
        return new TableBlock(f.Document.NextId("t"), rows, Enumerable.Repeat(Twip.FromPoints(280.0 / columns), columns)) { HeaderRows = random.Next(2), StyleName = "T" };
    }

    private void AddNote(LayoutFixture f, Random random, ParagraphBlock block)
    {
        var id = f.Document.NextId("fn");
        f.Document.Notes.Add(new Note(id, NoteKind.Footnote, new BlockContainer([Paragraph(f, random, random.Next(1, 25))])));
        block.Marks = [.. block.Marks, new NoteMark(random.Next(block.Length + 1), id)];
    }

    private void AddObject(LayoutFixture f, Random random, ParagraphBlock block)
    {
        f.Document.Objects.Add(new AnchoredObject(f.Document.NextId("o"), new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer()),
            new DocumentSize(Twip.FromPoints(random.Next(20, 120)), Twip.FromPoints(random.Next(10, 90))))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(random.Next(0, 200)), Twip.FromPoints(random.Next(0, 40))),
            Wrap = random.Next(3) switch { 0 => WrapMode.Parallel, 1 => WrapMode.Through, _ => WrapMode.None },
        });
    }

    private List<ParagraphBlock> BodyParagraphs(LayoutFixture f) => [.. f.Document.Body.Blocks.OfType<ParagraphBlock>()];

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void TabelleNoteEOggettiSiReimpaginanoComeLaReimpaginazioneCompleta(int seed)
    {
        var random = new Random(seed);
        var f = new LayoutFixture(width: 300, height: 400);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 2, BorderWidth = 0 } });
        for (var i = 0; i < random.Next(20, 60); i++)
        {
            if (random.Next(6) == 0) f.Document.Body.Add(Table(f, random));
            else f.Document.Body.Add(Paragraph(f, random, random.Next(0, 80)));
        }
        foreach (var block in BodyParagraphs(f).Where(_ => random.Next(5) == 0)) AddNote(f, random, block);
        foreach (var block in BodyParagraphs(f).Where(_ => random.Next(7) == 0)) AddObject(f, random, block);
        var previous = f.Layout();
        for (var step = 0; step < 25; step++)
        {
            var body = f.Document.Body;
            var paragraphs = BodyParagraphs(f);
            switch (random.Next(7))
            {
                case 0 when paragraphs.Count > 0:
                    var edited = paragraphs[random.Next(paragraphs.Count)];
                    var text = Text(random, random.Next(0, 120));
                    edited.State = new ParagraphState(edited.Content.WithRuns(text.Length == 0 ? [] : [new TextRun(text)]),
                        [.. edited.Marks.Select(mark => mark.At(Math.Min(mark.Offset, text.Length)))]);
                    break;
                case 1:
                    body.Insert(random.Next(body.Count + 1), Table(f, random));
                    break;
                case 2 when body.Blocks.OfType<TableBlock>().FirstOrDefault() is { } table:
                    var cell = table.Rows[random.Next(table.Rows.Count)].Cells[0];
                    cell.Content.Replace([Paragraph(f, random, random.Next(0, 60))]);
                    break;
                case 3 when paragraphs.Count > 0:
                    AddNote(f, random, paragraphs[random.Next(paragraphs.Count)]);
                    break;
                case 4 when f.Document.Notes.All.Count > 0:
                    var note = f.Document.Notes.All[random.Next(f.Document.Notes.All.Count)];
                    note.Content.Replace([Paragraph(f, random, random.Next(1, 50))]);
                    break;
                case 5 when paragraphs.Count > 0:
                    AddObject(f, random, paragraphs[random.Next(paragraphs.Count)]);
                    break;
                default:
                    if (body.Count > 1) body.RemoveAt(random.Next(body.Count));
                    break;
            }
            var incremental = f.Engine.Relayout(f.Document, previous);
            f.Engine.Invalidate();
            var full = f.Engine.Layout(f.Document);
            Assert.Equal(Signature(full), Signature(incremental));
            previous = incremental;
        }
    }
}
