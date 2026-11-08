using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpCommentIdTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void Repeated_comment_ids_are_made_unique_when_read()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Theme = _themes.Default, Master = master };
        var factory = new SlideFactory();
        for (var index = 0; index < 2; index++)
        {
            var slide = factory.Create(deck.NextId(), master, SlideLayout.Blank);
            slide.AddComment(new SlideComment { Id = "dup", Author = "Ada", Text = "uno" });
            slide.AddComment(new SlideComment { Id = "dup", Author = "Bea", Text = "due" });
            deck.AddExisting(slide);
        }

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(buffer).Value!;

        var ids = read.Slides.SelectMany(slide => slide.Comments).Select(comment => comment.Id).ToList();
        Assert.Equal(4, ids.Count);
        Assert.Equal(4, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("dup", ids);
    }
}
