using PulseOffice.Formats;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Sample()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };

        var title = _slides.Create(deck.NextId(), master, SlideLayout.TitleSlide);
        var t = title.Placeholder(PlaceholderRole.Title)!;
        title.Replace(t with { Text = t.Text.WithPlainText("Relazione trimestrale") });
        var sub = title.Placeholder(PlaceholderRole.Subtitle)!;
        title.Replace(sub with { Text = sub.Text.WithPlainText("Consiglio") });
        deck.AddExisting(title);

        var content = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        var ct = content.Placeholder(PlaceholderRole.Title)!;
        content.Replace(ct with { Text = ct.Text.WithPlainText("Risultati") });
        var body = content.Placeholder(PlaceholderRole.Body)!;
        content.Replace(body with { Text = body.Text.WithPlainText("Ricavi in crescita\nMargine stabile") });
        deck.AddExisting(content);

        return deck;
    }

    private Presentation RoundTrip(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);

        buffer.Position = 0;
        var result = new PptxReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void SlidesAndTitlesSurvive()
    {
        var back = RoundTrip(Sample());

        Assert.Equal(2, back.Slides.Count);
        Assert.Equal("Relazione trimestrale", back.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
        Assert.Equal("Risultati", back.Slides[1].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
    }

    [Fact]
    public void HiddenSlideSurvives()
    {
        var deck = Sample();
        deck.Slides[1].Hidden = true;

        var back = RoundTrip(deck);

        Assert.False(back.Slides[0].Hidden);
        Assert.True(back.Slides[1].Hidden);
    }

    [Fact]
    public void SectionsSurvive()
    {
        var deck = Sample();
        deck.Slides[0].SectionName = "Intro";
        deck.Slides[1].SectionName = "Corpo";

        var back = RoundTrip(deck);

        Assert.Equal("Intro", back.Slides[0].SectionName);
        Assert.Equal("Corpo", back.Slides[1].SectionName);
    }

    [Theory]
    [InlineData(TextFieldKind.SlideCount, "3")]
    [InlineData(TextFieldKind.SlideTitle, "Conclusioni")]
    [InlineData(TextFieldKind.Author, "Mario Rossi")]
    [InlineData(TextFieldKind.FileName, "relazione.odp")]
    public void IlNuovoCampoSopravviveAlGiro(TextFieldKind kind, string shown)
    {
        var deck = Sample();
        var run = new TextRun(shown) { Field = kind };
        deck.Slides[1].Add(new TextElement
        {
            Id = "field2",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(1)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph(shown) { Runs = [run] }]),
        });

        var back = RoundTrip(deck);

        var field = back.Slides[1].Elements.OfType<TextElement>()
            .SelectMany(t => t.Text.Paragraphs)
            .SelectMany(p => p.Runs)
            .FirstOrDefault(r => r.Field != TextFieldKind.None);

        Assert.NotNull(field);
        Assert.Equal(kind, field!.Field);
        Assert.Equal(shown, field.Text);
    }

    [Fact]
    public void SlideNumberFieldSurvives()
    {
        var deck = Sample();
        var run = new TextRun("2") { Field = TextFieldKind.SlideNumber };
        deck.Slides[1].Add(new TextElement
        {
            Id = "field1",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(1)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("2") { Runs = [run] }]),
        });

        var back = RoundTrip(deck);

        var field = back.Slides[1].Elements.OfType<TextElement>()
            .SelectMany(t => t.Text.Paragraphs)
            .SelectMany(p => p.Runs)
            .FirstOrDefault(r => r.Field != TextFieldKind.None);

        Assert.NotNull(field);
        Assert.Equal(TextFieldKind.SlideNumber, field!.Field);
    }

    [Fact]
    public void TextHighlightSurvives()
    {
        var deck = Sample();
        var marked = new TextRun("evidenziato") { Highlight = 0xFFFFFF00 };
        var plain = new TextRun(" normale");
        deck.Slides[1].Add(new TextElement
        {
            Id = "hl",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(8), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("evidenziato normale") { Runs = [marked, plain] }]),
        });

        var back = RoundTrip(deck);
        var runs = back.Slides[1].Elements.OfType<TextElement>()
            .SelectMany(t => t.Text.Paragraphs).SelectMany(p => p.Runs).ToList();

        Assert.Equal(0xFFFFFF00u, runs.First(x => x.Text == "evidenziato").Highlight);
        Assert.Null(runs.First(x => x.Text == " normale").Highlight);
    }

    [Fact]
    public void NativeCharacterEffectsSurvive()
    {
        var deck = Sample();
        var run = new TextRun("effetti") { DoubleUnderline = true, Outline = true, SmallCaps = true };
        deck.Slides[1].Add(new TextElement
        {
            Id = "fx",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(6), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("effetti") { Runs = [run] }]),
        });

        var back = RoundTrip(deck);
        var r = back.Slides[1].Elements.OfType<TextElement>()
            .SelectMany(t => t.Text.Paragraphs).SelectMany(p => p.Runs).First(x => x.Text == "effetti");

        Assert.True(r.DoubleUnderline);
        Assert.True(r.Outline);
        Assert.True(r.SmallCaps);
    }

    [Fact]
    public void TableMergeSurvives()
    {
        var deck = Sample();
        var snapshot = new PulseOffice.Document.Embedding.DataSnapshot
        {
            Columns = [new("A", PulseOffice.Document.Embedding.DataValueKind.Text), new("B", PulseOffice.Document.Embedding.DataValueKind.Text)],
            Rows = [["x", "y"], ["z", "w"]],
        };
        deck.Slides[1].Add(new ObjectElement
        {
            Id = "tbl",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(8), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Content = new PulseOffice.Document.Embedding.DataTableObject
            {
                Id = "tbl",
                Query = new PulseOffice.Document.Embedding.DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = snapshot,
                Merges = [new PulseOffice.Document.Embedding.TableMerge(0, 0, 1, 2)],
            },
        });

        var back = RoundTrip(deck);
        var table = back.Slides[1].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.DataTableObject>().First();

        Assert.Single(table.Merges);
        Assert.Equal(2, table.Merges[0].ColumnSpan);
    }

    [Fact]
    public void HeaderRowMergesSurvive()
    {
        var deck = Sample();
        deck.Slides[1].Add(new ObjectElement
        {
            Id = "tbh",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(9), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Content = new PulseOffice.Document.Embedding.DataTableObject
            {
                Id = "tbh",
                Query = new PulseOffice.Document.Embedding.DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new PulseOffice.Document.Embedding.DataSnapshot
                {
                    Columns = [new("Gruppo", PulseOffice.Document.Embedding.DataValueKind.Text), new("", PulseOffice.Document.Embedding.DataValueKind.Text), new("Note", PulseOffice.Document.Embedding.DataValueKind.Text)],
                    Rows = [["a", "b", ""], ["c", "d", "e"]],
                },
                Style = new PulseOffice.Document.Embedding.DataTableStyle { HeaderRow = true },
                Merges =
                [
                    new PulseOffice.Document.Embedding.TableMerge(PulseOffice.Document.Embedding.TableCellFormat.HeaderRow, 0, 1, 2),
                    new PulseOffice.Document.Embedding.TableMerge(PulseOffice.Document.Embedding.TableCellFormat.HeaderRow, 2, 2, 1),
                ],
            },
        });

        var back = RoundTrip(deck);
        var table = back.Slides[1].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.DataTableObject>().Single(t => t.Id == "tbh" || t.Snapshot.Columns.Count == 3);

        Assert.Equal(
            [(-1, 0, 1, 2), (-1, 2, 2, 1)],
            table.Merges.Select(merge => (merge.Row, merge.Column, merge.RowSpan, merge.ColumnSpan)).OrderBy(merge => merge.Column).ToArray());
        Assert.Equal("Gruppo", table.Snapshot.Columns[0].Name);
        Assert.Equal("Note", table.Snapshot.Columns[2].Name);
    }

    [Fact]
    public void ListsLevelsAndSpacingSurvive()
    {
        var deck = Sample();
        var body = deck.Slides[1].Placeholder(PlaceholderRole.Body)!;
        deck.Slides[1].Replace(body with
        {
            Text = new RichText(PlaceholderRole.Body,
            [
                new TextParagraph("uno") { Numbered = true },
                new TextParagraph("uno.a") { Numbered = true, BulletLevel = 1 },
                new TextParagraph("punto") { Bullet = true, SpaceBefore = 6, SpaceAfter = 12 },
                new TextParagraph("libero"),
            ]),
        });

        var back = RoundTrip(deck);
        var paragraphs = back.Slides[1].Placeholder(PlaceholderRole.Body)!.Text.Paragraphs;
        TextParagraph Find(string text) => paragraphs.First(p => p.Text == text);

        Assert.True(Find("uno").Numbered);
        Assert.True(Find("uno.a").Numbered);
        Assert.Equal(1, Find("uno.a").BulletLevel);
        Assert.True(Find("punto").Bullet);
        Assert.False(Find("punto").Numbered);
        Assert.Equal(6, Find("punto").SpaceBefore, 1);
        Assert.Equal(12, Find("punto").SpaceAfter, 1);
        Assert.False(Find("libero").IsListItem);
    }

    [Fact]
    public void LineBreaksTabsAndSpacesInRunsSurvive()
    {
        var deck = Sample();
        deck.Slides[1].Add(new TextElement
        {
            Id = "breaks",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(8), PulseOffice.Foundation.Units.Twip.FromCentimeters(3)),
            Text = new RichText(PlaceholderRole.Free,
            [
                new TextParagraph("a\tb  c\nd") { Runs = [new TextRun("a\tb  c\n") { Bold = true }, new TextRun("d") { Italic = true }] },
            ]),
        });

        var back = RoundTrip(deck);
        var para = back.Slides[1].Elements.OfType<TextElement>().SelectMany(t => t.Text.Paragraphs).First(p => p.Text.StartsWith("a\t", StringComparison.Ordinal) || p.Text.StartsWith("a ", StringComparison.Ordinal));

        Assert.Equal("a\tb  c\nd", para.Text);
    }

    [Fact]
    public void LineSpacingSurvives()
    {
        var deck = Sample();
        deck.Slides[1].Add(new TextElement
        {
            Id = "spaced",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(6), PulseOffice.Foundation.Units.Twip.FromCentimeters(3)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("riga") { LineSpacing = 1.5 }]),
        });

        var back = RoundTrip(deck);

        var para = back.Slides[1].Elements.OfType<TextElement>()
            .SelectMany(t => t.Text.Paragraphs).FirstOrDefault(p => p.Text == "riga");

        Assert.NotNull(para);
        Assert.Equal(1.5, para!.LineSpacing, 2);
    }

    [Fact]
    public void HeaderFooterSurvives()
    {
        var deck = Sample();
        deck.HeaderFooter = new HeaderFooterSettings
        {
            ShowDate = true,
            ShowFooter = true,
            FooterText = "Riservato",
            ShowSlideNumber = true,
            ShowOnTitleSlide = false,
        };

        var back = RoundTrip(deck);

        Assert.True(back.HeaderFooter.ShowDate);
        Assert.True(back.HeaderFooter.ShowFooter);
        Assert.Equal("Riservato", back.HeaderFooter.FooterText);
        Assert.True(back.HeaderFooter.ShowSlideNumber);
        Assert.False(back.HeaderFooter.ShowOnTitleSlide);
    }

    [Fact]
    public void ShowSetupSurvives()
    {
        var deck = Sample();
        deck.Setup = new ShowSetup
        {
            Loop = true,
            WithoutAnimation = true,
            Advance = ShowAdvanceMode.Manual,
            FromSlide = 1,
            ToSlide = 2,
            PenColor = "#2563EB",
        };

        var back = RoundTrip(deck);

        Assert.True(back.Setup.Loop);
        Assert.True(back.Setup.WithoutAnimation);
        Assert.Equal(ShowAdvanceMode.Manual, back.Setup.Advance);
        Assert.Equal(1, back.Setup.FromSlide);
        Assert.Equal(2, back.Setup.ToSlide);
        Assert.Equal("#2563EB", back.Setup.PenColor);
    }

    [Fact]
    public void CustomShowSurvives()
    {
        var deck = Sample();
        deck.AddCustomShow(new CustomShow("Breve", [deck.Slides[1].Id, deck.Slides[0].Id]));

        var back = RoundTrip(deck);

        Assert.Single(back.CustomShows);
        var show = back.CustomShows[0];
        Assert.Equal("Breve", show.Name);
        Assert.Equal(2, show.SlideIds.Count);
        Assert.Equal(back.Slides[1].Id, show.SlideIds[0]);
        Assert.Equal(back.Slides[0].Id, show.SlideIds[1]);
    }

    [Fact]
    public void BodyParagraphsSurvive()
    {
        var back = RoundTrip(Sample());
        var body = back.Slides[1].Placeholder(PlaceholderRole.Body)!;

        Assert.Equal(2, body.Text.Paragraphs.Count);
        Assert.Equal("Ricavi in crescita", body.Text.Paragraphs[0].Text);
        Assert.Equal("Margine stabile", body.Text.Paragraphs[1].Text);
    }

    [Fact]
    public void ThemeColoursAndFontsSurvive()
    {
        var deck = Sample();
        var back = RoundTrip(deck);

        Assert.Equal(deck.Theme.Colors.Background, back.Theme.Colors.Background);
        Assert.Equal(deck.Theme.Colors.Accent1, back.Theme.Colors.Accent1);
        Assert.Equal(deck.Theme.Colors.Text, back.Theme.Colors.Text);
    }

    [Fact]
    public void SlideSizeSurvives()
    {
        var back = RoundTrip(Sample());
        Assert.Equal(SlideSize.Widescreen.Width.Value, back.Size.Width.Value, tolerance: 5);
        Assert.Equal(SlideSize.Widescreen.Height.Value, back.Size.Height.Value, tolerance: 5);
    }

    [Fact]
    public void TitleFormattingSurvives()
    {
        var back = RoundTrip(Sample());
        var title = back.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.Paragraphs[0];

        Assert.True(title.Bold);
        Assert.Equal(40, title.FontSize);
    }

    [Fact]
    public void LayoutIsInferred()
    {
        var back = RoundTrip(Sample());
        Assert.Equal(SlideLayout.TitleSlide, back.Slides[0].Layout);
        Assert.Equal(SlideLayout.TitleAndContent, back.Slides[1].Layout);
    }

    [Fact]
    public void FidelityDeclaresUnwrittenFeatures()
    {
        var deck = Sample();
        deck.Slides[0].AddInteraction(new InteractiveAction { TargetId = "x", Kind = InteractionKind.PlaySound, SoundPath = "a.wav" });

        var report = new PptxWriter().CheckFidelity(deck);
        Assert.Contains(report.Losses, l => l.Code == "PPTX003");
    }

    [Theory]
    [InlineData(SlideLayout.TitleSlide)]
    [InlineData(SlideLayout.TitleAndContent)]
    [InlineData(SlideLayout.SectionHeader)]
    [InlineData(SlideLayout.TwoContent)]
    [InlineData(SlideLayout.Comparison)]
    [InlineData(SlideLayout.TitleOnly)]
    [InlineData(SlideLayout.Blank)]
    [InlineData(SlideLayout.ContentWithCaption)]
    [InlineData(SlideLayout.PictureWithCaption)]
    [InlineData(SlideLayout.VerticalText)]
    [InlineData(SlideLayout.VerticalTitleAndText)]
    [InlineData(SlideLayout.CenteredText)]
    [InlineData(SlideLayout.ContentAndTwoContent)]
    [InlineData(SlideLayout.TwoContentAndContent)]
    [InlineData(SlideLayout.ContentOverContent)]
    [InlineData(SlideLayout.TwoContentOverContent)]
    [InlineData(SlideLayout.FourContent)]
    [InlineData(SlideLayout.SixContent)]
    [InlineData(SlideLayout.VerticalTitleTextChart)]
    [InlineData(SlideLayout.TitleTwoVerticalText)]
    public void EveryLayoutSurvives(SlideLayout layout)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.AddExisting(_slides.Create(deck.NextId(), master, layout));

        var back = RoundTrip(deck);

        Assert.Equal(layout, back.Slides[0].Layout);
    }

    [Theory]
    [InlineData(InteractionKind.NextSlide, null)]
    [InlineData(InteractionKind.PreviousSlide, null)]
    [InlineData(InteractionKind.FirstSlide, null)]
    [InlineData(InteractionKind.LastSlide, null)]
    [InlineData(InteractionKind.StopShow, null)]
    [InlineData(InteractionKind.Hyperlink, "https://example.org/")]
    [InlineData(InteractionKind.RunMacro, "Macro")]
    public void InteractionsSurvive(InteractionKind kind, string? reference)
    {
        var deck = Sample();
        var target = deck.Slides[0].Elements[0];
        deck.Slides[0].AddInteraction(new InteractiveAction { TargetId = target.Id, Kind = kind, Reference = reference });

        var back = RoundTrip(deck);
        var action = back.Slides[0].Interaction(back.Slides[0].Elements[0].Id);

        Assert.NotNull(action);
        Assert.Equal(kind, action!.Kind);
        Assert.Equal(reference, action.Reference);
    }

    [Theory]
    [InlineData(MediaKind.Video, "video/mp4")]
    [InlineData(MediaKind.Audio, "audio/mpeg")]
    public void MediaSurvives(MediaKind kind, string mime)
    {
        var deck = Sample();
        var bytes = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        deck.Slides[0].Add(new MediaElement
        {
            Id = "m1",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
                PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
                PulseOffice.Foundation.Units.Twip.FromCentimeters(8),
                PulseOffice.Foundation.Units.Twip.FromCentimeters(5)),
            ZOrder = 9,
            Kind = kind,
            Data = bytes,
            MimeType = mime,
        });

        var back = RoundTrip(deck);
        var media = back.Slides[0].Elements.OfType<MediaElement>().SingleOrDefault();

        Assert.NotNull(media);
        Assert.Equal(kind, media!.Kind);
        Assert.Equal(bytes, media.Data);
    }

    [Fact]
    public void RichRunsSurvive()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        var body = slide.Placeholder(PlaceholderRole.Body)!;
        slide.Replace(body with
        {
            Text = new RichText(PlaceholderRole.Body,
            [
                new TextParagraph("Bold plain") { Runs =
                [
                    new TextRun("Bold") { Bold = true, Underline = true, FontSize = 24 },
                    new TextRun(" plain") { Color = 0xFFC0392B },
                ] },
            ]),
        });
        deck.AddExisting(slide);

        var back = RoundTrip(deck);
        var paragraph = back.Slides[0].Placeholder(PlaceholderRole.Body)!.Text.Paragraphs[0];
        var runs = paragraph.EffectiveRuns();

        Assert.True(runs.Count >= 2);
        var boldRun = runs.First(r => r.Text.StartsWith("Bold"));
        Assert.True(boldRun.Bold);
        Assert.True(boldRun.Underline);
        Assert.Contains(runs, r => r.Color == 0xFFC0392B);
    }

    [Fact]
    public void ShapeAndImageSurvive()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);

        var rect = new PulseOffice.Foundation.Units.DocumentRect(
            PulseOffice.Foundation.Units.Twip.FromCentimeters(3),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(3),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(6),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(4));

        slide.Add(new ObjectElement
        {
            Id = "shape1",
            Bounds = rect,
            ZOrder = 5,
            Content = new PulseOffice.Drawing.Shapes.ShapeObject
            {
                Id = "shape1",
                Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse,
                Text = "Hi",
                Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with
                {
                    Fill = PulseOffice.Drawing.Shapes.ShapeFill.Default with { ColorHex = "#C0392B" },
                },
            },
        });

        var bytes = new byte[] { 5, 6, 7, 8, 9, 10 };
        slide.Add(new ObjectElement
        {
            Id = "img1",
            Bounds = rect,
            ZOrder = 6,
            Content = new PulseOffice.Drawing.Assets.RasterImageObject
            {
                Id = "img1",
                Data = bytes,
                MediaType = "image/png",
            },
        });
        deck.AddExisting(slide);

        var back = RoundTrip(deck);
        var objects = back.Slides[0].Elements.OfType<ObjectElement>().ToList();

        var shape = objects.Select(o => o.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>().SingleOrDefault();
        Assert.NotNull(shape);
        Assert.Equal(PulseOffice.Drawing.Shapes.ShapeKind.Ellipse, shape!.Shape);
        Assert.Equal("Hi", shape.Text);
        Assert.Equal("#C0392B", shape.Style.Fill.ColorHex, ignoreCase: true);

        var image = objects.Select(o => o.Content).OfType<PulseOffice.Drawing.Assets.RasterImageObject>().SingleOrDefault();
        Assert.NotNull(image);
        Assert.Equal(bytes, image!.Data.ToArray());
    }

    [Fact]
    public void TableSurvives()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);

        var rect = new PulseOffice.Foundation.Units.DocumentRect(
            PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(12),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(6));

        slide.Add(new ObjectElement
        {
            Id = "tbl1", Bounds = rect, ZOrder = 5,
            Content = new PulseOffice.Document.Embedding.DataTableObject
            {
                Id = "tbl1",
                Query = new PulseOffice.Document.Embedding.DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new PulseOffice.Document.Embedding.DataSnapshot
                {
                    Columns =
                    [
                        new PulseOffice.Document.Embedding.DataColumn("Nome", PulseOffice.Document.Embedding.DataValueKind.Text),
                        new PulseOffice.Document.Embedding.DataColumn("Valore", PulseOffice.Document.Embedding.DataValueKind.Text),
                    ],
                    Rows = [ ["Dado", "10"], ["Vite", "4"] ],
                },
            },
        });
        deck.AddExisting(slide);

        var back = RoundTrip(deck);
        var table = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.DataTableObject>().SingleOrDefault();

        Assert.NotNull(table);
        Assert.Equal(2, table!.Snapshot.Columns.Count);
        Assert.Equal("Nome", table.Snapshot.Columns[0].Name);
        Assert.Equal(2, table.Snapshot.Rows.Count);
        Assert.Equal("Dado", table.Snapshot.Rows[0][0]?.ToString());
        Assert.Equal("4", table.Snapshot.Rows[1][1]?.ToString());
    }

    [Fact]
    public void FormulaSurvives()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);

        var rect = new PulseOffice.Foundation.Units.DocumentRect(
            PulseOffice.Foundation.Units.Twip.FromCentimeters(3),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(3),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(8),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(3));

        slide.Add(new ObjectElement
        {
            Id = "f1", Bounds = rect, ZOrder = 5,
            Content = new PulseOffice.Document.Embedding.FormulaObject { Id = "f1", Expression = "sqrt {a^2 + b^2}" },
        });
        deck.AddExisting(slide);

        var back = RoundTrip(deck);
        var formula = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.FormulaObject>().SingleOrDefault();

        Assert.NotNull(formula);
        Assert.Equal("sqrt {a^2 + b^2}", formula!.Expression);
    }

    [Fact]
    public void ChartSurvives()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);

        var rect = new PulseOffice.Foundation.Units.DocumentRect(
            PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(14),
            PulseOffice.Foundation.Units.Twip.FromCentimeters(9));

        slide.Add(new ObjectElement
        {
            Id = "ch1", Bounds = rect, ZOrder = 5,
            Content = new PulseOffice.Charts.ChartDefinition
            {
                Id = "ch1", Type = PulseOffice.Charts.ChartType.Column,
                SourceSheet = "", SourceRange = "", ChartTitle = "Vendite",
                InlineData = new PulseOffice.Charts.ChartData(
                    ["Gen", "Feb", "Mar"],
                    [
                        new PulseOffice.Charts.ChartSeries("Nord", [10, 20, 15]),
                        new PulseOffice.Charts.ChartSeries("Sud", [5, 8, 12]),
                    ]),
            },
        });
        deck.AddExisting(slide);

        var back = RoundTrip(deck);
        var chart = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Charts.ChartDefinition>().SingleOrDefault();

        Assert.NotNull(chart);
        Assert.Equal("Vendite", chart!.ChartTitle);
        Assert.NotNull(chart.InlineData);
        Assert.Equal(3, chart.InlineData!.Categories.Count);
        Assert.Equal(2, chart.InlineData.Series.Count);
        Assert.Equal("Nord", chart.InlineData.Series[0].Name);
        Assert.Equal(20, chart.InlineData.Series[0].Values[1]);
    }

    [Theory]
    [InlineData(PulseOffice.Charts.ChartType.Doughnut)]
    [InlineData(PulseOffice.Charts.ChartType.StackedArea)]
    [InlineData(PulseOffice.Charts.ChartType.SmoothLine)]
    [InlineData(PulseOffice.Charts.ChartType.PercentStackedBar)]
    public void ChartTypeAndLegendSurvive(PulseOffice.Charts.ChartType type)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "ch1",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.FromCentimeters(2), PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
                PulseOffice.Foundation.Units.Twip.FromCentimeters(14), PulseOffice.Foundation.Units.Twip.FromCentimeters(9)),
            Content = new PulseOffice.Charts.ChartDefinition
            {
                Id = "ch1", Type = type, SourceSheet = "", SourceRange = "",
                Legend = PulseOffice.Charts.LegendPosition.Bottom,
                InlineData = new PulseOffice.Charts.ChartData(
                    ["Gen", "Feb"], [new PulseOffice.Charts.ChartSeries("Nord", [1, 2]), new PulseOffice.Charts.ChartSeries("Sud", [3, 4])]),
            },
        });
        deck.AddExisting(slide);

        var chart = RoundTrip(deck).Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Charts.ChartDefinition>().Single();

        Assert.Equal(type, chart.Type);
        Assert.Equal(PulseOffice.Charts.LegendPosition.Bottom, chart.Legend);
        Assert.Equal(2, chart.InlineData!.Series.Count);
    }

    [Fact]
    public void ShapeStyleSurvivesInFull()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);

        var style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with
        {
            Fill = PulseOffice.Drawing.Shapes.ShapeFill.Default with
            {
                Kind = PulseOffice.Drawing.Shapes.ShapeFillKind.Gradient,
                Gradient = PulseOffice.Drawing.Shapes.ShapeGradient.Default with
                {
                    Stops = [new("#FF0000", 0), new("#0000FF", 1)],
                    AngleDegrees = 45,
                },
            },
            Line = PulseOffice.Drawing.Shapes.ShapeLine.Default with { ColorHex = "#00AA00", Dash = PulseOffice.Drawing.Shapes.ShapeStrokeStyle.Dashed },
            Shadow = PulseOffice.Drawing.Shapes.ShapeShadow.Default with { Enabled = true, ColorHex = "#333333" },
            Text = PulseOffice.Drawing.Shapes.ShapeTextStyle.Default with { Bold = true, Size = 20 },
        };

        slide.Add(new ObjectElement
        {
            Id = "s1",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.FromCentimeters(2), PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
                PulseOffice.Foundation.Units.Twip.FromCentimeters(6), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject
            {
                Id = "s1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse, Text = "Ciao", Style = style,
            },
        });
        deck.AddExisting(slide);

        var shape = RoundTrip(deck).Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>().Single();

        Assert.Equal(PulseOffice.Drawing.Shapes.ShapeFillKind.Gradient, shape.Style.Fill.Kind);
        Assert.Equal("#FF0000", shape.Style.Fill.Gradient.Stops[0].ColorHex);
        Assert.Equal(45, shape.Style.Fill.Gradient.AngleDegrees, 1);
        Assert.Equal(PulseOffice.Drawing.Shapes.ShapeStrokeStyle.Dashed, shape.Style.Line.Dash);
        Assert.True(shape.Style.Shadow.Enabled);
        Assert.True(shape.Style.Text.Bold);
        Assert.Equal(20, shape.Style.Text.Size, 1);
    }

    [Fact]
    public void TransitionInsideAlternateContentIsRead()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Transition = new SlideTransition { Kind = TransitionKind.Push, Direction = TransitionDirection.Up };
        deck.AddExisting(slide);

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);

        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, true))
        {
            var part = document.PresentationPart!.SlideParts.First();
            var slideRoot = part.Slide!;
            foreach (var choice in slideRoot.Descendants<DocumentFormat.OpenXml.AlternateContentChoice>().ToList())
                choice.Remove();
            slideRoot.Save();
        }

        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal(TransitionKind.Push, back.Slides[0].Transition.Kind);
        Assert.Equal(TransitionDirection.Up, back.Slides[0].Transition.Direction);
    }

    [Fact]
    public void ExactTransitionDurationSurvives()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Transition = new SlideTransition { Kind = TransitionKind.Push, Direction = TransitionDirection.Up, DurationMs = 700 };
        deck.AddExisting(slide);

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal(700, back.Slides[0].Transition.DurationMs);
        Assert.Equal(TransitionKind.Push, back.Slides[0].Transition.Kind);
    }

    [Fact]
    public void LeMisureDellaTabellaSopravvivonoAlGiro()
    {
        var deck = Sample();
        deck.Slides[1].Add(new ObjectElement
        {
            Id = "sized",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(10), PulseOffice.Foundation.Units.Twip.FromCentimeters(3)),
            Content = new PulseOffice.Document.Embedding.DataTableObject
            {
                Id = "sized",
                Query = new PulseOffice.Document.Embedding.DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new PulseOffice.Document.Embedding.DataSnapshot
                {
                    Columns = [new("A", PulseOffice.Document.Embedding.DataValueKind.Text), new("B", PulseOffice.Document.Embedding.DataValueKind.Text)],
                    Rows = [["x", "y"]],
                },
                Layout = new PulseOffice.Document.Embedding.TableLayout { ColumnFractions = [0.25, 0.75], RowHeights = [30, 18] },
            },
        });

        var table = RoundTrip(deck).Slides[1].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.DataTableObject>().Single(t => t.Snapshot.Columns[0].Name == "A");

        Assert.Equal(0.25, table.Layout.ColumnFractions[0], 2);
        Assert.Equal(0.75, table.Layout.ColumnFractions[1], 2);
        Assert.Equal(30, table.Layout.RowHeights[0], 1);
        Assert.Equal(18, table.Layout.RowHeights[1], 1);
    }

    [Fact]
    public void LeMacroSiDichiaranoPerseInPptx()
    {
        var deck = Sample();
        deck.Macros.Store(new PulseOffice.Document.Forms.DocumentMacro("Riepilogo", PulseOffice.Document.Forms.MacroLanguage.JavaScript, "app.Log(1)"));
        Assert.Contains(new PptxWriter().CheckFidelity(deck).Losses, l => l.Code == "PPTX016");
    }

    [Fact]
    public void LaMappaImmagineSiDichiaraPersaInPptx()
    {
        var deck = Sample();
        var map = new PulseOffice.Document.Embedding.ImageMap();
        map.Add(new PulseOffice.Document.Embedding.ImageMapArea { Id = "a", Points = [(0, 0), (0.5, 0.5)], Target = "https://example.org" });
        deck.Slides[0].Add(new ObjectElement
        {
            Id = "img",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(3)),
            Content = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "img", Data = new byte[] { 1 }, MediaType = "image/png", Map = map },
        });
        Assert.Contains(new PptxWriter().CheckFidelity(deck).Losses, l => l.Code == "PPTX017");
    }

    [Fact]
    public void HeaderFooterIsWrittenAsNativePlaceholdersAndNotReadBackAsText()
    {
        var deck = Sample();
        deck.HeaderFooter = new HeaderFooterSettings { ShowFooter = true, FooterText = "Riservato", ShowSlideNumber = true, ShowOnTitleSlide = true };

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var package = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, false))
        {
            var types = package.PresentationPart!.SlideParts
                .SelectMany(part => part.Slide!.Descendants<DocumentFormat.OpenXml.Presentation.PlaceholderShape>())
                .Select(placeholder => placeholder.Type?.Value)
                .ToList();
            Assert.Contains(DocumentFormat.OpenXml.Presentation.PlaceholderValues.Footer, types);
            Assert.Contains(DocumentFormat.OpenXml.Presentation.PlaceholderValues.SlideNumber, types);
        }

        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;
        Assert.Equal(deck.Slides[1].Elements.Count, back.Slides[1].Elements.Count);
        Assert.Equal("Riservato", back.HeaderFooter.FooterText);
    }

    [Fact]
    public void TabellaSenzaIntestazioneSenzaBandeEConTestoSuPiuRigheSopravvive()
    {
        var deck = Sample();
        deck.Slides[1].Add(new ObjectElement
        {
            Id = "tbh",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(8), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Content = new PulseOffice.Document.Embedding.DataTableObject
            {
                Id = "tbh",
                Query = new PulseOffice.Document.Embedding.DataQuerySource { ConnectionId = "", Sql = "" },
                Style = new PulseOffice.Document.Embedding.DataTableStyle { HeaderRow = false, BandedRows = false },
                Snapshot = new PulseOffice.Document.Embedding.DataSnapshot
                {
                    Columns = [new("", PulseOffice.Document.Embedding.DataValueKind.Text), new("", PulseOffice.Document.Embedding.DataValueKind.Text), new("", PulseOffice.Document.Embedding.DataValueKind.Text)],
                    Rows = [["uno\ndue", "b", ""], ["c", "d", "e"]],
                },
            },
        });

        var table = RoundTrip(deck).Slides[1].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.DataTableObject>().Single();

        Assert.False(table.Style.HeaderRow);
        Assert.False(table.Style.BandedRows);
        Assert.Equal(3, table.Snapshot.Columns.Count);
        Assert.Equal(2, table.Snapshot.Rows.Count);
        Assert.Equal("uno\ndue", table.Snapshot.Rows[0][0]);
        Assert.Equal("e", table.Snapshot.Rows[1][2]);
    }
}
