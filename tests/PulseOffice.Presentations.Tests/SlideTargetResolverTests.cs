using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideTargetResolverTests
{
    private readonly SlideTargetResolver _resolver = new();

    private static Presentation Deck()
    {
        var deck = new Presentation();
        deck.AddExisting(new Slide("s1", SlideLayout.Blank));
        var second = new Slide("s2", SlideLayout.Blank) { Name = "Results" };
        second.Add(new ObjectElement { Id = "e1", Content = new ShapeObject { Id = "e1", Shape = ShapeKind.Rectangle, Name = "star" } });
        deck.AddExisting(second);
        deck.AddExisting(new Slide("s3", SlideLayout.Blank));
        return deck;
    }

    [Theory]
    [InlineData("Results", "s2")]
    [InlineData("s3", "s3")]
    [InlineData("Slide 3", "s3")]
    [InlineData("page1", "s1")]
    [InlineData("star", "s2")]
    public void FindsTheSlideByNameIdPositionOrObjectName(string target, string expected) =>
        Assert.Equal(expected, _resolver.Find(Deck(), target)?.Id);

    [Theory]
    [InlineData("Slide 9")]
    [InlineData("Slide 0")]
    [InlineData("nowhere")]
    public void ReturnsNothingForMissingTargets(string target) => Assert.Null(_resolver.Find(Deck(), target));
}
