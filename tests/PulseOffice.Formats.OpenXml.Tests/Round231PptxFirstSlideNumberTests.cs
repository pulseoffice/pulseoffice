using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round231PptxFirstSlideNumberTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void The_first_slide_number_round_trips_as_firstSlideNum(int first)
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default, FirstSlideNumber = first };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));

        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream).Value!;

        Assert.Equal(first, read.FirstSlideNumber);
        Assert.Equal(first, new SlideFieldsFactory().For(read, read.Slides[0]).Number);
    }
}
