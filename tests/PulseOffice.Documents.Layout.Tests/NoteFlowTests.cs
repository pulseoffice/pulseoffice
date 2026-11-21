using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class NoteFlowTests
{
    private PageStyle Style(LayoutFixture f) => f.Document.Styles.Page.Find(LayoutFixture.Page)!;

    private string Citation(LineBox line) => string.Concat(line.Line.Runs.Where(run => run.Kind is GlyphRunKind.Citation or GlyphRunKind.Label).Select(run => run.Text)).Trim();

    private void Note(LayoutFixture f, ParagraphBlock block, string id)
    {
        f.Document.Notes.Add(new Note(id, NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np" + id, "nota")])));
        block.Marks = [new NoteMark(block.Length, id)];
    }

    [Fact]
    public void The_citation_in_the_footnote_area_is_superscript()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        Note(f, f.Add("riga"), "n1");

        var line = f.Layout().Pages[0].Footnotes!.Lines.Single();
        var label = line.Line.Runs.Single(run => run.Kind == GlyphRunKind.Label);
        var text = line.Line.Runs.First(run => run.Kind == GlyphRunKind.Text);

        Assert.Equal("1", label.Text);
        Assert.True(label.BaselineShift > 0);
        Assert.True(label.Font.Size < text.Font.Size);
    }

    [Fact]
    public void ConLeColonneDiPaginaLaNotaStaSottoLaSuaColonna()
    {
        var f = new LayoutFixture(width: 210, height: 120);
        Style(f).Columns = new SectionColumns(2, Twip.FromPoints(10));
        var blocks = Enumerable.Range(0, 15).Select(_ => f.Add("riga")).ToList();
        Note(f, blocks[12], "n1");

        var layout = f.Layout();
        var page = layout.Pages[0];
        var note = page.Footnotes!.Lines.Single();

        Assert.Equal(110, note.Left, 3);
        Assert.Equal(10, page.Lines.Count(line => line.Left < 100));
        Assert.True(page.Footnotes.Bounds.Width <= 100 + 0.001);
    }

    [Fact]
    public void UnaNotaInUnaCellaSpostaLaRiga()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        for (var i = 0; i < 3; i++) f.Add("riga");
        var cell = new ParagraphBlock("cell", "cella");
        var table = new TableBlock("t", [new TableRow([new TableCell(new BlockContainer([cell]))])], [Twip.FromPoints(100)]);
        f.Document.Body.Add(table);
        Note(f, cell, "n1");

        var layout = f.Layout();

        Assert.Empty(layout.Pages[0].Tables);
        Assert.Null(layout.Pages[0].Footnotes);
        Assert.Single(layout.Pages[1].Tables);
        Assert.Single(layout.Pages[1].Footnotes!.Lines);
    }

    [Fact]
    public void LaNumerazioneDelleNoteRipartePerPagina()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { Restart = NoteNumbering.Page };
        var blocks = Enumerable.Range(0, 6).Select(_ => f.Add("riga")).ToList();
        Note(f, blocks[0], "a");
        blocks[1].Marks = [];
        f.Document.Notes.Add(new Note("b", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("npb", "nota")])));
        blocks[1].Marks = [new NoteMark(4, "b")];
        Note(f, blocks[5], "c");

        var layout = f.Layout();
        var citations = layout.Pages.SelectMany(page => page.Lines.Where(line => line.Line.Notes.Any()).Select(line => (page.Index, Citation(line)))).ToList();

        Assert.Equal(["1", "2", "1"], citations.Select(item => item.Item2));
        Assert.Equal(0, citations[1].Index);
        Assert.True(citations[2].Index > 0);
    }

    [Fact]
    public void LaNumerazioneDelleNoteRipartePerCapitolo()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { Restart = NoteNumbering.Chapter };
        f.Add("Primo", p => p with { OutlineLevel = 1 });
        var a = f.Add("riga");
        var b = f.Add("riga");
        f.Add("Secondo", p => p with { OutlineLevel = 1 });
        var c = f.Add("riga");
        Note(f, a, "a");
        Note(f, b, "b");
        Note(f, c, "c");

        var layout = f.Layout();

        Assert.Equal(["1", "2", "1"], layout.Pages[0].Lines.Where(line => line.Line.Notes.Any()).Select(Citation));
    }

    [Fact]
    public void Note_numbering_restarts_at_each_section()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { Restart = NoteNumbering.Section };
        var a = f.Add("riga");
        var b = f.Add("riga", p => p with { OutlineLevel = 1 });
        var c = f.Add("riga", p => p with { PageStyleName = LayoutFixture.Page });
        Note(f, a, "a");
        Note(f, b, "b");
        Note(f, c, "c");

        var citations = f.Layout().Pages.SelectMany(page => page.Lines.Where(line => line.Line.Notes.Any()).Select(Citation));

        Assert.Equal(["1", "2", "1"], citations);
    }

    [Fact]
    public void Note_numbering_restarts_where_a_section_block_starts_and_ends()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { Restart = NoteNumbering.Section };
        var a = f.Add("riga");
        var inside = new ParagraphBlock("s1", "dentro");
        f.Document.Body.Add(new SectionBlock("sec", "S", new BlockContainer([inside])));
        var after = f.Add("dopo");
        Note(f, a, "a");
        Note(f, inside, "b");
        Note(f, after, "c");

        var citations = f.Layout().Pages.SelectMany(page => page.Lines.Where(line => line.Line.Notes.Any()).Select(Citation));

        Assert.Equal(["1", "1", "1"], citations);
    }
}
