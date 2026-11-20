using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round325JustifiedPlainCellTests
{
    private sealed class RecordingRichText : ITableCellRichText
    {
        public List<(TableCellText Text, TableCellLook Look)> Laid { get; } = [];

        public IReadOnlyList<FormattedText> Lines(TableCellText text, TableCellLook look, double size, IBrush brush, double width, CultureInfo culture)
        {
            Laid.Add((text, look));
            return [];
        }

        public void Draw(DrawingContext context, IReadOnlyList<FormattedText> lines, Point origin)
        {
        }
    }

    [Fact]
    public Task A_justified_cell_without_rich_text_is_laid_out_justified() => FolioScene.RunAsync(() =>
    {
        var rich = new RecordingRichText();
        var renderer = new DataTableRenderer(new TextCatalog(UiLanguage.English), new TableCellTextStyles(), new TableCellBox(), rich);
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Style = new DataTableStyle { HeaderRow = false, OwnLook = true },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["one two\nthree"]] },
            CellFormats = [new TableCellFormat(0, 0, CellVerticalAlign.Top) { Look = TableCellLook.Plain with { Horizontal = CellHorizontalAlign.Justify } }],
        };

        using var bitmap = new RenderTargetBitmap(new PixelSize(40, 40), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
            renderer.Render(context, table, new Rect(0, 0, 40, 40), CultureInfo.InvariantCulture);

        var (text, look) = Assert.Single(rich.Laid);
        Assert.Equal(CellHorizontalAlign.Justify, look.Horizontal);
        Assert.Equal(["one two", "three"], text.Paragraphs.Select(paragraph => paragraph.Text));
        return Task.CompletedTask;
    });
}
