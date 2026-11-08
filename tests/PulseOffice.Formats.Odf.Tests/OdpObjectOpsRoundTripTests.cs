using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpObjectOpsRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private (Presentation Deck, Slide Slide) Base()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private Presentation RoundTrip(Presentation deck)
    {
        var writer = new OdpWriter();
        using var buffer = new MemoryStream();
        Assert.True(writer.Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var result = new OdpReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static ObjectElement Shape(string id, int x, int y, int w, int h, bool flipH = false) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(x), new Twip(y), new Twip(w), new Twip(h)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle, FlipHorizontal = flipH },
    };

    [Fact]
    public void LinkedImageStaysLinked()
    {
        var file = Path.Combine(Path.GetTempPath(), $"qo-link-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(file, new byte[] { 9, 8, 7, 6 });
        try
        {
            var (deck, slide) = Base();
            slide.Add(new ObjectElement
            {
                Id = "lnk1",
                Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
                Content = new PulseOffice.Drawing.Assets.RasterImageObject
                {
                    Id = "lnk1", Data = new byte[] { 9, 8, 7, 6 }, MediaType = "image/png", LinkPath = file,
                },
            });

            var back = RoundTrip(deck);
            var image = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content)
                .OfType<PulseOffice.Drawing.Assets.RasterImageObject>().Single();
            Assert.Equal(Path.GetFullPath(file), image.LinkPath);
            Assert.Empty(image.Data.ToArray());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void FlipSurvives()
    {
        var (deck, slide) = Base();
        slide.Add(Shape("s1", 1000, 1000, 3000, 2000, flipH: true));

        var back = RoundTrip(deck);
        var shape = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<ShapeObject>().Single();
        Assert.True(shape.FlipHorizontal);
        Assert.False(shape.FlipVertical);
    }

    [Fact]
    public void GroupFlattensToChildren()
    {
        var (deck, slide) = Base();
        var group = new DeckGrouping().Group([Shape("a", 1000, 1000, 2000, 2000), Shape("b", 3000, 3000, 1000, 1000)], "g1")!;
        slide.Add(group);

        var back = RoundTrip(deck);
        var shapes = back.Slides[0].Elements.OfType<ObjectElement>()
            .Where(o => o.Content is ShapeObject).ToList();

        Assert.Equal(2, shapes.Count);
        Assert.Contains(shapes, s => Math.Abs(s.Bounds.Left.Value - 1000) < 30 && Math.Abs(s.Bounds.Width.Value - 2000) < 30);
        Assert.Contains(shapes, s => Math.Abs(s.Bounds.Left.Value - 3000) < 30 && Math.Abs(s.Bounds.Width.Value - 1000) < 30);
        Assert.DoesNotContain(back.Slides[0].Elements.OfType<ObjectElement>(), o => o.Content is ShapeGroupObject);
    }

    [Fact]
    public void FreeformSurvives()
    {
        var (deck, slide) = Base();
        var path = new FreeformPath([new PathFigure([
            new PathNode(new ShapePoint(0.1, 0.1)), new PathNode(new ShapePoint(0.9, 0.4)), new PathNode(new ShapePoint(0.4, 0.9))])]);
        slide.Add(new ObjectElement { Id = "f1", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)), Content = new FreeformObject { Id = "f1", Path = path } });

        var back = RoundTrip(deck);
        var freeform = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<FreeformObject>().Single();
        Assert.True(freeform.Path.Figures.Count > 0);
    }

    [Fact]
    public void FreeformStyleSurvives()
    {
        var (deck, slide) = Base();
        var path = new FreeformPath([new PathFigure([
            new PathNode(new ShapePoint(0.1, 0.1)), new PathNode(new ShapePoint(0.9, 0.4)), new PathNode(new ShapePoint(0.4, 0.9))]) { Closed = true }]) { Filled = true };
        var style = ShapeStyle.Default with
        {
            Fill = ShapeFill.Default with { ColorHex = "#16A34A" },
            Line = ShapeLine.Default with { ColorHex = "#DC2626", Width = Twip.FromPoints(3) },
        };
        slide.Add(new ObjectElement { Id = "f2", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)), Content = new FreeformObject { Id = "f2", Path = path, Style = style } });

        var back = RoundTrip(deck);
        var freeform = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<FreeformObject>().Single();
        Assert.Equal("#16A34A", freeform.Style.Fill.ColorHex, ignoreCase: true);
        Assert.Equal("#DC2626", freeform.Style.Line.ColorHex, ignoreCase: true);
        Assert.Equal(3, freeform.Style.Line.Width.ToPoints(), 1);
        Assert.True(freeform.Path.Filled);
    }

    [Fact]
    public void ConnectorSurvivesAsGeometry()
    {
        var (deck, slide) = Base();
        slide.Add(Shape("a", 0, 0, 1000, 1000));
        slide.Add(Shape("b", 5000, 3000, 1000, 1000));
        var connector = new DeckConnectors(new PulseOffice.Drawing.Shapes.ConnectorFramer()).Connect(slide, (ObjectElement)slide.Find("a")!, (ObjectElement)slide.Find("b")!, "c1", ConnectorKind.Elbow)!;
        slide.Add(connector);

        var back = RoundTrip(deck);
        Assert.Contains(back.Slides[0].Elements.OfType<ObjectElement>(), o => o.Content is FreeformObject);
    }

    [Fact]
    public void FormControlSurvives()
    {
        var (deck, slide) = Base();
        var catalog = new PulseOffice.Document.Forms.FormControlCatalog();
        slide.Add(new ObjectElement
        {
            Id = "ctl1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(2500), new Twip(700)),
            Content = new PulseOffice.Document.Forms.FormControlObject
            {
                Id = "ctl1",
                ControlKind = PulseOffice.Document.Forms.FormControlKind.CheckBox,
                Properties = catalog.Create(PulseOffice.Document.Forms.FormControlKind.CheckBox, "chk1"),
            },
        });

        var back = RoundTrip(deck);
        var control = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Forms.FormControlObject>().Single();
        Assert.Equal(PulseOffice.Document.Forms.FormControlKind.CheckBox, control.ControlKind);
    }

    [Fact]
    public void NotesAndHandoutMastersSurvive()
    {
        var (deck, slide) = Base();
        slide.Notes = "Prima riga";
        deck.NotesMaster = new NotesMaster
        {
            SlideBox = new PageBox(0.1, 0.1, 0.8, 0.3),
            NotesBox = new PageBox(0.1, 0.5, 0.8, 0.4),
            FontSize = 14,
            Header = "Riunione",
            Footer = "Riservato",
            PageNumber = false,
        };
        deck.Handout = new HandoutMaster { SlidesPerPage = 4, Header = "Corso", PageNumber = true };

        var back = RoundTrip(deck);

        Assert.Equal(0.3, back.NotesMaster.SlideBox.Height, 3);
        Assert.Equal(0.5, back.NotesMaster.NotesBox.Y, 3);
        Assert.Equal(14, back.NotesMaster.FontSize);
        Assert.Equal("Riunione", back.NotesMaster.Header);
        Assert.Equal("Riservato", back.NotesMaster.Footer);
        Assert.False(back.NotesMaster.PageNumber);
        Assert.Equal(4, back.Handout.SlidesPerPage);
        Assert.Equal("Corso", back.Handout.Header);
        Assert.True(back.Handout.PageNumber);
        Assert.Equal("Prima riga", back.Slides[0].Notes);
    }

    [Fact]
    public void BoundFieldAndDataSourceSurvive()
    {
        var (deck, slide) = Base();
        var catalog = new PulseOffice.Document.Forms.FormControlCatalog();
        deck.FormSettings = new PulseOffice.Document.Forms.FormDocumentSettings(DataSource: "shop", Command: "SELECT * FROM articoli");
        slide.Add(new ObjectElement
        {
            Id = "fld1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(2500), new Twip(700)),
            Content = new PulseOffice.Document.Forms.FormControlObject
            {
                Id = "fld1",
                ControlKind = PulseOffice.Document.Forms.FormControlKind.TextBox,
                Properties = catalog.Create(PulseOffice.Document.Forms.FormControlKind.TextBox, "nome") with { DataField = "nome" },
            },
        });

        var back = RoundTrip(deck);
        var control = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Forms.FormControlObject>().Single();
        Assert.Equal("nome", control.Properties.DataField);
        Assert.Equal("shop", back.FormSettings.DataSource);
        Assert.Equal("SELECT * FROM articoli", back.FormSettings.Command);
    }

    [Fact]
    public void PackagedObjectReplacementPictureSurvives()
    {
        var (deck, slide) = Base();
        var picture = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 42 };
        slide.Add(new ObjectElement
        {
            Id = "ole1",
            Bounds = new DocumentRect(new Twip(600), new Twip(700), new Twip(3000), new Twip(2000)),
            Content = new PulseOffice.Document.Embedding.PackagedObject
            {
                Id = "ole1",
                FileName = "Foglio1.ods",
                MediaType = "application/vnd.oasis.opendocument.spreadsheet",
                Data = new byte[] { 1, 2, 3, 4, 5, 6, 7 },
                Replacement = picture,
            },
        });

        var back = RoundTrip(deck);
        var package = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.PackagedObject>().Single();
        Assert.Equal(picture, package.Replacement?.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, package.Data.ToArray());
    }

    [Fact]
    public void PackagedObjectSurvives()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "ole1",
            Bounds = new DocumentRect(new Twip(600), new Twip(700), new Twip(3000), new Twip(2000)),
            Content = new PulseOffice.Document.Embedding.PackagedObject
            {
                Id = "ole1",
                FileName = "report.pdf",
                MediaType = "application/pdf",
                Data = new byte[] { 1, 2, 3, 4, 5, 6, 7 },
            },
        });

        var back = RoundTrip(deck);
        var package = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Document.Embedding.PackagedObject>().Single();
        Assert.Equal("report.pdf", package.FileName);
        Assert.Equal("application/pdf", package.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, package.Data.ToArray());
    }

    [Fact]
    public void AltTextSurvives()
    {
        var (deck, slide) = Base();
        var shape = Shape("s1", 1000, 1000, 3000, 2000);
        slide.Add(shape with { Content = shape.Content with { Title = "Logo", Description = "Il marchio dell'azienda" } });
        slide.Add(new ObjectElement
        {
            Id = "img1",
            Bounds = new DocumentRect(new Twip(5000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Content = new PulseOffice.Drawing.Assets.RasterImageObject
            {
                Id = "img1", Data = new byte[] { 1, 2, 3, 4 }, MediaType = "image/png",
                Title = "Grafico", Description = "Vendite per trimestre",
            },
        });

        var back = RoundTrip(deck);
        var contents = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).ToList();
        var readShape = contents.OfType<ShapeObject>().Single();
        Assert.Equal("Logo", readShape.Title);
        Assert.Equal("Il marchio dell'azienda", readShape.Description);
        var image = contents.OfType<PulseOffice.Drawing.Assets.RasterImageObject>().Single();
        Assert.Equal("Grafico", image.Title);
        Assert.Equal("Vendite per trimestre", image.Description);
    }

    [Fact]
    public void ExtrusionBevelAndNameSurvive()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "s1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject
            {
                Id = "s1", Name = "Logo", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle,
                Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with
                {
                    Extrusion = new PulseOffice.Drawing.Shapes.ShapeExtrusion { Kind = PulseOffice.Drawing.Shapes.ExtrusionKind.Extrude, Depth = Twip.FromCentimeters(2) },
                    Bevel = new PulseOffice.Drawing.Shapes.ShapeBevel { Style = PulseOffice.Drawing.Shapes.BevelStyle.Round, Depth = Twip.FromPoints(4) },
                },
            },
        });

        var back = RoundTrip(deck);
        var shape = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>().Single();
        Assert.Equal(PulseOffice.Drawing.Shapes.ExtrusionKind.Extrude, shape.Style.Extrusion.Kind);
        Assert.Equal(2, shape.Style.Extrusion.Depth.ToCentimeters(), 2);
        Assert.Equal("Logo", shape.Name);
    }

    [Fact]
    public void MasterDisplayFlagsSurvive()
    {
        var (deck, slide) = Base();
        slide.ShowMasterObjects = false;
        slide.ShowMasterBackground = false;

        var back = RoundTrip(deck).Slides[0];
        Assert.False(back.ShowMasterObjects);
        Assert.False(back.ShowMasterBackground);
    }

    [Fact]
    public void SecondMasterWithItsThemeAndElementsSurvives()
    {
        var (deck, slide) = Base();
        var themes = new ThemeCatalog();
        var sand = themes.Find("Sand")!;
        var second = new MasterFactory().CreateStandard(sand) with { Id = "master2", Name = "Secondo", Elements = new MasterElements(Date: false, Footer: true, SlideNumber: false) };
        deck.AddMaster(second);
        var other = new SlideFactory().Create(deck.NextId(), second, SlideLayout.TitleAndContent);
        other.MasterId = "master2";
        deck.AddExisting(other);

        var back = RoundTrip(deck);
        Assert.Equal(2, back.Masters.Count);
        Assert.Null(back.Slides[0].MasterId);
        var master = back.MasterOf(back.Slides[1]);
        Assert.Equal("Secondo", master.Name);
        Assert.Equal("Sand", master.Theme.Name);
        Assert.Equal(new MasterElements(false, true, false), master.Elements);
    }

    [Fact]
    public void GraphicStylesAndTheirLinksSurvive()
    {
        var (deck, slide) = Base();
        var red = PulseOffice.Drawing.Shapes.ShapeStyle.Default with { Fill = PulseOffice.Drawing.Shapes.ShapeFill.Default with { ColorHex = "#DC2626" } };
        deck.GraphicStyles.Set(new PulseOffice.Drawing.Shapes.GraphicStyle("Avviso", red));
        slide.Add(new ObjectElement
        {
            Id = "s1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle, Style = red, StyleName = "Avviso" },
        });

        var back = RoundTrip(deck);
        var style = Assert.Single(back.GraphicStyles.All);
        Assert.Equal("Avviso", style.Name);
        Assert.Equal("#DC2626", style.Style.Fill.ColorHex, ignoreCase: true);
        var shape = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>().Single();
        Assert.Equal("Avviso", shape.StyleName);
    }

    [Fact]
    public void A_freeform_keeps_its_link_to_a_graphic_style()
    {
        var (deck, slide) = Base();
        var green = ShapeStyle.Default with { Fill = ShapeFill.Default with { ColorHex = "#16A34A" } };
        deck.GraphicStyles.Set(new GraphicStyle("Évidence verte", green));
        var path = new FreeformPath([new PathFigure([
            new PathNode(new ShapePoint(0.1, 0.1)), new PathNode(new ShapePoint(0.9, 0.4)), new PathNode(new ShapePoint(0.4, 0.9))]) { Closed = true }]) { Filled = true };
        slide.Add(new ObjectElement { Id = "f9", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)), Content = new FreeformObject { Id = "f9", Path = path, Style = green, StyleName = "Évidence verte" } });

        var back = RoundTrip(deck);

        var freeform = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<FreeformObject>().Single();
        Assert.Equal("Évidence verte", freeform.StyleName);
    }

    [Fact]
    public void Master_and_layout_shapes_keep_their_link_to_a_graphic_style()
    {
        var (deck, _) = Base();
        var green = ShapeStyle.Default with { Fill = ShapeFill.Default with { ColorHex = "#16A34A" } };
        deck.GraphicStyles.Set(new GraphicStyle("Fond accentué", green));
        ObjectElement Decoration(string id) => new()
        {
            Id = id, Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(2000), new Twip(1000)),
            Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle, Style = green, StyleName = "Fond accentué" },
        };
        deck.Master = deck.Master with
        {
            Objects = [Decoration("m1")],
            Layouts = [.. deck.Master.Layouts.Select(layout => layout.Kind == SlideLayout.Blank ? layout with { Objects = [Decoration("l1")], HideMasterObjects = true } : layout)],
        };

        var back = RoundTrip(deck);

        Assert.Equal("Fond accentué", ((ShapeObject)((ObjectElement)back.Master.Objects[0]).Content).StyleName);
        var blank = back.Master.Template(SlideLayout.Blank);
        Assert.Equal("Fond accentué", ((ShapeObject)((ObjectElement)blank.Objects[0]).Content).StyleName);
    }

    [Fact]
    public void FontworkSurvives()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "fw1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(8000), new Twip(3000)),
            Content = new PulseOffice.Drawing.Fontwork.FontworkObject
            {
                Id = "fw1", Text = "Benvenuti", Warp = PulseOffice.Drawing.Fontwork.FontworkWarp.Wave,
                Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with { Fill = PulseOffice.Drawing.Shapes.ShapeFill.Default with { ColorHex = "#DC2626" } },
            },
        });

        var fontwork = RoundTrip(deck).Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<PulseOffice.Drawing.Fontwork.FontworkObject>().Single();
        Assert.Equal("Benvenuti", fontwork.Text);
        Assert.Equal(PulseOffice.Drawing.Fontwork.FontworkWarp.Wave, fontwork.Warp);
        Assert.Equal("#DC2626", fontwork.Style.Fill.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void ImageGrayscaleAndCropSurvive()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "img1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Content = new PulseOffice.Drawing.Assets.RasterImageObject
            {
                Id = "img1", Data = new byte[] { 1, 2, 3, 4 }, MediaType = "image/png",
                Filter = PulseOffice.Drawing.Assets.ImageFilter.Grayscale,
                Crop = (0.1, 0.2, 0.05, 0.15),
            },
        });

        var back = RoundTrip(deck);
        var img = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Drawing.Assets.RasterImageObject>().Single();
        Assert.Equal(PulseOffice.Drawing.Assets.ImageFilter.Grayscale, img.Filter);
        Assert.Equal(0.1, img.Crop.Left, tolerance: 0.01);
        Assert.Equal(0.2, img.Crop.Top, tolerance: 0.01);
        Assert.Equal(0.05, img.Crop.Right, tolerance: 0.01);
        Assert.Equal(0.15, img.Crop.Bottom, tolerance: 0.01);
    }

    [Fact]
    public void BackgroundImageSurvives()
    {
        var (deck, slide) = Base();
        slide.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = "bg", Data = new byte[] { 10, 20, 30, 40 }, MediaType = "image/png",
        };

        var back = RoundTrip(deck);
        var bg = back.Slides[0].BackgroundImage;
        Assert.NotNull(bg);
        Assert.Equal("image/png", bg!.MediaType);
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, bg.Data.ToArray());
    }

    [Fact]
    public void CommentSurvives()
    {
        var (deck, slide) = Base();
        slide.AddComment(new SlideComment { Id = "cm1", Author = "Mario", Text = "Rivedere il titolo", X = new Twip(500), Y = new Twip(600) });

        var back = RoundTrip(deck);
        var comment = back.Slides[0].Comments.Single();
        Assert.Equal("Mario", comment.Author);
        Assert.Equal("Rivedere il titolo", comment.Text);
        Assert.Equal(500, comment.X.Value, tolerance: 20);
    }

    [Fact]
    public void CellVerticalAlignSurvives()
    {
        var (deck, slide) = Base();
        var table = new DataTableObject
        {
            Id = "t1",
            Query = new DataQuerySource { ConnectionId = "", Sql = "" },
            Snapshot = new DataSnapshot
            {
                Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Text)],
                Rows = [["x", "y"], ["z", "w"]],
            },
            CellFormats = [new TableCellFormat(0, 1, CellVerticalAlign.Center), new TableCellFormat(1, 0, CellVerticalAlign.Bottom)],
        };
        slide.Add(new ObjectElement { Id = "t1", Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)), Content = table });

        var back = RoundTrip(deck);
        var formats = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<DataTableObject>().Single().CellFormats;

        Assert.Contains(formats, f => f.Row == 0 && f.Column == 1 && f.VerticalAlign == CellVerticalAlign.Center);
        Assert.Contains(formats, f => f.Row == 1 && f.Column == 0 && f.VerticalAlign == CellVerticalAlign.Bottom);
    }

    [Fact]
    public void ClickActionOnAShapeSurvives()
    {
        var (deck, slide) = Base();
        slide.Add(Shape("s1", 1000, 1000, 3000, 2000));
        slide.AddInteraction(new InteractiveAction { TargetId = "s1", Kind = InteractionKind.LastSlide });

        var back = RoundTrip(deck).Slides[0];
        var shape = back.Elements.OfType<ObjectElement>().Single();

        Assert.Equal(InteractionKind.LastSlide, back.Interaction(shape.Id)?.Kind);
    }

    [Fact]
    public void ThemesAreRecognisedWhenAnotherSuiteDropsOurAttribute()
    {
        var theme = _themes.Find("Slate")!;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme), Size = SlideSize.Widescreen };
        deck.AddExisting(_slides.Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        var second = _masters.CreateStandard(_themes.Find("Sand")!) with { Id = "master2", Name = "Secondo" };
        deck.AddMaster(second);
        var other = _slides.Create(deck.NextId(), second, SlideLayout.Blank);
        other.MasterId = "master2";
        deck.AddExisting(other);

        using var written = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, written).Succeeded);
        using var stripped = new MemoryStream();
        using (var source = new System.IO.Compression.ZipArchive(new MemoryStream(written.ToArray()), System.IO.Compression.ZipArchiveMode.Read))
        using (var target = new System.IO.Compression.ZipArchive(stripped, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                using var input = new StreamReader(entry.Open());
                var text = input.ReadToEnd();
                if (entry.FullName == "styles.xml")
                    text = System.Text.RegularExpressions.Regex.Replace(text.Replace("style:display-name=\"Ardesia\"", string.Empty), "qm:theme=\"[^\"]*\"", string.Empty);
                using var output = new StreamWriter(target.CreateEntry(entry.FullName).Open());
                output.Write(text);
            }

        stripped.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(stripped).Value!;
        Assert.Equal("Slate", back.Theme.Name);
        Assert.Equal("Sand", back.MasterOf(back.Slides[1]).Theme.Name);
    }

    [Fact]
    public void MediaIsWrittenAsStarMediaTypeAndKeepsItsType()
    {
        var (deck, slide) = Base();
        var aiff = new byte[] { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 4, (byte)'A', (byte)'I', (byte)'F', (byte)'F', 1, 2 };
        slide.Add(new MediaElement { Id = "snd", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(1000), new Twip(1000)), Kind = MediaKind.Audio, Data = aiff, MimeType = "audio/aiff" });

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var archive = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
            Assert.Contains("application/vnd.sun.star.media", new StreamReader(archive.GetEntry("content.xml")!.Open()).ReadToEnd());

        buffer.Position = 0;
        var media = new OdpReader(_themes, _masters).Read(buffer).Value!.Slides[0].Elements.OfType<MediaElement>().Single();
        Assert.Equal("audio/aiff", media.MimeType);
        Assert.Equal(MediaKind.Audio, media.Kind);
    }

    [Fact]
    public void NotesFontSizeIsAlsoWrittenInTheNamedNotesStyle()
    {
        var (deck, slide) = Base();
        slide.Notes = "Nota";
        deck.NotesMaster = deck.NotesMaster with { FontSize = 15 };

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        using var stripped = new MemoryStream();
        using (var source = new System.IO.Compression.ZipArchive(new MemoryStream(buffer.ToArray()), System.IO.Compression.ZipArchiveMode.Read))
        using (var target = new System.IO.Compression.ZipArchive(stripped, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                var text = new StreamReader(entry.Open()).ReadToEnd();
                if (entry.FullName == "styles.xml")
                {
                    Assert.Contains("-notes\"", text);
                    text = System.Text.RegularExpressions.Regex.Replace(text, "qm:font-size=\"[^\"]*\"", string.Empty);
                }
                using var output = new StreamWriter(target.CreateEntry(entry.FullName).Open());
                output.Write(text);
            }

        stripped.Position = 0;
        Assert.Equal(15, new OdpReader(_themes, _masters).Read(stripped).Value!.NotesMaster.FontSize);
    }

    [Fact]
    public void UnGruppoRuotatoSalvatoTieneLeRotazioniDeiFigli()
    {
        var (deck, slide) = Base();
        var group = new DeckGrouping().Group([Shape("ga", 0, 0, 1000, 1000) with { RotationDegrees = 30 }, Shape("gb", 3000, 0, 1000, 1000)], "grp")!
            with { RotationDegrees = 180 };
        slide.Add(group);

        var shapes = RoundTrip(deck).Slides[0].Elements.OfType<ObjectElement>().Where(e => e.Content is ShapeObject).ToList();

        Assert.Equal(2, shapes.Count);
        var left = shapes.OrderBy(e => e.Bounds.Left.Value).ToList();
        Assert.Equal(180, left[0].RotationDegrees, 1);
        Assert.Equal(210, left[1].RotationDegrees, 1);
    }

    [Fact]
    public void UnGruppoEsternoSiAprePosizionandoIFigli()
    {
        var (deck, slide) = Base();
        var rotated = Shape("ga", 1000, 1000, 1000, 1000) with { RotationDegrees = 45 };
        slide.Add(rotated);
        slide.Add(Shape("gb", 4000, 1000, 1000, 1000));

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        var changed = new MemoryStream();
        buffer.Position = 0;
        using (var source = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
        using (var target = new System.IO.Compression.ZipArchive(changed, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? System.IO.Compression.CompressionLevel.NoCompression : System.IO.Compression.CompressionLevel.Optimal);
                using var input = entry.Open();
                using var output = copy.Open();
                if (entry.FullName != "content.xml") { input.CopyTo(output); continue; }
                var document = System.Xml.Linq.XDocument.Load(input);
                var draw = (System.Xml.Linq.XNamespace)"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
                var page = document.Descendants(draw + "page").First();
                var shapes = page.Elements(draw + "custom-shape").ToList();
                foreach (var shape in shapes) shape.Remove();
                page.AddFirst(new System.Xml.Linq.XElement(draw + "g", shapes));
                document.Save(output);
            }

        changed.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(changed).Value!;
        var elements = read.Slides[0].Elements.OfType<ObjectElement>().Where(e => e.Content is ShapeObject).OrderBy(e => e.Bounds.Left.Value).ToList();

        Assert.Equal(2, elements.Count);
        Assert.Equal(45, elements[0].RotationDegrees, 1);
        Assert.InRange(elements[0].Bounds.Left.Value, 990, 1010);
    }

    [Fact]
    public void UnGruppoDellEditorRuotatoEIngranditoSiRiapreComeLoStessoGruppo()
    {
        var (deck, slide) = Base();
        var members = new[] { Shape("ga", 1000, 1000, 1000, 1000), Shape("gb", 3000, 1500, 2000, 1000) };
        foreach (var member in members) slide.Add(member);
        var editing = new SlideGroupEditing();
        slide.ReplaceGroups(editing.Group(slide, members, null, "grp")!);
        var group = Assert.Single(slide.Groups);
        var frame = editing.Frame(slide, group)!;
        var grown = new DocumentRect(frame.Bounds.Left, frame.Bounds.Top, new Twip(frame.Bounds.Width.Value * 2), new Twip(frame.Bounds.Height.Value * 2));
        var turned = editing.Transform(slide, group, grown, 90)!;
        foreach (var element in turned.Elements) slide.Replace(element);
        slide.ReplaceGroups(turned.Groups);
        var expected = editing.Frame(slide, slide.Groups[0])!;

        var back = RoundTrip(deck).Slides[0];

        var read = Assert.Single(back.Groups);
        Assert.Equal(2, read.Members.Count);
        var actual = editing.Frame(back, read)!;
        Assert.Equal(90, actual.RotationDegrees, 1);
        Assert.InRange(actual.Bounds.Width.Value, expected.Bounds.Width.Value - 20, expected.Bounds.Width.Value + 20);
        Assert.InRange(actual.Bounds.Height.Value, expected.Bounds.Height.Value - 20, expected.Bounds.Height.Value + 20);
        Assert.InRange(actual.Bounds.Left.Value, expected.Bounds.Left.Value - 20, expected.Bounds.Left.Value + 20);
        Assert.InRange(actual.Bounds.Top.Value, expected.Bounds.Top.Value - 20, expected.Bounds.Top.Value + 20);
    }
}
