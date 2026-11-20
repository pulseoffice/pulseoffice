using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Search;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round331DeckFindTests
{
    private readonly DeckTextSearch _search = new();

    private DocumentRect Box => new(new Twip(0), new Twip(0), new Twip(1440), new Twip(1440));

    private ObjectElement Table() => new()
    {
        Id = "t",
        Bounds = Box,
        Content = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot
            {
                Columns = [new DataColumn("Name", DataValueKind.Text)],
                Rows = [["alpha"], ["beta"]],
            },
            Style = new DataTableStyle { HeaderRow = true },
        },
    };

    private ObjectElement Shape() => new()
    {
        Id = "s",
        Bounds = Box,
        Content = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Text = "beta shape" },
    };

    private Slide Slide()
    {
        var slide = new Slide("one", SlideLayout.Blank);
        slide.Add(Table());
        slide.Add(Shape());
        return slide;
    }

    [Fact]
    public void Table_cells_and_shape_text_are_found()
    {
        var matches = _search.Matches([Slide()], "beta", matchCase: false);

        Assert.Contains(matches, match => match.ElementId == "t" && match.InCell && match.Row == 1 && match.Column == 0);
        Assert.Contains(matches, match => match.ElementId == "s" && !match.InCell);
    }

    [Fact]
    public void Replace_rewrites_table_cells_and_shape_text()
    {
        var rewrite = new DeckElementTextRewrite();
        Func<RichText, RichText> swap = text => _search.Replace(text, "beta", "gamma", matchCase: false).Text;

        var table = (ObjectElement)rewrite.Rewrite(Table(), swap);
        var shape = (ObjectElement)rewrite.Rewrite(Shape(), swap);

        Assert.Equal("gamma", ((DataTableObject)table.Content).Snapshot.Rows[1][0]);
        Assert.Equal("gamma shape", ((ShapeObject)shape.Content).Text);
    }

    [Fact]
    public void Untouched_elements_come_back_as_the_same_instance()
    {
        var table = Table();

        Assert.Same(table, new DeckElementTextRewrite().Rewrite(table, text => text));
    }

    [Fact]
    public void Highlight_spans_follow_the_query_case()
    {
        var highlights = new TextFindHighlights();
        var scope = new object();
        highlights.Show(scope, new TextFindQuery("ab", MatchCase: true));

        var spans = highlights.SpansIn(highlights.QueryOf(scope)!, "ab AB ab");

        Assert.Equal(new[] { 0, 6 }, spans.Select(span => span.Start));
        highlights.Clear(scope);
        Assert.Null(highlights.QueryOf(scope));
    }
}
