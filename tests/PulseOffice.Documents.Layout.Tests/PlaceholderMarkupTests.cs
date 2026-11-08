using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class PlaceholderMarkupTests
{
    private readonly PlaceholderMarkup _markup = new(new ParagraphText());

    [Fact]
    public void SoloIlTestoSegnapostoDiventaGrigioEIlRisultatoSiRiusa()
    {
        var block = new ParagraphBlock("p", "Nome: Scrivi qui")
        {
            Marks = [new ContentControlMark(6, "c", MarkEdge.Start) { Control = new ContentControl { ShowingPlaceholder = true } }, new ContentControlMark(16, "c", MarkEdge.End)],
        };
        var shown = _markup.Present(block, block.Content);
        Assert.Equal(["Nome: ", "Scrivi qui"], shown.Runs.Select(run => run.Text));
        Assert.Null(shown.Runs[0].Color);
        Assert.Equal(0xFF808080u, shown.Runs[1].Color);
        Assert.Same(shown, _markup.Present(block, block.Content));

        var filled = new ParagraphBlock("q", "Nome: Ada") { Marks = [new ContentControlMark(6, "c", MarkEdge.Start) { Control = new ContentControl() }, new ContentControlMark(9, "c", MarkEdge.End)] };
        Assert.Same(filled.Content, _markup.Present(filled, filled.Content));
    }
}
