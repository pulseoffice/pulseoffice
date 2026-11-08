using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class MasterEditingTests
{
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new Theme { Name = "Light" });

    [Fact]
    public void WithBackgroundReplacesMasterBackground()
    {
        var updated = new MasterEditing().WithBackground(_master, ColorRole.Accent1);
        Assert.Equal(ColorRole.Accent1, updated.Background);
        Assert.Equal(ColorRole.Background, _master.Background);
    }

    [Fact]
    public void WithLayoutBackgroundTouchesOnlyThatLayout()
    {
        var updated = new MasterEditing().WithLayoutBackground(_master, SlideLayout.TitleAndContent, ColorRole.Background2);
        Assert.Equal(ColorRole.Background2, updated.Template(SlideLayout.TitleAndContent).Background);
        Assert.Null(updated.Template(SlideLayout.Blank).Background);
    }

    [Fact]
    public void WithPlaceholderBoundsMovesOnePlaceholder()
    {
        var bounds = new DocumentRect(new Twip(1000), new Twip(2000), new Twip(3000), new Twip(4000));
        var updated = new MasterEditing().WithPlaceholderBounds(_master, SlideLayout.TitleSlide, PlaceholderRole.Title, bounds);

        var placeholder = updated.Template(SlideLayout.TitleSlide).Placeholders.First(p => p.Role == PlaceholderRole.Title);
        Assert.Equal(1000, placeholder.Bounds.Left.Value);
        Assert.Equal(3000, placeholder.Bounds.Width.Value);
    }
}
