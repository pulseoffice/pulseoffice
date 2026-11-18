using Avalonia;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Links;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round236TextLinkHitTests
{
    private sealed class PlainTheme : ITextTheme
    {
        public uint Resolve(ColorRole role) => 0xFF000000;

        public string MajorFont => "Inter";

        public string MinorFont => "Inter";
    }

    [Fact]
    public Task Only_the_linked_run_under_the_pointer_gives_its_link() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var paragraph = new TextParagraph("plain link") { Runs = [new TextRun("plain "), new TextRun("link") { Hyperlink = "https://example.org", HyperlinkTip = "Example" }] };
        var composed = new TextFrameComposer(new ListLabels()).Compose(
            new RichText(PlaceholderRole.Body, [paragraph]), new Rect(0, 0, 20000, 8000), new PlainTheme(), new TextFields(), 1.0);
        var segment = composed.Lines[0].Segments[0];
        var origin = composed.Origin(0, segment);
        var middle = origin.Y + segment.Text.Height / 2;
        var hits = new RichTextLinkHit();

        var linked = hits.At(composed, new TextFields(), new Point(origin.X + segment.Text.Width - 1, middle).Transform(composed.Placement));
        var plain = hits.At(composed, new TextFields(), new Point(origin.X + 1, middle).Transform(composed.Placement));

        Assert.Equal(new RichTextLink("https://example.org", "Example"), linked);
        Assert.Null(plain);
        Assert.Equal("Example", new LinkTips().Text(linked!.Address, linked.Tip));
    }, CancellationToken.None);
}
