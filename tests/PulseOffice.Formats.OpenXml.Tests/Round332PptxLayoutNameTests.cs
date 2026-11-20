using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round332PptxLayoutNameTests
{
    [Fact]
    public void A_standard_layout_keeps_its_own_name_and_an_unnamed_one_stays_standard()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        master = master with
        {
            Layouts = [.. master.Layouts.Select(layout => layout.Kind == SlideLayout.TitleAndContent ? layout with { Name = "Title, Content" } : layout)],
        };
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master.Template(SlideLayout.TitleAndContent)));

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new PptxReader(themes, masters).Read(buffer).Value!;

        var named = back.Master.Template(SlideLayout.TitleAndContent);
        Assert.Equal("Title, Content", named.Name);
        Assert.True(named.HasOwnName);
        Assert.False(back.Master.Template(SlideLayout.TitleOnly).HasOwnName);
        Assert.False(back.Master.Template(SlideLayout.TitleSlide).HasOwnName);
    }
}
