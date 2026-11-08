using System.Globalization;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Protection;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class CellFormatTests
{
    private readonly Fixture _f = new();
    private readonly CultureInfo _italian = CultureInfo.GetCultureInfo("it-IT");

    private TableBlock Table(params string[] texts)
    {
        _f.Editor.InsertTable(_f.Document, _f.History, new DocumentPosition(_f.First.Id, 0), 1, texts.Length);
        var table = _f.Document.Body.Blocks.OfType<TableBlock>().Single();
        for (var column = 0; column < texts.Length; column++)
        {
            var paragraph = (ParagraphBlock)table.Rows[0].Cells[column].Content.Blocks[0];
            _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(paragraph.Id, 0), texts[column]);
        }
        return table;
    }

    private string Text(TableBlock table, int column) => ((ParagraphBlock)table.Rows[0].Cells[column].Content.Blocks[0]).Text;

    [Theory]
    [InlineData(CellValueKind.Number, "1234,5", "1.234,50")]
    [InlineData(CellValueKind.Percent, "0,125", "12,50%")]
    [InlineData(CellValueKind.Currency, "12", "12,00 €")]
    public void IlFormatoRiscriveINumeriESiAnnulla(CellValueKind kind, string typed, string shown)
    {
        var table = Table(typed, "parola");
        var formatting = new CellNumberFormatting(_f.Editor, new CellValueText());

        var changed = formatting.Apply(_f.Document, _f.History, table, new TableCellRange(0, 0, 0, 1), new CellNumberFormat(kind), _italian);

        Assert.Equal(1, changed);
        Assert.Equal(shown, Text(table, 0));
        Assert.Equal("parola", Text(table, 1));
        Assert.Equal(kind, table.Rows[0].Cells[1].NumberFormat?.Kind);

        _f.History.Undo();

        Assert.Equal(typed, Text(table, 0));
        Assert.Null(table.Rows[0].Cells[0].NumberFormat);
    }

    [Fact]
    public void UnaCellaProtettaNonSiModificaLeAltreSi()
    {
        var table = Table("chiusa", "aperta");
        new CellNumberFormatting(_f.Editor, new CellValueText()).Protect(_f.Document, _f.History, table, new TableCellRange(0, 0, 0, 0), true);
        var guard = new EditGuard(new Indexes.GeneratedRegions(_f.Tree), _f.Trails);

        Assert.Equal(EditVerdict.ProtectedCell, guard.Check(_f.Document, table.Rows[0].Cells[0].Content.Blocks[0].Id));
        Assert.Equal(EditVerdict.Allowed, guard.Check(_f.Document, table.Rows[0].Cells[1].Content.Blocks[0].Id));

        _f.History.Undo();

        Assert.False(table.Rows[0].Cells[0].Protected);
    }
}
