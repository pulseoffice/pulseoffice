using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxWriteFailureTests
{
    private sealed class FailingStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("disk full");

        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("disk full");

        public override void WriteByte(byte value) => throw new IOException("disk full");
    }

    [Fact]
    public void A_stream_that_fails_while_writing_gives_a_failed_result_like_the_other_writers()
    {
        var theme = new ThemeCatalog().Find("Slate")!;
        var master = new MasterFactory().CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleOnly));
        using var stream = new FailingStream();

        var result = new PptxWriter().Write(deck, stream);

        Assert.False(result.Succeeded);
        Assert.Equal("PPTX106", Assert.Single(result.Diagnostics).Code);
    }
}
