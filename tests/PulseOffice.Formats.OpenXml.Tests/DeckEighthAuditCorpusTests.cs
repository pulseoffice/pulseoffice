using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckEighthAuditCorpusTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        return deck;
    }

    private MemoryStream Pptx(Presentation deck)
    {
        var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return buffer;
    }

    private Presentation Read(Stream stream)
    {
        stream.Position = 0;
        return new PptxReader(_themes, _masters).Read(stream).Value!;
    }

    private P.Shape CustomShape(uint id, string name, string? text)
    {
        var path = new D.Path(new D.MoveTo(new D.Point { X = "0", Y = "0" }), new D.LineTo(new D.Point { X = "100", Y = "0" }),
            new D.LineTo(new D.Point { X = "50", Y = "100" }), new D.CloseShapePath()) { Width = 100, Height = 100 };
        var properties = new P.ShapeProperties(
            new D.Transform2D(new D.Offset { X = 914400, Y = 914400 }, new D.Extents { Cx = 914400, Cy = 914400 }),
            new D.CustomGeometry(new D.AdjustValueList(), new D.ShapeGuideList(), new D.AdjustHandleList(), new D.ConnectionSiteList(),
                new D.Rectangle { Left = "l", Top = "t", Right = "r", Bottom = "b" }, new D.PathList(path)),
            new D.SolidFill(new D.RgbColorModelHex { Val = "FF0000" }));
        var body = new P.TextBody(new D.BodyProperties(), new D.ListStyle(),
            text is null ? new D.Paragraph() : new D.Paragraph(new D.Run(new D.Text(text))));
        return new P.Shape(new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = name },
            new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()), properties, body);
    }

    [Fact]
    public void Le_forme_a_geometria_personalizzata_di_PowerPoint_non_si_perdono_in_lettura()
    {
        using var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var tree = document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            tree.Append(CustomShape(40, "Freeform 1", null));
            tree.Append(CustomShape(41, "Freeform 2", "Testo"));
        }

        var slide = Read(stream).Slides[0];

        Assert.Contains(slide.Elements.OfType<ObjectElement>(), element => element.Content is FreeformObject);
        Assert.Contains(slide.Elements.OfType<TextElement>(), element => element.Text.Paragraphs.Any(paragraph => paragraph.Text == "Testo"));
    }

    private Presentation Linked()
    {
        var deck = Deck();
        deck.AddExisting(_slides.Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        deck.Slides[0].Add(new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(4)),
            Text = new RichText(PlaceholderRole.Free,
                [new TextParagraph("Sito e avanti").WithRuns([
                    new TextRun("Sito") { Hyperlink = "https://example.org/a" },
                    new TextRun(" e "),
                    new TextRun("avanti") { Hyperlink = "#" + SlideNames.PageName(deck.Slides[1]) }])]),
        });
        return deck;
    }

    private IReadOnlyList<string?> Links(Presentation deck) =>
        [.. deck.Slides[0].Elements.OfType<TextElement>().SelectMany(element => element.Text.Paragraphs).SelectMany(p => p.Runs).Select(run => run.Hyperlink)];

    [Fact]
    public void I_collegamenti_nel_testo_fanno_il_giro_in_PPTX()
    {
        var deck = Linked();
        var again = Read(Pptx(deck));

        Assert.Equal(["https://example.org/a", null, "#" + SlideNames.PageName(again.Slides[1])], Links(again));
    }

    [Theory]
    [InlineData("ppaction://hlinkfile")]
    [InlineData("ppaction://hlinkpres?slideindex=1&slidetitle=")]
    public void I_collegamenti_a_file_e_presentazioni_nel_testo_si_leggono(string action)
    {
        using var stream = Pptx(Linked());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var link = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.HyperlinkOnClick>())
                .First(candidate => string.IsNullOrEmpty(candidate.Action?.Value));
            link.Action = action;
        }

        Assert.Contains("https://example.org/a", Links(Read(stream)));
    }

    [Fact]
    public void I_collegamenti_nel_testo_delle_forme_si_leggono_e_si_riscrivono()
    {
        using var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var slide = document.PresentationPart!.SlideParts.First();
            var link = slide.AddHyperlinkRelationship(new Uri("https://example.org/"), true);
            var properties = new P.ShapeProperties(
                new D.Transform2D(new D.Offset { X = 914400, Y = 914400 }, new D.Extents { Cx = 914400, Cy = 914400 }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Ellipse });
            var run = new D.Run(new D.RunProperties(new D.HyperlinkOnClick { Id = link.Id }), new D.Text("sito"));
            slide.Slide!.CommonSlideData!.ShapeTree!.Append(new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = 50, Name = "Ovale" }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                properties, new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph(run))));
        }

        stream.Position = 0;
        var deck = Read(stream);
        PulseOffice.Drawing.Shapes.ShapeObject Shape(PulseOffice.Presentations.Presentation source) =>
            source.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>().Single(shape => shape.Runs is not null);
        Assert.Equal("https://example.org/", Shape(deck).Runs!.Single().Link);
        Assert.Equal("https://example.org/", Shape(Read(Pptx(deck))).Runs!.Single().Link);
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        odp.Position = 0;
        Assert.Equal("https://example.org/", Shape(new OdpReader(_themes, _masters).Read(odp).Value!).Runs!.Single().Link);
    }

    [Fact]
    public void Una_forma_senza_stile_ne_bordo_ne_riempimento_resta_senza()
    {
        using var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var properties = new P.ShapeProperties(
                new D.Transform2D(new D.Offset { X = 914400, Y = 914400 }, new D.Extents { Cx = 914400, Cy = 914400 }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Ellipse },
                new D.SolidFill(new D.RgbColorModelHex { Val = "FF0000" }));
            document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!.Append(new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = 70, Name = "Ovale" }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                properties, new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph())));
        }

        var shape = Read(stream).Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>().Single();
        Assert.Equal(PulseOffice.Drawing.Shapes.ShapeStrokeStyle.None, shape.Style.Line.Dash);
        Assert.Equal("#FF0000", shape.Style.Fill.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void Le_forme_in_un_contenuto_alternativo_si_leggono_dalla_riserva()
    {
        using var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var chosen = CustomShape(80, "Scelta", "scelta");
            chosen.TextBody!.GetFirstChild<D.Paragraph>()!.Append(new DocumentFormat.OpenXml.OpenXmlUnknownElement("a14", "m", "http://schemas.microsoft.com/office/drawing/2010/main"));
            var alternate = new DocumentFormat.OpenXml.AlternateContent(
                new DocumentFormat.OpenXml.AlternateContentChoice(chosen) { Requires = "a14" },
                new DocumentFormat.OpenXml.AlternateContentFallback(CustomShape(81, "Riserva", "riserva")));
            document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!.Append(alternate);
        }

        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "PPTX183");
        var slide = read.Value!.Slides[0];
        Assert.Contains(slide.Elements.OfType<TextElement>(), element => element.Text.PlainText == "riserva");
        Assert.DoesNotContain(slide.Elements.OfType<TextElement>(), element => element.Text.PlainText == "scelta");
    }

    [Fact]
    public void Le_geometrie_predefinite_dello_standard_si_disegnano_senza_dichiarazione()
    {
        using var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var properties = new P.ShapeProperties(
                new D.Transform2D(new D.Offset { X = 914400, Y = 914400 }, new D.Extents { Cx = 914400, Cy = 914400 }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Round2DiagonalRectangle },
                new D.SolidFill(new D.RgbColorModelHex { Val = "FF0000" }));
            document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!.Append(new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = 90, Name = "Diagonale" }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                properties, new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph())));
        }

        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "PPTX184");
        Assert.Contains(read.Value!.Slides[0].Elements.OfType<ObjectElement>(), element =>
            element.Content is PulseOffice.Drawing.Shapes.ShapeObject { Computed: PulseOffice.Drawing.Shapes.Geometry.PresetGeometry { Name: "round2DiagRect" } });
        Assert.DoesNotContain(new PptxReader(_themes, _masters).Read(Pptx(Linked())).Diagnostics, diagnostic => diagnostic.Code == "PPTX184");
    }

    [Fact]
    public void Le_forme_nascoste_restano_nascoste_nei_due_formati()
    {
        using var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var shape = CustomShape(60, "Nascosta", null);
            shape.NonVisualShapeProperties!.NonVisualDrawingProperties!.Hidden = true;
            document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!.Append(shape);
        }

        stream.Position = 0;
        var deck = Read(stream);
        Assert.Single(deck.Slides[0].Elements, element => element.Hidden);
        Assert.Single(Read(Pptx(deck)).Slides[0].Elements, element => element.Hidden);
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        odp.Position = 0;
        Assert.Single(new OdpReader(_themes, _masters).Read(odp).Value!.Slides[0].Elements, element => element.Hidden);
    }

    [Fact]
    public void I_suoni_incorporati_fanno_il_giro_nei_due_formati()
    {
        var deck = Linked();
        byte[] wave = [.. "RIFF$\0\0\0WAVEfmt "u8.ToArray(), 1, 2, 3, 4];
        deck.Sounds.Add(new EmbeddedSound("Media/campana.wav", "audio/wav", wave));
        var slide = deck.Slides[0];
        slide.Transition = slide.Transition with { Kind = TransitionKind.Fade, SoundPath = "Media/campana.wav", LoopSoundUntilNext = true };
        var target = slide.Elements.First();
        slide.SetInteraction(new InteractiveAction { TargetId = target.Id, Kind = InteractionKind.PlaySound, SoundPath = "Media/campana.wav" });

        Assert.DoesNotContain(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code is "PPTX003" or "PPTX006");
        var pptx = Read(Pptx(deck));
        void Check(PulseOffice.Presentations.Presentation back)
        {
            var path = back.Slides[0].Transition.SoundPath;
            Assert.Equal(wave, back.Sounds.Find(path)!.Data);
            Assert.True(back.Slides[0].Transition.LoopSoundUntilNext);
            var click = back.Slides[0].Interactions.Single(action => action.SoundPath is not null);
            Assert.Equal(wave, back.Sounds.Find(click.SoundPath)!.Data);
        }
        Check(pptx);
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(pptx, odp).Succeeded);
        odp.Position = 0;
        Check(new OdpReader(_themes, _masters).Read(odp).Value!);
    }

    [Fact]
    public void I_collegamenti_nel_testo_fanno_il_giro_in_ODP()
    {
        var deck = Linked();
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var again = new OdpReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal(["https://example.org/a", null, "#" + SlideNames.PageName(again.Slides[1])], Links(again));
    }

    [Fact]
    public void I_gruppi_letti_da_PPTX_restano_gruppi_in_ODP_e_tornano_gruppi_in_PPTX()
    {
        using var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var tree = document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            tree.Append(new P.GroupShape(
                new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 50, Name = "Gruppo esterno" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new D.TransformGroup(new D.Offset { X = 0, Y = 0 }, new D.Extents { Cx = 914400, Cy = 914400 }, new D.ChildOffset { X = 0, Y = 0 }, new D.ChildExtents { Cx = 914400, Cy = 914400 })),
                CustomShape(51, "Freeform 1", null),
                new P.GroupShape(
                    new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 52, Name = "Gruppo interno" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new D.TransformGroup(new D.Offset { X = 0, Y = 0 }, new D.Extents { Cx = 914400, Cy = 914400 }, new D.ChildOffset { X = 0, Y = 0 }, new D.ChildExtents { Cx = 914400, Cy = 914400 })),
                    CustomShape(53, "Freeform 2", null),
                    CustomShape(54, "Freeform 3", "testo"))));
        }
        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "PPTX044");
        var slide = read.Value!.Slides[0];
        Assert.Equal(2, slide.Groups.Count);
        var outer = slide.Groups.Single(group => group.Name == "Gruppo esterno");
        var inner = slide.Groups.Single(group => group.Name == "Gruppo interno");
        Assert.Equal(2, outer.Members.Count);
        Assert.Contains(inner.Id, outer.Members);
        Assert.Equal(2, inner.Members.Count);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(read.Value!, odp).Succeeded);
        Assert.DoesNotContain(new OdpWriter().CheckFidelity(read.Value!).Losses, diagnostic => diagnostic.Code == "ODP014");
        odp.Position = 0;
        var content = OdpContent(odp);
        var groups = content.Descendants(PulseOffice.Formats.Packaging.OdfPackage.Namespaces.Draw + "g").ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[0].Elements().Count());
        Assert.Same(groups[0], groups[1].Parent);

        odp.Position = 0;
        var fromOdp = new OdpReader(_themes, _masters).Read(odp);
        Assert.DoesNotContain(fromOdp.Diagnostics, diagnostic => diagnostic.Code == "ODF095");
        Assert.Equal(2, fromOdp.Value!.Slides[0].Groups.Count);
        Assert.Contains(fromOdp.Value!.Slides[0].Groups, group => group.Name == "Gruppo interno" && group.Members.Count == 2);

        using var back = Pptx(fromOdp.Value!);
        using var written = PresentationDocument.Open(back, false);
        var shapes = written.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
        var outerShape = Assert.Single(shapes.Elements<P.GroupShape>());
        Assert.Equal("Gruppo esterno", outerShape.NonVisualGroupShapeProperties!.NonVisualDrawingProperties!.Name!.Value);
        var innerShape = Assert.Single(outerShape.Elements<P.GroupShape>());
        Assert.Equal(2, innerShape.Elements<P.Shape>().Count());
        Assert.Single(outerShape.Elements<P.Shape>());
    }

    [Fact]
    public void Un_gruppo_fatto_in_Deck_si_scrive_come_gruppo_in_PPTX_e_ODP()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        ObjectElement Box(string id, int left) => new()
        {
            Id = id,
            Bounds = new DocumentRect(new Twip(left), new Twip(1000), new Twip(1000), new Twip(1000)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
        };
        slide.Add(new DeckGrouping().Group([Box("a", 1000), Box("b", 3000)], "g1")!);

        Assert.DoesNotContain(new PptxWriter().CheckFidelity(deck).Losses, diagnostic => diagnostic.Code == "PPTX009");
        using var pptx = Pptx(deck);
        using (var written = PresentationDocument.Open(pptx, false))
        {
            var group = Assert.Single(written.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!.Elements<P.GroupShape>());
            Assert.Equal(2, group.Elements<P.Shape>().Count());
        }
        Assert.Single(Read(pptx).Slides[0].Groups);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        odp.Position = 0;
        var odpGroup = Assert.Single(OdpContent(odp).Descendants(PulseOffice.Formats.Packaging.OdfPackage.Namespaces.Draw + "g"));
        Assert.Equal(2, odpGroup.Elements().Count());
    }

    private System.Xml.Linq.XDocument OdpContent(Stream odp)
    {
        using var archive = new System.IO.Compression.ZipArchive(odp, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
        using var reader = archive.GetEntry("content.xml")!.Open();
        return System.Xml.Linq.XDocument.Load(reader);
    }

    [Fact]
    public void L_azione_vai_a_diapositiva_fa_il_giro_in_PPTX()
    {
        var deck = Linked();
        var target = SlideNames.PageName(deck.Slides[1]);
        deck.Slides[0].AddInteraction(new InteractiveAction { TargetId = "box", Kind = InteractionKind.GoToSlide, Reference = target });

        Assert.DoesNotContain(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code == "PPTX003");
        var again = Read(Pptx(deck));
        var action = Assert.Single(again.Slides[0].Interactions);

        Assert.Equal(InteractionKind.GoToSlide, action.Kind);
        Assert.Equal(SlideNames.PageName(again.Slides[1]), action.Reference);
    }

    private Presentation Animated()
    {
        var deck = Deck();
        deck.Slides[0].Add(new TextElement
        {
            Id = "list",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(6)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("Uno"), new TextParagraph("Due"), new TextParagraph("Tre")]),
        });
        for (var index = 0; index < 3; index++)
            deck.Slides[0].AddAnimation(new AnimationEffect { Id = $"a{index}", TargetId = "list", Paragraph = index, Preset = "appear" });
        return deck;
    }

    private IReadOnlyList<int?> Paragraphs(Presentation deck) => [.. deck.Slides[0].Animations.Select(effect => effect.Paragraph)];

    [Fact]
    public void Le_animazioni_per_paragrafo_fanno_il_giro_in_PPTX_e_ODP()
    {
        var throughPptx = Read(Pptx(Animated()));
        Assert.Equal([0, 1, 2], Paragraphs(throughPptx));

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Animated(), odp).Succeeded);
        odp.Position = 0;
        var throughOdp = new OdpReader(_themes, _masters).Read(odp).Value!;
        Assert.Equal([0, 1, 2], Paragraphs(throughOdp));

        Assert.Equal([0, 1, 2], Paragraphs(Read(Pptx(throughOdp))));
    }

    private Presentation WithVideo()
    {
        var deck = Deck();
        deck.Slides[0].Add(new MediaElement
        {
            Id = "film",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(8), Twip.FromCentimeters(5)),
            Kind = MediaKind.Video,
            Data = [0, 0, 0, 24, 102, 116, 121, 112],
            MimeType = "video/mp4",
        });
        deck.Slides[0].AddAnimation(new AnimationEffect { Id = "play", TargetId = "film", Category = AnimationCategory.Media, Preset = "play", Trigger = AnimationTrigger.AfterPrevious, DurationMs = 4000 });
        return deck;
    }

    private void IsPlay(Presentation deck)
    {
        var effect = Assert.Single(deck.Slides[0].Animations);
        Assert.Equal(AnimationCategory.Media, effect.Category);
        Assert.Equal("play", effect.Preset);
        Assert.Equal(AnimationTrigger.AfterPrevious, effect.Trigger);
        Assert.IsType<MediaElement>(deck.Slides[0].Elements.Single(element => element.Id == effect.TargetId));
    }

    [Fact]
    public void L_avvio_dei_filmati_fa_il_giro_in_PPTX_e_ODP()
    {
        var throughPptx = Read(Pptx(WithVideo()));
        IsPlay(throughPptx);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(throughPptx, odp).Succeeded);
        odp.Position = 0;
        var throughOdp = new OdpReader(_themes, _masters).Read(odp).Value!;
        IsPlay(throughOdp);
        IsPlay(Read(Pptx(throughOdp)));
    }

    [Fact]
    public void Le_formule_di_riferimento_in_ODP_si_leggono_come_formule()
    {
        var deck = Deck();
        deck.Slides[0].Add(new ObjectElement
        {
            Id = "f1",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(4), Twip.FromCentimeters(2)),
            Content = new PulseOffice.Document.Embedding.FormulaObject { Id = "f1", Expression = "a=a" },
        });
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        using (var archive = new System.IO.Compression.ZipArchive(odp, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            var stripped = System.Text.RegularExpressions.Regex.Replace(content, "<text:p>a=a</text:p>", string.Empty);
            Assert.NotEqual(content, stripped);
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(stripped);
        }
        odp.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(odp).Value!;

        var formula = Assert.Single(read.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<PulseOffice.Document.Embedding.FormulaObject>());
        Assert.Equal("a=a", formula.Expression);
    }

    [Fact]
    public void Free_text_boxes_of_ODP_masters_are_read_as_master_shapes()
    {
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), odp).Succeeded);
        using (var archive = new System.IO.Compression.ZipArchive(odp, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("styles.xml")!;
            string styles;
            using (var reader = new StreamReader(entry.Open())) styles = reader.ReadToEnd();
            var at = styles.IndexOf("<style:master-page ", StringComparison.Ordinal);
            var close = styles.IndexOf('>', at) + 1;
            styles = styles[..close] + "<draw:frame svg:x=\"1cm\" svg:y=\"1cm\" svg:width=\"4cm\" svg:height=\"1cm\"><draw:text-box><text:p>Bozza</text:p></draw:text-box></draw:frame>" + styles[close..];
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("styles.xml").Open());
            writer.Write(styles);
        }
        odp.Position = 0;

        var read = new OdpReader(_themes, _masters).Read(odp);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF090");
        var shape = Assert.Single(read.Value!.Masters[0].Objects.OfType<ObjectElement>().Select(element => element.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>());
        Assert.Equal("Bozza", shape.Text);
        var pptx = Read(Pptx(read.Value));
        Assert.Contains(pptx.Masters[0].Objects.OfType<ObjectElement>().Select(element => element.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>(), item => item.Text == "Bozza");
    }

    [Fact]
    public void Due_SmartArt_con_lo_stesso_gruppo_non_impediscono_la_lettura()
    {
        const string Path = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/tdf131553.pptx";
        Assert.SkipWhen(!File.Exists(Path), "Reference corpus not available.");
        using var stream = File.OpenRead(Path);

        Assert.True(new PptxReader(_themes, _masters).Read(stream).Succeeded);
    }
}
