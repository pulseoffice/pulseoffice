using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TableConversionFieldTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void UnSeparatoreDentroUnCampoNonSpezzaIlCampoNeMangiaTesto()
    {
        _f.Type("a;b ");
        _f.Editor.InsertField(_f.Document, _f.History, _f.At(0, 4), TextFieldKind.Title, "x;y");
        var conversion = new TableConversion(_f.Stories, _f.Text, new TableCellFactory(), _f.Deletion, new TableSpanCutting());

        conversion.ToTable(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 0), new TextTableOptions(TableTextSeparator.Semicolon), 400);

        var table = (TableBlock)_f.Document.Body[0];
        var cells = table.Rows[0].Cells.Select(cell => ((ParagraphBlock)cell.Content[0]).Text).ToList();
        Assert.Equal(["a", "b x;y"], cells);
    }
}
