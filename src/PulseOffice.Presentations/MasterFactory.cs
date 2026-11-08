using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.Decorations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public interface IMasterFactory
{
    SlideMaster CreateStandard(Theme theme);

    SlideMaster CreateStandard(Theme theme, SlideSize size);
}

public sealed class MasterFactory(ITextFrameDefaults frames, IThemeDecorations decorations, ISlideResize resize) : IMasterFactory
{
    public MasterFactory() : this(new TextFrameDefaults(), new ThemeDecorations(), new SlideResize())
    {
    }

    public SlideMaster CreateStandard(Theme theme, SlideSize size) =>
        size == SlideSize.Widescreen ? CreateStandard(theme) : resize.Fit(CreateStandard(theme), SlideSize.Widescreen, size);

    public SlideMaster CreateStandard(Theme theme)
    {
        var master = Standard(theme);
        var decoration = decorations.Build(theme);
        return master with
        {
            Objects = decoration.Master,
            Layouts = [.. master.Layouts.Select(layout => Decorated(layout, decoration) with
            {
                Placeholders = [.. layout.Placeholders.Select(placeholder => placeholder with { Frame = frames.For(layout.Kind, placeholder.Role) })],
            })],
        };
    }

    private SlideLayoutTemplate Decorated(SlideLayoutTemplate layout, MasterDecoration decoration) =>
        decoration.Layouts.TryGetValue(layout.Kind, out var own)
            ? layout with { Objects = own.Objects, HideMasterObjects = own.HidesMaster }
            : layout;

