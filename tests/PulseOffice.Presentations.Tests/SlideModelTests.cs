using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideModelTests
{
    private readonly ISlideFactory _factory = new SlideFactory();
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);

    [Fact]
    public void TitleSlideHasTitleAndSubtitlePlaceholders()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleSlide);

        Assert.NotNull(slide.Placeholder(PlaceholderRole.Title));
        Assert.NotNull(slide.Placeholder(PlaceholderRole.Subtitle));
    }

    [Fact]
    public void PlaceholderGeometryComesFromTheMaster()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleAndContent);
        var template = _master.Template(SlideLayout.TitleAndContent).Placeholders[0];

        Assert.Equal(template.Bounds, slide.Placeholder(PlaceholderRole.Title)!.Bounds);
    }

    [Fact]
    public void CloneCopiesTextAndReidsElements()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleAndContent);
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(title with { Text = title.Text.WithPlainText("Ciao") });

        var clone = _factory.Clone(slide, "s2");

        Assert.Equal("Ciao", clone.Placeholder(PlaceholderRole.Title)!.Text.PlainText);
        Assert.StartsWith("s2.", clone.Placeholder(PlaceholderRole.Title)!.Id);
        Assert.NotEqual(slide.Placeholder(PlaceholderRole.Title)!.Id, clone.Placeholder(PlaceholderRole.Title)!.Id);
    }

    [Fact]
    public void RelayoutKeepsFilledPlaceholderText()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleAndContent);
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(title with { Text = title.Text.WithPlainText("Titolo vero") });

        var relaid = _factory.Relayout(slide, _master, SlideLayout.SectionHeader);

        Assert.Equal(SlideLayout.SectionHeader, relaid.Layout);
        Assert.Equal("Titolo vero", relaid.Placeholder(PlaceholderRole.Title)!.Text.PlainText);
    }

    [Fact]
    public void CloneCarriesTransitionAndAnimations()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.Blank);
        slide.Transition = new SlideTransition { Kind = TransitionKind.Fade, DurationMs = 300 };
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "s1.title", Preset = "fly-in" });

        var clone = _factory.Clone(slide, "s2");

        Assert.Equal(TransitionKind.Fade, clone.Transition.Kind);
        Assert.Single(clone.Animations);
        Assert.Equal("s2.title", clone.Animations[0].TargetId);
    }

    [Fact]
    public void MoveReordersSlides()
    {
        var deck = new Presentation();
        var a = _factory.Create(deck.NextId(), _master, SlideLayout.Blank);
        var b = _factory.Create(deck.NextId(), _master, SlideLayout.Blank);
        var c = _factory.Create(deck.NextId(), _master, SlideLayout.Blank);
        deck.AddExisting(a);
        deck.AddExisting(b);
        deck.AddExisting(c);

        Assert.True(deck.Move(0, 2));

        Assert.Equal([b, c, a], deck.Slides);
    }

    [Fact]
    public void WithPlainTextSplitsParagraphsAndKeepsFormatting()
    {
        var text = new RichText(PlaceholderRole.Body,
            [new TextParagraph("x") { Bullet = true, FontSize = 24 }]);

        var updated = text.WithPlainText("uno\ndue\ntre");

        Assert.Equal(3, updated.Paragraphs.Count);
        Assert.All(updated.Paragraphs, p => Assert.True(p.Bullet));
        Assert.Equal("due", updated.Paragraphs[1].Text);
        Assert.Equal(24, updated.Paragraphs[1].FontSize);
    }

    [Fact]
    public void ThemeResolvesColorRoles()
    {
        var theme = new ThemeCatalog().Find("Slate")!;

        Assert.Equal(theme.Colors.Background, theme.Colors.Resolve(ColorRole.Background));
        Assert.Equal(theme.Colors.Accent1, theme.Colors.Resolve(ColorRole.Accent1));
        Assert.NotEqual(theme.Colors.Background, theme.Colors.Text);
    }

    [Fact]
    public void SectionHeaderLayoutDeclaresItsOwnBackground()
    {
        Assert.Equal(ColorRole.Background2, _master.Template(SlideLayout.SectionHeader).Background);
        Assert.Null(_master.Template(SlideLayout.TitleSlide).Background);
    }

    [Fact]
    public void TitleUsesMajorFontAndBodyUsesMinor()
    {
        var titleSeed = _master.Template(SlideLayout.TitleAndContent).Placeholders[0].Seed;
        var bodySeed = _master.Template(SlideLayout.TitleAndContent).Placeholders[1].Seed;

        Assert.True(titleSeed.MajorFont);
        Assert.False(bodySeed.MajorFont);
    }

    [Fact]
    public void SetInteractionReplacesByTarget()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleSlide);
        var id = slide.Placeholder(PlaceholderRole.Title)!.Id;

        slide.SetInteraction(new InteractiveAction { TargetId = id, Kind = InteractionKind.NextSlide });
        slide.SetInteraction(new InteractiveAction { TargetId = id, Kind = InteractionKind.LastSlide });

        Assert.Single(slide.Interactions);
        Assert.Equal(InteractionKind.LastSlide, slide.Interaction(id)!.Kind);

        Assert.True(slide.RemoveInteraction(id));
        Assert.Null(slide.Interaction(id));
    }

    [Fact]
    public void CloneReidsMediaAndKeepsInteraction()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleAndContent);
        slide.Add(new MediaElement
        {
            Id = "s1.vid",
            Bounds = default,
            Kind = MediaKind.Video,
            Data = new byte[] { 1, 2, 3 },
            MimeType = "video/mp4",
        });
        slide.AddInteraction(new InteractiveAction { TargetId = "s1.vid", Kind = InteractionKind.StopShow });

        var clone = _factory.Clone(slide, "s2");
        var media = clone.Elements.OfType<MediaElement>().Single();

        Assert.Equal("s2.vid", media.Id);
        Assert.Equal(new byte[] { 1, 2, 3 }, media.Data);
        Assert.Equal(InteractionKind.StopShow, clone.Interaction("s2.vid")!.Kind);
    }

    [Fact]
    public void UnIdentificativoNuovoNonCollideConLeDiapositiveLette()
    {
        var deck = new Presentation();
        deck.AddExisting(_factory.Create("slide1", _master, SlideLayout.Blank));
        deck.AddExisting(_factory.Create("slide2", _master, SlideLayout.Blank));

        Assert.Equal("slide3", deck.NextId());
    }

    [Fact]
    public void CambiareLayoutConservaAnimazioniCommentiNomeEIdentificativi()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleAndContent);
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(title with { Text = title.Text.WithPlainText("Titolo") });
        slide.Name = "Apertura";
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = title.Id, Category = AnimationCategory.Entrance, Preset = "fade" });
        slide.AddComment(new SlideComment { Id = "c1", Author = "Ada", Text = "nota" });

        var relaid = _factory.Relayout(slide, _master, SlideLayout.TitleOnly);

        Assert.Equal("Apertura", relaid.Name);
        Assert.Single(relaid.Comments);
        var animation = Assert.Single(relaid.Animations);
        Assert.Equal("Titolo", relaid.Elements.OfType<TextElement>().Single(e => e.Id == animation.TargetId).Text.Paragraphs[0].Text);
    }
}
