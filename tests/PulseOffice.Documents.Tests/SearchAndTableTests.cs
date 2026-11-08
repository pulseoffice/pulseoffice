using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class SearchAndTableTests
{
    [Fact]
    public void LaRicercaTrovaLeOccorrenzeEDistingueLeParoleIntere()
    {
        var paragraphs = new[] { new ParagraphBlock("a", "Casa casale casa"), new ParagraphBlock("b", "la CASA") };
        var search = new DocumentSearch();

        Assert.Equal(4, search.FindAll(paragraphs, new SearchQuery("casa")).Count);
        Assert.Equal(3, search.FindAll(paragraphs, new SearchQuery("casa", WholeWords: true)).Count);
        Assert.Single(search.FindAll(paragraphs, new SearchQuery("CASA", MatchCase: true)));
        var next = search.FindNext(paragraphs, new SearchQuery("casa", WholeWords: true), new DocumentPosition("a", 1));
        Assert.Equal(DocumentRange.In("a", 12, 16), next);
        var wrapped = search.FindNext(paragraphs, new SearchQuery("casa", WholeWords: true), new DocumentPosition("b", 7));
        Assert.Equal(DocumentRange.In("a", 0, 4), wrapped);
    }

    [Fact]
    public void IlConteggioContaParoleCaratteriEParagrafi()
    {
        var statistics = new DocumentStatisticsCounter().Count(["Uno due  tre", "", "quattro"]);

        Assert.Equal(4, statistics.Words);
        Assert.Equal(19, statistics.Characters);
        Assert.Equal(16, statistics.CharactersWithoutSpaces);
        Assert.Equal(2, statistics.Paragraphs);
    }

    [Fact]
    public void RigheEColonneSiInserisconoESiAnnullano()
    {
        var fixture = new Fixture();
        fixture.Editor.InsertTable(fixture.Document, fixture.History, new DocumentPosition(fixture.First.Id, 0), 2, 2);
        var table = fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Rows[1].Cells[1].Content.Blocks.OfType<ParagraphBlock>().Single();
        var editing = new TableEditing(fixture.Stories, fixture.Deletion);

        var at = editing.Locate(fixture.Document, cell.Id)!;
        Assert.Equal((1, 1), (at.Row, at.Column));
        editing.InsertRow(fixture.Document, fixture.History, at, false);
        editing.InsertColumn(fixture.Document, fixture.History, editing.Locate(fixture.Document, cell.Id)!, true);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(3, table.Columns.Count);
        Assert.Same(cell, table.Rows[2].Cells[1].Content.Blocks[0]);

        fixture.History.Undo();
        fixture.History.Undo();
        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(2, row.Cells.Count));
        Assert.Null(editing.Locate(fixture.Document, fixture.Document.Body.Blocks.OfType<ParagraphBlock>().First().Id));
    }

    [Fact]
    public void EliminareLUltimaRigaEliminaLaTabella()
    {
        var fixture = new Fixture();
        fixture.Editor.InsertTable(fixture.Document, fixture.History, new DocumentPosition(fixture.First.Id, 0), 1, 2);
        var table = fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Rows[0].Cells[0].Content.Blocks.OfType<ParagraphBlock>().Single();
        var editing = new TableEditing(fixture.Stories, fixture.Deletion);

        editing.DeleteRow(fixture.Document, fixture.History, editing.Locate(fixture.Document, cell.Id)!);

        Assert.Empty(fixture.Document.Body.Blocks.OfType<TableBlock>());
        fixture.History.Undo();
        Assert.Single(fixture.Document.Body.Blocks.OfType<TableBlock>());
    }

    [Fact]
    public void ElenchiEFormattazioneDirettaSiTolgono()
    {
        var fixture = new Fixture();
        var range = DocumentRange.In(fixture.First.Id, 0, 0);
        fixture.Editor.InsertText(fixture.Document, fixture.History, range.Start, "Testo");
        var all = DocumentRange.In(fixture.First.Id, 0, 5);

        fixture.Editor.SetList(fixture.Document, fixture.History, all, DefaultStyleNames.List1, 2);
        Assert.Equal(DefaultStyleNames.List1, fixture.First.Content.ListStyleName);
        Assert.Equal(2, fixture.First.Content.BulletLevel);

        fixture.Editor.SetCharacter(fixture.Document, fixture.History, all, new CharacterProperties { Bold = true });
        fixture.Editor.MapRuns(fixture.Document, fixture.History, all, "link", run => run with { Hyperlink = "https://example.org" });
        fixture.Editor.ClearDirectFormatting(fixture.Document, fixture.History, all);
        Assert.All(fixture.First.Content.Runs, run =>
        {
            Assert.False(run.Bold);
            Assert.Equal("https://example.org", run.Hyperlink);
        });
    }
}

