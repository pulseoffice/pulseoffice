using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round229SectionMoveTests
{
    [Fact]
    public void Moving_the_first_slide_of_a_section_moves_the_section_identity_with_its_name()
    {
        var slides = new List<Slide>
        {
            new("s1", SlideLayout.Blank) { SectionName = "A", SectionId = "{11111111-1111-4111-8111-111111111111}" },
            new("s2", SlideLayout.Blank),
            new("s3", SlideLayout.Blank) { SectionName = "B", SectionId = "{22222222-2222-4222-8222-222222222222}" },
        };

        var after = new SlideSections().SectionsAfterMove(slides, 0, 2);

        Assert.Equal(new SectionMarker("A", "{11111111-1111-4111-8111-111111111111}"), after[0]);
        Assert.Equal(new SectionMarker("B", "{22222222-2222-4222-8222-222222222222}"), after[1]);
        Assert.Null(after[2]);
    }
}
