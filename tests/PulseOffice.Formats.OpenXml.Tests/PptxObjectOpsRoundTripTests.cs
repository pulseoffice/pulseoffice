using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxObjectOpsRoundTripTests
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
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var result = new PptxReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static ObjectElement Shape(string id, int x, int y, int w, int h, bool flipV = false) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(x), new Twip(y), new Twip(w), new Twip(h)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle, FlipVertical = flipV },
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
    public void NotesAndHandoutMastersSurvive()
    {
        var (deck, slide) = Base();
        slide.Notes = "Prima riga\nSeconda riga";
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
        Assert.Equal("Prima riga\nSeconda riga", back.Slides[0].Notes);
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
        Assert.Equal(PulseOffice.Drawing.Shapes.BevelStyle.Round, shape.Style.Bevel.Style);
    }

    [Fact]
    public void MasterDisplayFlagsSurvive()
    {
        var (deck, slide) = Base();
        slide.ShowMasterObjects = false;
        

        var back = RoundTrip(deck).Slides[0];
        Assert.False(back.ShowMasterObjects);
        
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
        Assert.Equal(0.1, img.Crop.Left, tolerance: 0.001);
        Assert.Equal(0.2, img.Crop.Top, tolerance: 0.001);
        Assert.Equal(0.05, img.Crop.Right, tolerance: 0.001);
        Assert.Equal(0.15, img.Crop.Bottom, tolerance: 0.001);
    }

    [Fact]
    public void BackgroundImageSurvives()
    {
        var (deck, slide) = Base();
        slide.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = "bg", Data = new byte[] { 11, 22, 33, 44 }, MediaType = "image/png",
        };

        var back = RoundTrip(deck);
        var bg = back.Slides[0].BackgroundImage;
        Assert.NotNull(bg);
        Assert.Equal("image/png", bg!.MediaType);
        Assert.Equal(new byte[] { 11, 22, 33, 44 }, bg.Data.ToArray());
    }

    [Fact]
    public void CommentSurvives()
    {
        var (deck, slide) = Base();
        slide.AddComment(new SlideComment { Id = "cm1", Author = "Mario Rossi", Text = "Rivedere il titolo", X = new Twip(500), Y = new Twip(600) });

        var back = RoundTrip(deck);
        var comment = back.Slides[0].Comments.Single();
        Assert.Equal("Mario Rossi", comment.Author);
        Assert.Equal("Rivedere il titolo", comment.Text);
        Assert.Equal(500, comment.X.Value, tolerance: 20);
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
            Content = new PackagedObject
            {
                Id = "ole1",
                FileName = "Foglio1.ods",
                MediaType = "application/vnd.oasis.opendocument.spreadsheet",
                Data = new byte[] { 9, 8, 7, 6, 5 },
                Replacement = picture,
            },
        });

        var back = RoundTrip(deck);
        var package = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PackagedObject>().Single();
        Assert.Equal(picture, package.Replacement?.ToArray());
        Assert.Equal(new byte[] { 9, 8, 7, 6, 5 }, package.Data.ToArray());
    }

    [Fact]
    public void PackagedObjectSurvives()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "ole1",
            Bounds = new DocumentRect(new Twip(600), new Twip(700), new Twip(3000), new Twip(2000)),
            Content = new PackagedObject
            {
                Id = "ole1",
                FileName = "report.pdf",
                MediaType = "application/pdf",
                Data = new byte[] { 9, 8, 7, 6, 5 },
            },
        });

        var back = RoundTrip(deck);
        var package = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PackagedObject>().Single();
        Assert.Equal("report.pdf", package.FileName);
        Assert.Equal("application/pdf", package.MediaType);
        Assert.Equal(new byte[] { 9, 8, 7, 6, 5 }, package.Data.ToArray());
    }

    [Fact]
    public void FlipSurvives()
    {
        var (deck, slide) = Base();
        slide.Add(Shape("s1", 1000, 1000, 3000, 2000, flipV: true));

        var back = RoundTrip(deck);
        var shape = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<ShapeObject>().Single();
        Assert.True(shape.FlipVertical);
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
        Assert.True(freeform.Path.Figures[0].Nodes.Count >= 3);
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
            Style = new DataTableStyle { HeaderRow = false },
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
    public void NotesInAPlainTextBoxAreRead()
    {
        var (deck, slide) = Base();
        slide.Notes = "Ricordare i numeri";

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, true))
        {
            var notes = document.PresentationPart!.SlideParts.First().NotesSlidePart!.NotesSlide!;
            foreach (var shape in notes.Descendants<DocumentFormat.OpenXml.Presentation.Shape>().Where(s => s.TextBody is not null))
                shape.NonVisualShapeProperties!.ApplicationNonVisualDrawingProperties!.PlaceholderShape?.Remove();
            notes.Save();
        }

        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal("Ricordare i numeri", back.Slides[0].Notes);
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
    public void UnGruppoDiPowerPointRuotatoSiAprePosizionandoIFigli()
    {
        var (deck, slide) = Base();
        slide.Add(Shape("ga", 0, 0, 1000, 1000));
        slide.Add(Shape("gb", 3000, 0, 2000, 1000));

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        using (var package = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, true))
        {
            var tree = package.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            var shapes = tree.Elements<DocumentFormat.OpenXml.Presentation.Shape>()
                .Where(shape => shape.Descendants<DocumentFormat.OpenXml.Drawing.Transform2D>().Any()).ToList();
            var boxes = shapes.Select(shape => shape.Descendants<DocumentFormat.OpenXml.Drawing.Transform2D>().First()).ToList();
            var left = boxes.Min(b => b.Offset!.X!.Value);
            var top = boxes.Min(b => b.Offset!.Y!.Value);
            var right = boxes.Max(b => b.Offset!.X!.Value + b.Extents!.Cx!.Value);
            var bottom = boxes.Max(b => b.Offset!.Y!.Value + b.Extents!.Cy!.Value);
            var group = new DocumentFormat.OpenXml.Presentation.GroupShape(
                new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                    new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 900U, Name = "Gruppo" },
                    new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                    new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                new DocumentFormat.OpenXml.Presentation.GroupShapeProperties(
                    new DocumentFormat.OpenXml.Drawing.TransformGroup(
                        new DocumentFormat.OpenXml.Drawing.Offset { X = left, Y = top },
                        new DocumentFormat.OpenXml.Drawing.Extents { Cx = right - left, Cy = bottom - top },
                        new DocumentFormat.OpenXml.Drawing.ChildOffset { X = left, Y = top },
                        new DocumentFormat.OpenXml.Drawing.ChildExtents { Cx = right - left, Cy = bottom - top }) { Rotation = 180 * 60000 }));
            shapes[0].InsertBeforeSelf(group);
            foreach (var shape in shapes) { shape.Remove(); group.AppendChild(shape); }
            package.PresentationPart!.SlideParts.First().Slide!.Save();
        }

        buffer.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(buffer).Value!;
        var elements = read.Slides[0].Elements.OfType<ObjectElement>().Where(e => e.Content is ShapeObject)
            .OrderBy(e => e.Bounds.Left.Value).ToList();

        Assert.Equal(2, elements.Count);
        Assert.InRange(elements[0].Bounds.Width.Value, 1990, 2010);
        Assert.InRange(elements[0].Bounds.Left.Value, -10, 10);
        Assert.All(elements, element => Assert.Equal(180, element.RotationDegrees, 1));
    }

    [Fact]
    public void ImageDuotoneSurvivesInPptxAndIsDeclaredLostInOdp()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "img2",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Content = new PulseOffice.Drawing.Assets.RasterImageObject
            {
                Id = "img2", Data = new byte[] { 1, 2, 3, 4 }, MediaType = "image/png",
                Duotone = new PulseOffice.Drawing.Assets.ImageDuotone("#3B0764", "#F5D0FE"),
            },
        });
        slide.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = "bg", Data = new byte[] { 1, 2, 3, 4 }, MediaType = "image/png",
            Duotone = new PulseOffice.Drawing.Assets.ImageDuotone("#000000", "#22C55E"),
        };

        var back = RoundTrip(deck);
        var img = back.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<PulseOffice.Drawing.Assets.RasterImageObject>().Single();
        Assert.Equal(new PulseOffice.Drawing.Assets.ImageDuotone("#3B0764", "#F5D0FE"), img.Duotone);
        Assert.Equal(new PulseOffice.Drawing.Assets.ImageDuotone("#000000", "#22C55E"), back.Slides[0].BackgroundImage?.Duotone);
        Assert.Contains("ODP026", new PulseOffice.Formats.Odf.OdpWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
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
