using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round229RelayoutSectionTests
{
    [Fact]
    public void A_layout_change_keeps_the_section_identity_of_the_slide_that_starts_it()
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var id = "{0B4F1A6E-2C3D-4E5F-8A9B-0C1D2E3F4A5B}";
        var source = new Slide("s1", SlideLayout.Blank) { SectionName = "Intro", SectionId = id };

        var result = new SlideFactory().Relayout(source, master, SlideLayout.TitleSlide);

        Assert.Equal("Intro", result.SectionName);
        Assert.Equal(id, result.SectionId);
    }
}