    private SlideMaster Standard(Theme theme) => new()
    {
        Id = "master1",
        Name = theme.Name,
        Theme = theme,
        Background = ColorRole.Background,
        Layouts =
        [
            new SlideLayoutTemplate
            {
                Id = "layout.title",
                Name = "title",
                Kind = SlideLayout.TitleSlide,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 2.5, 6.2, 28.87, 3.2,
                        Seed(40, true, TextAlign.Center, ColorRole.Text, major: true)),
                    Placeholder(PlaceholderRole.Subtitle, 2.5, 9.8, 28.87, 2.0,
                        Seed(20, false, TextAlign.Center, ColorRole.Text2)),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.titleContent",
                Name = "titleAndContent",
                Kind = SlideLayout.TitleAndContent,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 1.9, 1.0, 30.0, 2.4,
                        Seed(32, true, TextAlign.Left, ColorRole.Text, major: true)),
                    Placeholder(PlaceholderRole.Body, 1.9, 4.0, 30.0, 13.5, BodySeed()),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.section",
                Name = "sectionHeader",
                Kind = SlideLayout.SectionHeader,
                Background = ColorRole.Background2,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 2.5, 8.0, 28.87, 3.2,
                        Seed(36, true, TextAlign.Left, ColorRole.Accent1, major: true)),
                    Placeholder(PlaceholderRole.Subtitle, 2.5, 11.4, 28.87, 1.8,
                        Seed(18, false, TextAlign.Left, ColorRole.Text2)),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.two",
                Name = "twoContent",
                Kind = SlideLayout.TwoContent,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 1.9, 1.0, 30.0, 2.4,
                        Seed(32, true, TextAlign.Left, ColorRole.Text, major: true)),
                    Placeholder(PlaceholderRole.Body, 1.9, 4.0, 14.5, 13.5, BodySeed()),
                    Placeholder(PlaceholderRole.Body, 17.4, 4.0, 14.5, 13.5, BodySeed()),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.comparison",
                Name = "comparison",
                Kind = SlideLayout.Comparison,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 1.9, 1.0, 30.0, 2.4,
                        Seed(32, true, TextAlign.Left, ColorRole.Text, major: true)),
                    Placeholder(PlaceholderRole.Text, 1.9, 3.7, 14.5, 1.4,
                        Seed(18, true, TextAlign.Left, ColorRole.Accent1)),
                    Placeholder(PlaceholderRole.Text, 17.4, 3.7, 14.5, 1.4,
                        Seed(18, true, TextAlign.Left, ColorRole.Accent1)),
                    Placeholder(PlaceholderRole.Body, 1.9, 5.3, 14.5, 12.2, BodySeed()),
                    Placeholder(PlaceholderRole.Body, 17.4, 5.3, 14.5, 12.2, BodySeed()),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.titleOnly",
                Name = "titleOnly",
                Kind = SlideLayout.TitleOnly,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 1.9, 1.0, 30.0, 2.4,
                        Seed(32, true, TextAlign.Left, ColorRole.Text, major: true)),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.contentCaption",
                Name = "contentWithCaption",
                Kind = SlideLayout.ContentWithCaption,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 1.9, 1.0, 12.0, 2.4,
                        Seed(28, true, TextAlign.Left, ColorRole.Text, major: true)),
                    Placeholder(PlaceholderRole.Text, 1.9, 3.6, 12.0, 13.9,
                        Seed(16, false, TextAlign.Left, ColorRole.Text2)),
                    Placeholder(PlaceholderRole.Body, 14.6, 1.0, 17.3, 16.5, BodySeed()),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.pictureCaption",
                Name = "pictureWithCaption",
                Kind = SlideLayout.PictureWithCaption,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 1.9, 1.0, 12.0, 2.4,
                        Seed(28, true, TextAlign.Left, ColorRole.Text, major: true)),
                    Placeholder(PlaceholderRole.Text, 1.9, 3.6, 12.0, 13.9,
                        Seed(16, false, TextAlign.Left, ColorRole.Text2)),
                    Placeholder(PlaceholderRole.Picture, 14.6, 1.0, 17.3, 16.5,
                        Seed(18, false, TextAlign.Center, ColorRole.Text2)),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.verticalText",
                Name = "verticalText",
                Kind = SlideLayout.VerticalText,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 1.9, 1.0, 30.0, 2.4,
                        Seed(32, true, TextAlign.Left, ColorRole.Text, major: true)),
                    Placeholder(PlaceholderRole.Body, 1.9, 4.0, 30.0, 13.5,
                        new TextParagraph(string.Empty) { FontSize = 20, Bullet = true, ColorRole = ColorRole.Text, Vertical = true }),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.verticalTitleText",
                Name = "verticalTitleAndText",
                Kind = SlideLayout.VerticalTitleAndText,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 22.0, 1.0, 9.9, 16.5,
                        new TextParagraph(string.Empty) { FontSize = 32, Bold = true, ColorRole = ColorRole.Text, MajorFont = true, Vertical = true }),
                    Placeholder(PlaceholderRole.Body, 1.9, 1.0, 19.6, 16.5,
                        new TextParagraph(string.Empty) { FontSize = 20, Bullet = true, ColorRole = ColorRole.Text, Vertical = true }),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.centeredText",
                Name = "centeredText",
                Kind = SlideLayout.CenteredText,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Body, 3.0, 4.0, 27.87, 11.0,
                        new TextParagraph(string.Empty) { FontSize = 28, Align = TextAlign.Center, ColorRole = ColorRole.Text }),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.contentAndTwoContent",
                Name = "contentAndTwoContent",
                Kind = SlideLayout.ContentAndTwoContent,
                Placeholders =
                [
                    TitleTop(),
                    Box(1.9, 4.0, 14.5, 13.5),
                    Box(17.4, 4.0, 14.5, 6.5),
                    Box(17.4, 11.0, 14.5, 6.5),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.twoContentAndContent",
                Name = "twoContentAndContent",
                Kind = SlideLayout.TwoContentAndContent,
                Placeholders =
                [
                    TitleTop(),
                    Box(1.9, 4.0, 14.5, 6.5),
                    Box(1.9, 11.0, 14.5, 6.5),
                    Box(17.4, 4.0, 14.5, 13.5),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.contentOverContent",
                Name = "contentOverContent",
                Kind = SlideLayout.ContentOverContent,
                Placeholders =
                [
                    TitleTop(),
                    Box(1.9, 4.0, 30.0, 6.5),
                    Box(1.9, 11.0, 30.0, 6.5),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.twoContentOverContent",
                Name = "twoContentOverContent",
                Kind = SlideLayout.TwoContentOverContent,
                Placeholders =
                [
                    TitleTop(),
                    Box(1.9, 4.0, 14.5, 6.5),
                    Box(17.4, 4.0, 14.5, 6.5),
                    Box(1.9, 11.0, 30.0, 6.5),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.fourContent",
                Name = "fourContent",
                Kind = SlideLayout.FourContent,
                Placeholders =
                [
                    TitleTop(),
                    Box(1.9, 4.0, 14.5, 6.5),
                    Box(17.4, 4.0, 14.5, 6.5),
                    Box(1.9, 11.0, 14.5, 6.5),
                    Box(17.4, 11.0, 14.5, 6.5),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.sixContent",
                Name = "sixContent",
                Kind = SlideLayout.SixContent,
                Placeholders =
                [
                    TitleTop(),
                    Box(1.9, 4.0, 9.6, 6.5),
                    Box(12.1, 4.0, 9.6, 6.5),
                    Box(22.3, 4.0, 9.6, 6.5),
                    Box(1.9, 11.0, 9.6, 6.5),
                    Box(12.1, 11.0, 9.6, 6.5),
                    Box(22.3, 11.0, 9.6, 6.5),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.verticalTitleTextChart",
                Name = "verticalTitleTextChart",
                Kind = SlideLayout.VerticalTitleTextChart,
                Placeholders =
                [
                    Placeholder(PlaceholderRole.Title, 22.0, 1.0, 9.9, 16.5,
                        new TextParagraph(string.Empty) { FontSize = 32, Bold = true, ColorRole = ColorRole.Text, MajorFont = true, Vertical = true }),
                    Placeholder(PlaceholderRole.Body, 1.9, 1.0, 19.6, 8.0,
                        new TextParagraph(string.Empty) { FontSize = 20, Bullet = true, ColorRole = ColorRole.Text, Vertical = true }),
                    Box(1.9, 9.5, 19.6, 8.0),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.titleTwoVerticalText",
                Name = "titleTwoVerticalText",
                Kind = SlideLayout.TitleTwoVerticalText,
                Placeholders =
                [
                    TitleTop(),
                    Placeholder(PlaceholderRole.Body, 1.9, 4.0, 14.5, 13.5,
                        new TextParagraph(string.Empty) { FontSize = 20, Bullet = true, ColorRole = ColorRole.Text, Vertical = true }),
                    Placeholder(PlaceholderRole.Body, 17.4, 4.0, 14.5, 13.5,
                        new TextParagraph(string.Empty) { FontSize = 20, Bullet = true, ColorRole = ColorRole.Text, Vertical = true }),
                ],
            },

            new SlideLayoutTemplate
            {
                Id = "layout.blank",
                Name = "blank",
                Kind = SlideLayout.Blank,
            },
        ],
    };

    private PlaceholderTemplate Placeholder(
        PlaceholderRole role, double left, double top, double width, double height, TextParagraph seed) =>
        new(role,
            new DocumentRect(Twip.FromCentimeters(left), Twip.FromCentimeters(top),
                Twip.FromCentimeters(width), Twip.FromCentimeters(height)),
            seed);

    private TextParagraph Seed(
        double size, bool bold, TextAlign align, ColorRole role, bool major = false) =>
        new(string.Empty) { FontSize = size, Bold = bold, Align = align, ColorRole = role, MajorFont = major };

    private PlaceholderTemplate TitleTop() =>
        Placeholder(PlaceholderRole.Title, 1.9, 1.0, 30.0, 2.4, Seed(32, true, TextAlign.Left, ColorRole.Text, major: true));

    private PlaceholderTemplate Box(double left, double top, double width, double height) =>
        Placeholder(PlaceholderRole.Body, left, top, width, height, BodySeed());

    private TextParagraph BodySeed() =>
        new(string.Empty) { FontSize = 20, Bullet = true, ColorRole = ColorRole.Text };
}
