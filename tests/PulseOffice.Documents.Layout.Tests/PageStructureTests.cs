using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class PageStructureTests
{
    private string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(_ => "aaaaaaaaa"));

    private ParagraphBlock Paragraph(LayoutFixture f, string text, params TextRun[] runs) =>
        new(f.Document.NextId("h"), runs.Length == 0 ? new TextParagraph(text) { Runs = [new TextRun(text)] } : new TextParagraph(string.Empty).WithRuns(runs));

    private string Text(LineBox line) => string.Concat(line.Line.Runs.Where(run => run.Kind is GlyphRunKind.Text or GlyphRunKind.Citation or GlyphRunKind.Label).Select(run => run.Text)).TrimEnd();

    private PageStyle Style(LayoutFixture f) => f.Document.Styles.Page.Find(LayoutFixture.Page)!;

    [Fact]
    public void IntestazioneEPiedeRiduconoIlCorpoEMostranoINumeriDiPagina()
    {
        var f = new LayoutFixture(width: 200, height: 120);
        var style = Style(f);
        style.Header.Enabled = true;
        style.Header.Spacing = Twip.FromPoints(6);
        style.Header.Default.Add(Paragraph(f, string.Empty, new TextRun("p. "), new TextRun("9") { Field = TextFieldKind.PageNumber }, new TextRun("/"), new TextRun("9") { Field = TextFieldKind.PageCount }));
        style.Footer.Enabled = true;
        style.Footer.Spacing = Twip.FromPoints(6);
        style.Footer.Default.Add(Paragraph(f, "piede"));
        for (var i = 0; i < 10; i++) f.Add("riga");

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(18, layout.Pages[0].Lines[0].Top, 3);
        Assert.Equal(7, layout.Pages[0].Lines.Count);
        Assert.Equal(["p. 1/2", "p. 2/2"], layout.Pages.Select(page => Text(page.Header!.Lines.Single())));
        Assert.All(layout.Pages, page => Assert.Equal(108, page.Footer!.Lines.Single().Top, 3));
    }

    [Fact]
    public void PrimaPaginaEPaginePariHannoLaLoroIntestazione()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        var style = Style(f);
        style.Header.Enabled = true;
        style.Header.Spacing = Twip.Zero;
        style.FirstPageDifferent = true;
        style.EvenPagesDifferent = true;
        style.Header.Default.Add(Paragraph(f, "dispari"));
        style.Header.First = new BlockContainer([Paragraph(f, "prima"), Paragraph(f, "titolo")]);
        style.Header.Even = new BlockContainer([Paragraph(f, "pari")]);
        for (var i = 0; i < 12; i++) f.Add("riga");

        var layout = f.Layout();

        Assert.Equal(["prima", "pari", "dispari", "pari"], layout.Pages.Take(4).Select(page => Text(page.Header!.Lines.First())));
        Assert.Equal(24, layout.Pages[0].Lines[0].Top, 3);
        Assert.Equal(12, layout.Pages[1].Lines[0].Top, 3);
    }

    [Fact]
    public void LeColonneDiPaginaFannoScorrereIlTesto()
    {
        var f = new LayoutFixture(width: 130, height: 36);
        Style(f).Columns = new SectionColumns(2, Twip.FromPoints(10));
        f.Add(Words(8), p => p with { Widows = 0, Orphans = 0 });

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal([0, 0, 0, 70, 70, 70], layout.Pages[0].Lines.Select(line => line.Left));
        Assert.Equal(0, layout.Pages[0].Lines[3].Top, 3);
        Assert.Equal(2, layout.Pages[0].Columns.Count);
    }

    [Fact]
    public void LInterruzioneDiColonnaPassaAllaColonnaSeguente()
    {
        var f = new LayoutFixture(width: 130, height: 400);
        Style(f).Columns = new SectionColumns(2, Twip.FromPoints(10));
        f.Add("uno");
        var second = f.Add("due", p => p with { ColumnBreakBefore = true });

        var layout = f.Layout();

        Assert.Single(layout.Pages);
        var line = layout.Pages[0].Lines.Single(l => l.ParagraphId == second.Id);
        Assert.Equal(70, line.Left);
        Assert.Equal(0, line.Top, 3);
    }

    [Fact]
    public void UnaSezioneBilanciaLeColonne()
    {
        var f = new LayoutFixture(width: 130, height: 400);
        f.Add("titolo");
        var content = new BlockContainer();
        content.Add(new ParagraphBlock(f.Document.NextId("s"), new TextParagraph(Words(6)) { Runs = [new TextRun(Words(6))], Widows = 0, Orphans = 0 }));
        f.Document.Body.Add(new SectionBlock(f.Document.NextId("sec"), "Colonne", content) { Columns = new SectionColumns(2, Twip.FromPoints(10)) });
        var after = f.Add("dopo");

        var layout = f.Layout();
        var lines = layout.Pages[0].Lines;

        Assert.Equal(3, lines.Count(line => line.Left == 0 && line.ParagraphId != after.Id) - 1);
        Assert.Equal(3, lines.Count(line => line.Left == 70));
        Assert.Equal(48, lines.Single(line => line.ParagraphId == after.Id).Top, 3);
    }

    [Fact]
    public void LaNotaStaInFondoAllaPaginaDellaSuaRiga()
    {
        var f = new LayoutFixture(width: 200, height: 120);
        var note = new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", "nota")]));
        f.Document.Notes.Add(note);
        var block = f.Add("testo");
        block.Marks = [new NoteMark(5, "n1")];

        var layout = f.Layout();
        var page = layout.Pages[0];

        Assert.NotNull(page.Footnotes);
        var noteLine = page.Footnotes!.Lines.Single();
        Assert.Equal("1nota", Text(noteLine));
        Assert.Equal(108, noteLine.Top, 3);
        Assert.Contains(page.Footnotes.Content, box => box is DecorationBox { Kind: DecorationKind.FootnoteSeparator });
        Assert.Equal("1", page.Lines[0].Line.Runs.Single(run => run.Kind == GlyphRunKind.Citation).Text);
        Assert.Equal(5, page.Lines[0].Line.End);
    }

    [Fact]
    public void SeRigaENotaNonCiStannoLaRigaPassaAllaPaginaDopo()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", "nota")])));
        for (var i = 0; i < 4; i++) f.Add("riga", p => p with { Widows = 0, Orphans = 0 });
        var anchored = f.Add("ancora");
        anchored.Marks = [new NoteMark(6, "n1")];

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(anchored.Id, layout.Pages[1].Lines[0].ParagraphId);
        Assert.Null(layout.Pages[0].Footnotes);
        Assert.NotNull(layout.Pages[1].Footnotes);
    }

    [Fact]
    public void UnaNotaLungaContinuaSullaPaginaSuccessiva()
    {
        var f = new LayoutFixture(width: 60, height: 60);
        var body = new TextParagraph(Words(6)) { Runs = [new TextRun(Words(6))], Widows = 0, Orphans = 0 };
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", body)])));
        var anchored = f.Add("a");
        anchored.Marks = [new NoteMark(1, "n1")];

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        var first = layout.Pages[0].Footnotes!.Lines.Count();
        var second = layout.Pages[1].Footnotes!.Lines.Count();
        Assert.Equal(6, first + second);
        Assert.True(first > 0 && second > 0);
        Assert.Equal(new PendingNote("n1", first), layout.Pages[1].Start.Pending.Items.Single());
    }

    [Fact]
    public void LeNoteDiChiusuraVannoInFondoAlDocumento()
    {
        var f = new LayoutFixture(width: 200, height: 120);
        f.Document.Notes.Add(new Note("e1", NoteKind.Endnote, new BlockContainer([new ParagraphBlock("ep", "chiusura")])));
        var block = f.Add("testo");
        block.Marks = [new NoteMark(5, "e1")];

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        var line = layout.Pages[1].Lines.Single();
        Assert.Equal(StoryKind.Endnote, line.Story);
        Assert.Equal("ichiusura", Text(line));
    }

    [Fact]
    public void LeNoteDiChiusuraContinueSeguonoIlTestoSullaStessaPagina()
    {
        var f = new LayoutFixture(width: 200, height: 120);
        f.Document.Notes.ContinuousEndnotes = true;
        f.Document.Notes.Add(new Note("e1", NoteKind.Endnote, new BlockContainer([new ParagraphBlock("ep", "chiusura")])));
        var block = f.Add("testo");
        block.Marks = [new NoteMark(5, "e1")];

        var layout = f.Layout();

        var page = Assert.Single(layout.Pages);
        Assert.Equal([StoryKind.Body, StoryKind.Endnote], page.Lines.Select(line => line.Story));
    }

    private TableBlock Table(LayoutFixture f, int rows, params string[][] cells)
    {
        var tableRows = new List<TableRow>();
        for (var r = 0; r < rows; r++)
        {
            var values = r < cells.Length ? cells[r] : [$"r{r}a", $"r{r}b"];
            tableRows.Add(new TableRow(values.Select(value => new TableCell(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(value) { Runs = [new TextRun(value)], Widows = 0, Orphans = 0 })])))));
        }
        return new TableBlock(f.Document.NextId("t"), tableRows, [Twip.FromPoints(60), Twip.FromPoints(60)]);
    }

    [Fact]
    public void LaTabellaUsaLeLarghezzeELaRigaPiuAlta()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 2, BorderWidth = 0 } });
        var table = Table(f, 2, ["uno", Words(3)], ["due", "tre"]);
        table.StyleName = "T";
        f.Document.Body.Add(table);

        var layout = f.Layout();
        var box = layout.Pages[0].Tables.Single();

        Assert.Equal(120, box.Bounds.Width, 3);
        Assert.Equal(3 * 12 + 4, box.Cells[0].Bounds.Height, 3);
        Assert.Equal(60, box.Cells[1].Bounds.X, 3);
        Assert.Equal(40, box.Cells[2].Bounds.Y, 3);
        Assert.Equal(62, box.Cells[1].Lines.First().Left, 3);
    }

    [Fact]
    public void LaTabellaSiSpezzaERipeteLIntestazione()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        var table = Table(f, 8);
        table.HeaderRows = 1;
        f.Document.Body.Add(table);

        var layout = f.Layout();

        Assert.True(layout.Pages.Count >= 2);
        var second = layout.Pages[1].Tables.SelectMany(t => t.Cells).First();
        Assert.Equal(0, second.Row);
        Assert.Equal("r0a", Text(second.Lines.First()));
    }

    [Fact]
    public void UnaRigaAltaSiSpezzaFraLePagine()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        var table = Table(f, 1, [Words(8), "x"]);
        f.Document.Body.Add(table);

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        var lines = layout.Pages.SelectMany(page => page.Tables.SelectMany(t => t.Lines)).Where(line => line.Line.Runs.Any(run => run.Text.StartsWith('a'))).Count();
        Assert.Equal(8, lines);
        Assert.NotEqual(0, layout.Pages[1].Start.Cells.Count);
    }

    [Fact]
    public void LeCelleUniteOccupanoPiuColonne()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var table = Table(f, 2);
        table.Rows[0].Cells[0].ColumnSpan = 2;
        table.Rows[0].Cells[1].Covered = true;
        f.Document.Body.Add(table);

        var layout = f.Layout();
        var cells = layout.Pages[0].Tables.Single().Cells;

        Assert.Equal(3, cells.Count);
        Assert.Equal(120, cells[0].Bounds.Width, 3);
    }

    [Fact]
    public void BordoESfondoDelParagrafoRientranoIlTesto()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var block = f.Add("cornice", p => p with { Background = 0xFFEEEEEE, Borders = BoxBorders.Uniform(new BorderLine(1, 0xFF000000), 3) });

        var layout = f.Layout();
        var page = layout.Pages[0];

        Assert.Equal(4, page.Lines[0].Top, 3);
        Assert.Equal(4, page.Lines[0].Line.Runs[0].X, 3);
        var background = page.Decorations.Single(box => box.Kind == DecorationKind.ParagraphBackground);
        Assert.Equal(new LayoutRect(0, 0, 200, 20), background.Bounds);
        Assert.Contains(page.Decorations, box => box.Kind == DecorationKind.ParagraphBorder && box.Owner == block.Id);
    }

    [Fact]
    public void UnOggettoComeCarattereOccupaSpazioNellaRiga()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var block = f.Add("abcd");
        block.Marks = [new ObjectMark(2, "img")];
        f.Document.Objects.Add(new AnchoredObject("img", new AsCharacterAnchor(block.Id), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(30), Twip.FromPoints(20))));

        var layout = f.Layout();
        var line = layout.Pages[0].Lines.Single();

        Assert.Equal(20, line.Line.Ascent, 3);
        Assert.Equal(12 + 30, line.Line.Runs.Single(run => run.Text == "cd").X, 3);
        var box = layout.Pages[0].Objects.Single();
        Assert.Equal(new LayoutRect(12, 0, 30, 20), box.Bounds);
        Assert.Equal(new DocumentPosition(block.Id, 2), f.Queries.HitTest(layout, 0, 13, 22));
    }

    [Fact]
    public void IlTestoScorreAttornoAllOggettoAncoratoAlParagrafo()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var block = f.Add(Words(12));
        f.Document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(40), Twip.FromPoints(30)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(80), Twip.Zero),
            Wrap = WrapMode.Parallel,
        });

        var layout = f.Layout();
        var lines = layout.Pages[0].Lines;

        Assert.Equal(new LayoutRect(80, 0, 40, 30), layout.Pages[0].Objects.Single().Bounds);
        Assert.Contains(lines, line => line.Top == 0 && line.Left == 120);
        Assert.All(lines.Where(line => line.Top < 30), line => Assert.True(line.Left >= 120 || line.Left + line.Line.Runs.Where(run => run.Kind == GlyphRunKind.Text).Sum(run => run.Width) <= 80 + 0.001));
        Assert.Contains(lines, line => line.Top >= 30 && line.Left == 0);
    }

    [Fact]
    public void UnOggettoDietroNonSpostaIlTesto()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var block = f.Add(Words(6));
        f.Document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(40), Twip.FromPoints(30)))
        {
            Wrap = WrapMode.Through,
            InBackground = true,
        });

        var layout = f.Layout();

        Assert.All(layout.Pages[0].Lines, line => Assert.Equal(0, line.Left));
        Assert.Equal(ObjectLayer.Behind, layout.Pages[0].Objects.Single().Layer);
    }

    [Fact]
    public void CursoreEPuntoFunzionanoInIntestazioneCellaENota()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var style = Style(f);
        style.Header.Enabled = true;
        var header = Paragraph(f, "testata");
        style.Header.Default.Add(header);
        var note = new ParagraphBlock("np", "nota");
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([note])));
        var anchored = f.Add("x");
        anchored.Marks = [new NoteMark(1, "n1")];
        var table = Table(f, 1, ["cella", "due"]);
        f.Document.Body.Add(table);
        var cell = (ParagraphBlock)table.Rows[0].Cells[0].Content[0];
        var layout = f.Layout();

        foreach (var (id, offset) in new[] { (header.Id, 3), (cell.Id, 2), (note.Id, 2) })
        {
            var caret = f.Queries.Caret(layout, new DocumentPosition(id, offset))!.Value;
            Assert.Equal(new DocumentPosition(id, offset), f.Queries.HitTest(layout, caret.Page, caret.X + 0.1, caret.Y + caret.Height / 2));
        }
    }

    private string Signature(DocumentLayout layout) => string.Join('|', layout.Pages.Select(page =>
        $"{page.Index}:{page.Start}:" + string.Join(',', page.AllLines.Select(line => $"{line.ParagraphId}/{line.Line.Start}-{line.Line.End}@{line.Left:F2},{line.Top:F3}"))
        + ":" + string.Join(',', page.Objects.Select(item => item.Bounds)) + ":" + string.Join(',', page.Decorations.Select(item => item.Bounds))));

    [Fact]
    public void LaReimpaginazioneIncrementaleConStruttureEUgualeAQuellaCompleta()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var style = Style(f);
        style.Header.Enabled = true;
        style.Header.Default.Add(Paragraph(f, string.Empty, new TextRun("1") { Field = TextFieldKind.PageNumber }));
        var random = new Random(3);
        var notes = 0;
        for (var i = 0; i < 120; i++)
        {
            if (i % 17 == 5)
            {
                f.Document.Body.Add(Table(f, 6));
                continue;
            }
            if (i % 23 == 7)
            {
                var content = new BlockContainer([new ParagraphBlock(f.Document.NextId("s"), new TextParagraph(Words(20)) { Runs = [new TextRun(Words(20))] })]);
                f.Document.Body.Add(new SectionBlock(f.Document.NextId("sec"), "S", content) { Columns = new SectionColumns(2, Twip.FromPoints(10)) });
                continue;
            }
            var block = f.Add(string.Join(' ', Enumerable.Range(0, random.Next(3, 40)).Select(_ => "parola")));
            if (i % 11 == 3)
            {
                var id = "n" + notes++;
                f.Document.Notes.Add(new Note(id, NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np" + id, Words(4))])));
                block.Marks = [new NoteMark(2, id)];
            }
        }
        var previous = f.Layout();
        var edited = (ParagraphBlock)f.Document.Body.Blocks.OfType<ParagraphBlock>().ElementAt(40);
        edited.Content = edited.Content.WithRuns([new TextRun(Words(30))]);
        var incremental = f.Engine.Relayout(f.Document, previous);
        f.Engine.Invalidate();
        var full = f.Engine.Layout(f.Document);

        Assert.Equal(Signature(full), Signature(incremental));
    }
}