public sealed class MergedTableTests
{
    private static (Fixture Fixture, TableBlock Table, TableEditing Editing) Grid(int rows, int columns)
    {
        var fixture = new Fixture();
        fixture.Editor.InsertTable(fixture.Document, fixture.History, new DocumentPosition(fixture.First.Id, 0), rows, columns);
        return (fixture, fixture.Document.Body.Blocks.OfType<TableBlock>().Single(), new TableEditing(fixture.Stories, fixture.Deletion));
    }

    private static string Id(TableBlock table, int row, int column) =>
        table.Rows[row].Cells[column].Content.Blocks.OfType<ParagraphBlock>().First().Id;

    [Fact]
    public void EliminareUnaColonnaDentroUnUnioneLaRestringe()
    {
        var (fixture, table, editing) = Grid(2, 3);
        table.Rows[0].Cells[0].ColumnSpan = 3;
        table.Rows[0].Cells[1].Covered = true;
        table.Rows[0].Cells[2].Covered = true;

        editing.DeleteColumn(fixture.Document, fixture.History, editing.Locate(fixture.Document, Id(table, 1, 1))!);

        Assert.Equal(2, table.Rows[0].Cells[0].ColumnSpan);
        Assert.All(table.Rows, row => Assert.Equal(2, row.Cells.Count));
        fixture.History.Undo();
        Assert.Equal(3, table.Rows[0].Cells[0].ColumnSpan);
        Assert.All(table.Rows, row => Assert.Equal(3, row.Cells.Count));
    }

    [Fact]
    public void EliminareLaColonnaDOrigineCedeIlContenutoAllaSuccessiva()
    {
        var (fixture, table, editing) = Grid(1, 3);
        var content = table.Rows[0].Cells[0].Content;
        table.Rows[0].Cells[0].ColumnSpan = 2;
        table.Rows[0].Cells[1].Covered = true;

        editing.DeleteColumn(fixture.Document, fixture.History, editing.Locate(fixture.Document, Id(table, 0, 0))!);

        Assert.Same(content, table.Rows[0].Cells[0].Content);
        Assert.Equal(1, table.Rows[0].Cells[0].ColumnSpan);
        Assert.False(table.Rows[0].Cells[0].Covered);
    }

    [Fact]
    public void EliminareUnaRigaRestringeLUnioneVerticaleOLaCede()
    {
        var (fixture, table, editing) = Grid(3, 2);
        var content = table.Rows[0].Cells[0].Content;
        table.Rows[0].Cells[0].RowSpan = 3;
        table.Rows[1].Cells[0].Covered = true;
        table.Rows[2].Cells[0].Covered = true;

        editing.DeleteRow(fixture.Document, fixture.History, editing.Locate(fixture.Document, Id(table, 1, 1))!);
        Assert.Equal(2, table.Rows[0].Cells[0].RowSpan);

        editing.DeleteRow(fixture.Document, fixture.History, editing.Locate(fixture.Document, Id(table, 0, 1))!);
        Assert.Single(table.Rows);
        Assert.Same(content, table.Rows[0].Cells[0].Content);
        Assert.Equal(1, table.Rows[0].Cells[0].RowSpan);
        Assert.False(table.Rows[0].Cells[0].Covered);

        fixture.History.Undo();
        fixture.History.Undo();
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(3, table.Rows[0].Cells[0].RowSpan);
    }

    [Fact]
    public void InserireUnaRigaDentroUnUnioneLAllunga()
    {
        var (fixture, table, editing) = Grid(2, 2);
        table.Rows[0].Cells[0].RowSpan = 2;
        table.Rows[1].Cells[0].Covered = true;

        editing.InsertRow(fixture.Document, fixture.History, editing.Locate(fixture.Document, Id(table, 0, 1))!, true);

        Assert.Equal(3, table.Rows[0].Cells[0].RowSpan);
        Assert.True(table.Rows[1].Cells[0].Covered);
        Assert.False(table.Rows[1].Cells[1].Covered);
    }

    [Fact]
    public void LeMaiuscoleDiFraseAttraversanoLePorzioniConservandoLaFormattazione()
    {
        var fixture = new Fixture();
        fixture.Editor.InsertText(fixture.Document, fixture.History, new DocumentPosition(fixture.First.Id, 0), "CIAO mondo");
        fixture.Editor.SetCharacter(fixture.Document, fixture.History, DocumentRange.In(fixture.First.Id, 2, 7), new CharacterProperties { Bold = true });

        fixture.Editor.TransformText(fixture.Document, fixture.History, DocumentRange.In(fixture.First.Id, 0, 10), "case", new PulseOffice.Text.SentenceCase().Apply);

        Assert.Equal("Ciao mondo", fixture.First.Text);
        Assert.Equal("ao mo", string.Concat(fixture.First.Content.Runs.Where(run => run.Bold).Select(run => run.Text)));
    }
}
