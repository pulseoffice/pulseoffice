using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round302PptxTableRunsTests
{
    private static readonly TableCellText Rich = new(0, 0,
    [
        new TableCellParagraph([new TableTextRun("plain "), new TableTextRun("bold") { Bold = true, TextHex = "#FF0000" }]),
        new TableCellParagraph([new TableTextRun("big") { FontSize = 20, Italic = true }]),
    ]);

    private static ObjectElement Element(bool ownLook) => new()
    {
        Id = "t",
        Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(1000)),
        Content = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text)], Rows = [["plain bold\nbig"]] },
            Style = new DataTableStyle { OwnLook = ownLook, Grid = !ownLook },
            CellTexts = [Rich],
        },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Runs_inside_a_cell_survive_the_round_trip(bool ownLook)
    {
        var mapper = new PptxDeckTableMapper();
        var element = Element(ownLook);

        var frame = mapper.Build(element, (DataTableObject)element.Content, 2);
        var back = (DataTableObject)mapper.Read(frame, "t")!.Content;

        var text = Assert.Single(back.CellTexts, cellText => cellText.Row == 0);
        Assert.Equal("plain bold\nbig", text.PlainText);
        var bold = text.Paragraphs[0].Runs.Single(run => run.Text == "bold");
        Assert.True(bold.Bold);
        Assert.Equal("#FF0000", bold.TextHex);
        Assert.NotEqual(true, text.Paragraphs[0].Runs.Single(run => run.Text == "plain ").Bold);
        Assert.Equal(20, text.Paragraphs[1].Runs.Single().FontSize);
    }

    [Fact]
    public void A_grid_cell_writes_only_the_run_overrides()
    {
        var element = Element(false);

        var frame = new PptxDeckTableMapper().Build(element, (DataTableObject)element.Content, 2);

        var runs = frame.Descendants<D.Run>().ToList();
        Assert.Null(runs.Single(run => run.Text!.Text == "plain ").RunProperties!.Bold);
        Assert.True(runs.Single(run => run.Text!.Text == "bold").RunProperties!.Bold!.Value);
    }

    [Fact]
    public void Stale_runs_are_ignored_when_the_cell_value_changed()
    {
        var element = Element(false);
        var table = (DataTableObject)element.Content;
        table = table with { Snapshot = table.Snapshot with { Rows = [["other"]] } };

        var frame = new PptxDeckTableMapper().Build(element, table, 2);

        Assert.Equal("other", string.Concat(frame.Descendants<D.TableRow>().Last().Descendants<D.Text>().Select(text => text.Text)));
    }
}
