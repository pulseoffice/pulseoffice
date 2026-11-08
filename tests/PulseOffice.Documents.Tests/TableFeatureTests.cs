using System.Globalization;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TableFeatureTests
{
    private readonly Fixture _fixture = new();
    private readonly TableEditing _tables;
    private readonly TableGrid _grid;
    private readonly TableSelections _selections;
    private readonly TableCellEditing _cells;
    private readonly TableGeometryEditing _geometry = new(new ColumnWidths());
    private readonly TableFormatting _formatting;
    private readonly TableSorting _sorting;
    private readonly TableFormulaEditing _formulas;
    private readonly TableConversion _conversion;
    private readonly CultureInfo _italian = CultureInfo.GetCultureInfo("it-IT");

    public TableFeatureTests()
    {
        _tables = new TableEditing(_fixture.Stories, _fixture.Deletion);
        _grid = new TableGrid(_fixture.Tree);
        _selections = new TableSelections(_tables, _grid, new TableSpans());
        _cells = new TableCellEditing(_grid, new TableCellFactory());
        _formatting = new TableFormatting(_grid, new StyleEditing(new StyleReferences(_fixture.Stories, _fixture.Tree)), _fixture.Tree);
        _sorting = new TableSorting(_grid, new TableSorter());
        _formulas = new TableFormulaEditing(new TableFormulaParser(), _grid, _fixture.Tree, [new CanonicalFormulaSyntax(), new WriterFormulaSyntax()]);
        _conversion = new TableConversion(_fixture.Stories, _fixture.Text, new TableCellFactory(), _fixture.Deletion, new TableSpanCutting());
    }

    private TableBlock Table(int rows, int columns, params string[] texts)
    {
        _fixture.Editor.InsertTable(_fixture.Document, _fixture.History, new DocumentPosition(_fixture.First.Id, 0), rows, columns);
        var table = _fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        for (var index = 0; index < texts.Length; index++)
        {
            var paragraph = Paragraph(table, index / columns, index % columns);
            _fixture.Editor.InsertText(_fixture.Document, _fixture.History, new DocumentPosition(paragraph.Id, 0), texts[index]);
        }
        return table;
    }

    private ParagraphBlock Paragraph(TableBlock table, int row, int column) => table.Cell(row, column).Content.Blocks.OfType<ParagraphBlock>().First();

    private string Text(TableBlock table, int row, int column) => _grid.PlainText(table.Cell(row, column));

    private TableSelection Select(TableBlock table, int r1, int c1, int r2, int c2) =>
        _selections.Select(_fixture.Document, new DocumentPosition(Paragraph(table, r1, c1).Id, 0), new DocumentPosition(Paragraph(table, r2, c2).Id, 0))!;

    [Fact]
    public void LaSelezioneFraDueCelleEUnRettangolo()
    {
        var table = Table(3, 3);

        var selection = Select(table, 2, 2, 0, 1);

        Assert.Equal(new TableCellRange(0, 1, 2, 2), selection.Range);
    }

    [Fact]
    public void UnireLeCelleRaccoglieIlTestoEDSiAnnullaESiRipete()
    {
        var table = Table(2, 2, "a", "b", "c", "d");

        _cells.Merge(_fixture.Document, _fixture.History, Select(table, 0, 0, 1, 1));

        Assert.Equal((2, 2), (table.Cell(0, 0).RowSpan, table.Cell(0, 0).ColumnSpan));
        Assert.True(table.Cell(1, 1).Covered);
        Assert.Equal("a\nb\nc\nd", Text(table, 0, 0));
        _fixture.History.Undo();
        Assert.Equal(1, table.Cell(0, 0).RowSpan);
        Assert.False(table.Cell(1, 1).Covered);
        Assert.Equal("d", Text(table, 1, 1));
        _fixture.History.Redo();
        Assert.Equal(2, table.Cell(0, 0).ColumnSpan);
    }

    [Fact]
    public void DividereUnaCellaUnitaLaRiportaAlleCelleDiPrima()
    {
        var table = Table(2, 2, "a", "b", "c", "d");
        _cells.Merge(_fixture.Document, _fixture.History, Select(table, 0, 0, 0, 1));

        _cells.Split(_fixture.Document, _fixture.History, Select(table, 0, 0, 0, 0), 2, false);

        Assert.False(table.Cell(0, 1).Covered);
        Assert.Equal(1, table.Cell(0, 0).ColumnSpan);
        Assert.Single(table.Cell(0, 1).Content.Blocks);
    }

    [Fact]
    public void DividereUnaCellaInColonneAllargaLeAltreRighe()
    {
        var table = Table(2, 2, "a", "b", "c", "d");
        var width = table.Columns[0].Value;

        _cells.Split(_fixture.Document, _fixture.History, Select(table, 0, 0, 0, 0), 3, false);

        Assert.Equal(4, table.ColumnCount);
        Assert.Equal(3, table.Cell(1, 0).ColumnSpan);
        Assert.True(table.Cell(1, 1).Covered);
        Assert.InRange(table.Columns.Take(3).Sum(column => column.Value), width - 2, width + 2);
        Assert.Equal("b", Text(table, 0, 3));
    }

    [Fact]
    public void DividereUnaCellaInRigheAllungaLeAltreColonne()
    {
        var table = Table(1, 2, "a", "b");

        _cells.Split(_fixture.Document, _fixture.History, Select(table, 0, 1, 0, 1), 2, true);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Cell(0, 0).RowSpan);
        Assert.True(table.Cell(1, 0).Covered);
        Assert.False(table.Cell(1, 1).Covered);
    }

    [Fact]
    public void LarghezzeDistribuiteEDTrascinate()
    {
        var table = Table(1, 3);
        _geometry.SetColumnWidths(_fixture.Document, _fixture.History, table, [100, 100, 200], "x");

        _geometry.DistributeColumns(_fixture.Document, _fixture.History, Select(table, 0, 1, 0, 2));
        Assert.Equal([100, 150, 150], table.Columns.Select(column => column.ToPoints()));

        _geometry.ResizeColumn(_fixture.Document, _fixture.History, table, 0, 130);
        Assert.Equal([130, 120, 150], table.Columns.Select(column => column.ToPoints()));
        _fixture.History.Undo();
        Assert.Equal([100, 150, 150], table.Columns.Select(column => column.ToPoints()));
    }

    [Fact]
    public void AltezzaDellaRigaEDInterruzioneSiAnnullano()
    {
        var table = Table(2, 1);

        _geometry.SetRowHeight(_fixture.Document, _fixture.History, Select(table, 0, 0, 1, 0), 30, false);
        _formatting.SetRowBreak(_fixture.Document, _fixture.History, Select(table, 0, 0, 0, 0), false);

        Assert.All(table.Rows, row => Assert.Equal(600, row.Height!.Value.Value));
        Assert.False(table.Rows[0].MinimumHeight);
        Assert.False(table.Rows[0].AllowBreakAcrossPages);
        _fixture.History.Undo();
        _fixture.History.Undo();
        Assert.Null(table.Rows[0].Height);
        Assert.True(table.Rows[0].AllowBreakAcrossPages);
    }

    [Fact]
    public void BordiEsterniEDInterniPerCella()
    {
        var table = Table(2, 2);
        var line = new BorderLine(1, 0xFF0000FF);

        _formatting.SetBorders(_fixture.Document, _fixture.History, Select(table, 0, 0, 1, 1), CellBorderPreset.Outer, line);

        Assert.Equal(line, table.Cell(0, 0).Borders!.Top);
        Assert.Equal(line, table.Cell(0, 0).Borders!.Left);
        Assert.Null(table.Cell(0, 0).Borders!.Bottom);
        Assert.Equal(line, table.Cell(1, 1).Borders!.Right);
        _formatting.SetBackground(_fixture.Document, _fixture.History, Select(table, 1, 0, 1, 1), 0xFFFFEEAA);
        Assert.Equal(0xFFFFEEAA, table.Cell(1, 1).Background);
        _fixture.History.Undo();
        _fixture.History.Undo();
        Assert.Null(table.Cell(0, 0).Borders);
    }

    [Fact]
    public void UnoStileDiTabellaPredefinitoSiCreaUnaVoltaSola()
    {
        var table = Table(3, 2);
        var look = new TableStylePresets().Find("boxListBlue")!;
        var appearance = new TableAppearance(_fixture.Resolver);

        using (_fixture.History.BeginTransaction("style"))
        {
            var name = _formatting.EnsurePreset(_fixture.Document, _fixture.History, look, look.Name);
            _formatting.ApplyStyle(_fixture.Document, _fixture.History, table, name);
        }
        _formatting.EnsurePreset(_fixture.Document, _fixture.History, look, "Box List Blue");

        Assert.Equal(1, table.HeaderRows);
        var style = appearance.Style(_fixture.Document.Styles, table);
        Assert.Equal(look.HeaderFill, appearance.Cell(table, style, 0, 0).Background);
        Assert.Equal(look.HeaderInk, appearance.Cell(table, style, 0, 0).Ink);
        Assert.Null(appearance.Cell(table, style, 1, 0).Background);
        Assert.Equal(look.BandFill, appearance.Cell(table, style, 2, 0).Background);
        Assert.Equal(DefaultStyleNames.TableHeading, Paragraph(table, 0, 0).StyleName);
        _fixture.History.Undo();
        Assert.False(_fixture.Document.Styles.Table.Contains("Box List Blue"));
        Assert.Equal(DefaultStyleNames.DefaultTableStyle, table.StyleName);
    }

    [Fact]
    public void OrdinareLaTabellaLasciaFermaLIntestazione()
    {
        var table = Table(4, 2, "Nome", "Valore", "pera", "10", "mela", "9", "uva", "1,5");
        table.HeaderRows = 1;
        var selection = Select(table, 1, 1, 1, 1);

        Assert.True(_sorting.CanSort(selection));
        _sorting.Sort(_fixture.Document, _fixture.History, selection, [new TableSortKey(1, Kind: TableSortKind.Numeric)], _italian);

        Assert.Equal(["Nome", "uva", "mela", "pera"], Enumerable.Range(0, 4).Select(r => Text(table, r, 0)));
        _fixture.History.Undo();
        Assert.Equal("pera", Text(table, 1, 0));
    }

    [Fact]
    public void LaFormulaScriveIlRisultatoEDSiRicalcola()
    {
        var table = Table(3, 1, "2", "3,5");
        var at = _tables.Locate(_fixture.Document, Paragraph(table, 2, 0).Id)!;

        _formulas.SetFormula(_fixture.Document, _fixture.History, at, "=sum <A1:A2>", _italian);

        Assert.Equal("SUM(A1:A2)", table.Cell(2, 0).Formula);
        Assert.Equal("5,5", Text(table, 2, 0));
        _fixture.Editor.InsertText(_fixture.Document, _fixture.History, new DocumentPosition(Paragraph(table, 0, 0).Id, 1), "0");
        Assert.True(_formulas.Recalculate(_fixture.Document, _fixture.History, table, _italian));
        Assert.Equal("23,5", Text(table, 2, 0));
        Assert.False(_formulas.IsValid("=SUM(A1"));
    }

    [Fact]
    public void IlTestoDiventaTabellaEDTornaTesto()
    {
        _fixture.Type("Nome\tEtà", "Anna\t30\textra", "Luca");
        var range = new DocumentRange(_fixture.At(0, 0), _fixture.At(2, 2));

        var result = _conversion.ToTable(_fixture.Document, _fixture.History, range, new TextTableOptions(TableTextSeparator.Tab, Heading: true), 450);

        var table = _fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        Assert.Equal((3, 3), (table.Rows.Count, table.ColumnCount));
        Assert.Equal(1, table.HeaderRows);
        Assert.Equal("extra", Text(table, 1, 2));
        Assert.Equal(Paragraph(table, 0, 0).Id, result.FirstParagraphId);
        Assert.Equal(DefaultStyleNames.TableHeading, Paragraph(table, 0, 1).StyleName);

        var at = _tables.Locate(_fixture.Document, Paragraph(table, 1, 0).Id)!;
        _conversion.ToText(_fixture.Document, _fixture.History, at, new TextTableOptions(TableTextSeparator.Semicolon));

        Assert.Empty(_fixture.Document.Body.Blocks.OfType<TableBlock>());
        Assert.Equal(["Nome;Età;", "Anna;30;extra", "Luca;;"], _fixture.Document.Body.Blocks.OfType<ParagraphBlock>().Take(3).Select(paragraph => paragraph.Text));
        _fixture.History.Undo();
        Assert.Single(_fixture.Document.Body.Blocks.OfType<TableBlock>());
        _fixture.History.Undo();
        Assert.Empty(_fixture.Document.Body.Blocks.OfType<TableBlock>());
        Assert.Equal("Anna\t30\textra", _fixture.Paragraph(1).Text);
    }

    [Fact]
    public void DividereEDUnireLeTabelle()
    {
        var table = Table(4, 2, "h1", "h2", "a", "b", "c", "d", "e", "f");
        table.HeaderRows = 1;
        var at = _tables.Locate(_fixture.Document, Paragraph(table, 2, 0).Id)!;

        _conversion.SplitTable(_fixture.Document, _fixture.History, at, copyHeading: true);

        var tables = _fixture.Document.Body.Blocks.OfType<TableBlock>().ToList();
        Assert.Equal(2, tables.Count);
        Assert.Equal(2, tables[0].Rows.Count);
        Assert.Equal(["h1", "c", "e"], Enumerable.Range(0, 3).Select(r => Text(tables[1], r, 0)));
        Assert.Equal(1, tables[1].HeaderRows);

        var first = _tables.Locate(_fixture.Document, Paragraph(tables[0], 0, 0).Id)!;
        Assert.True(_conversion.CanMergeWithNext(first));
        _conversion.MergeWithNext(_fixture.Document, _fixture.History, first);

        var merged = _fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(["h1", "a", "c", "e"], Enumerable.Range(0, 4).Select(r => Text(merged, r, 0)));
    }
}
