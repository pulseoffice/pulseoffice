using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PresentationsSilentLossAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly byte[] _poster = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        slide.Add(new MediaElement
        {
            Id = "film",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(8), Twip.FromCentimeters(5)),
            Kind = MediaKind.Video,
            Data = [0, 0, 0, 24, 102, 116, 121, 112, 109, 112, 52, 50],
            MimeType = "video/mp4",
            FileName = "vacanze.mp4",
            PosterData = _poster,
        });
        deck.AddExisting(slide);
        return deck;
    }

    private Presentation ThroughOdp(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return new OdpReader(_themes, _masters).Read(stream).Value!;
    }

    private Presentation ThroughPptx(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return new PptxReader(_themes, _masters).Read(stream).Value!;
    }

    [Fact]
    public void NomeDelFileEFotogrammaDelFilmatoAttraversanoOdpEPptx()
    {
        foreach (var back in new[] { ThroughOdp(Deck()), ThroughPptx(Deck()) })
        {
            var media = back.Slides[0].Elements.OfType<MediaElement>().Single();
            Assert.Equal("vacanze.mp4", media.FileName);
            Assert.Equal(_poster, media.PosterData);
        }
    }

    [Fact]
    public void IlFotogrammaDelFilmatoNonRitornaComeImmagineSeparata()
    {
        foreach (var back in new[] { ThroughOdp(Deck()), ThroughPptx(Deck()), ThroughOdp(ThroughOdp(Deck())) })
            Assert.IsType<MediaElement>(Assert.Single(back.Slides[0].Elements));
    }

    [Fact]
    public void GliEffettiEsternoSenzaNomeCanonicoAttraversanoIDueFormatiODichiaranoLaPerdita()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "film", Category = AnimationCategory.Emphasis, Preset = "libo-physics-fall" });
        slide.AddAnimation(new AnimationEffect { Id = "a2", TargetId = "film", Category = AnimationCategory.Entrance, Preset = "ooo-entrance-breaks" });
        slide.AddAnimation(new AnimationEffect { Id = "a3", TargetId = "film", Category = AnimationCategory.Exit, Preset = "ooxml-51" });

        Assert.Equal(["libo-physics-fall", "ooo-entrance-breaks", "ooo-exit-magnify"], ThroughOdp(deck).Slides[0].Animations.Select(effect => effect.Preset));
        Assert.Equal(["ooxml-34", "ooxml-51"], ThroughPptx(deck).Slides[0].Animations.Skip(1).Select(effect => effect.Preset));
        Assert.Contains("PPTX032", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void UnaTransizioneDiImpressSenzaTipoNostroSopravviveInOdpEInPptxEDichiarata()
    {
        var deck = Deck();
        deck.Slides[0].Transition = new PulseOffice.Formats.SmilTransitionMapper().FromSmil("clockWipe", "clockwiseTwelve", false);

        Assert.Equal("clockWipe clockwiseTwelve", ThroughOdp(deck).Slides[0].Transition.CurrentNative?.Payload);
        Assert.Contains("PPTX033", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
        Assert.DoesNotContain("ODP023", new OdpWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void LeTransizioniClassicheDiPowerPointPassanoPerOdpEIndietroConLaTabellaEsterno()
    {
        var deck = Deck();
        deck.Slides[0].Transition = new PulseOffice.Formats.SmilTransitionMapper().FromSmil("blindsWipe", "vertical", false);

        var pptx = ThroughPptx(deck).Slides[0].Transition;
        Assert.Contains("blinds", pptx.CurrentNative?.Payload);
        Assert.Contains("dir=\"vert\"", pptx.CurrentNative?.Payload);

        var odp = ThroughOdp(ThroughPptx(deck)).Slides[0].Transition;
        Assert.Equal("blindsWipe vertical", odp.CurrentNative?.Payload);
        Assert.DoesNotContain("PPTX033", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void UnaTransizioneP14RestaInPptxEInOdpDiventaQuellaEsterno()
    {
        var deck = Deck();
        deck.Slides[0].Transition = new SlideTransition
        {
            Kind = TransitionKind.Fade,
            Native = new NativeTransition(NativeTransition.Ooxml,
                "<p14:vortex xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\" dir=\"r\" />", TransitionKind.Fade, TransitionDirection.None),
        };

        Assert.Contains("vortex", ThroughPptx(deck).Slides[0].Transition.CurrentNative?.Payload);
        Assert.Equal("miscShapeWipe vertical", ThroughOdp(deck).Slides[0].Transition.CurrentNative?.Payload);
        deck.Slides[0].Transition = deck.Slides[0].Transition with { Kind = TransitionKind.Push };
        Assert.Null(deck.Slides[0].Transition.CurrentNative);
    }

    [Fact]
    public void IConnettoriDiPowerPointNonSiPerdonoAllApertura()
    {
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), stream).Succeeded);
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(stream, true))
        {
            var tree = document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            tree.Append(new DocumentFormat.OpenXml.Presentation.ConnectionShape(
                new DocumentFormat.OpenXml.Presentation.NonVisualConnectionShapeProperties(
                    new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 90, Name = "Connettore 89" },
                    new DocumentFormat.OpenXml.Presentation.NonVisualConnectorShapeDrawingProperties(),
                    new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                    new DocumentFormat.OpenXml.Drawing.Transform2D(
                        new DocumentFormat.OpenXml.Drawing.Offset { X = 360045, Y = 360045 },
                        new DocumentFormat.OpenXml.Drawing.Extents { Cx = 1270000, Cy = 635000 }) { HorizontalFlip = true },
                    new DocumentFormat.OpenXml.Drawing.PresetGeometry(new DocumentFormat.OpenXml.Drawing.AdjustValueList())
                    {
                        Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.BentConnector3,
                    })));
        }

        stream.Position = 0;
        var slide = new PptxReader(_themes, _masters).Read(stream).Value!.Slides[0];

        var element = slide.Elements.OfType<ObjectElement>().Single(candidate => candidate.Content is PulseOffice.Drawing.Shapes.ConnectorObject);
        var connector = (PulseOffice.Drawing.Shapes.ConnectorObject)element.Content;
        Assert.Equal(PulseOffice.Drawing.Shapes.ConnectorKind.Elbow, connector.Shape);
        Assert.Equal(Twip.FromCentimeters(1), element.Bounds.Left);
        Assert.Equal(new PulseOffice.Drawing.Shapes.MoveSegment(new PulseOffice.Drawing.Shapes.ShapePoint(1, 0)), connector.Path.Figures[0].Segments[0]);
    }

    [Fact]
    public void IlComportamentoNativoDiUnEffettoAttraversaOdpEPptxNelProprioFormato()
    {
        var odp = ThroughOdp(Animated(new NativeAnimation(NativeAnimation.Odf,
            "<anim:par xmlns:anim=\"urn:oasis:names:tc:opendocument:xmlns:animation:1.0\" xmlns:smil=\"urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" presentation:preset-class=\"entrance\"><anim:animate smil:dur=\"1s\" smil:targetElement=\"vecchio\" smil:attributeName=\"x\" smil:values=\"0-width/2;x\" smil:keyTimes=\"0;1\"/></anim:par>",
            AnimationCategory.Entrance, "fly-in", "from-left", 1000, 1, null)));
        var pptx = ThroughPptx(Animated(new NativeAnimation(NativeAnimation.Ooxml,
            "<p:par xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cTn id=\"5\" presetID=\"2\" presetClass=\"entr\" presetSubtype=\"8\" fill=\"hold\" nodeType=\"clickEffect\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst><p:anim calcmode=\"lin\" valueType=\"num\"><p:cBhvr additive=\"base\"><p:cTn id=\"6\" dur=\"1000\" fill=\"hold\"/><p:tgtEl><p:spTgt spid=\"99\"/></p:tgtEl><p:attrNameLst><p:attrName>ppt_x</p:attrName></p:attrNameLst></p:cBhvr><p:tavLst><p:tav tm=\"0\"><p:val><p:strVal val=\"0-#ppt_w/2\"/></p:val></p:tav><p:tav tm=\"100000\"><p:val><p:strVal val=\"#ppt_x\"/></p:val></p:tav></p:tavLst></p:anim></p:childTnLst></p:cTn></p:par>",
            AnimationCategory.Entrance, "fly-in", "from-left", 1000, 1, null)));

        foreach (var effect in new[] { odp.Slides[0].Animations.Single(), pptx.Slides[0].Animations.Single() })
        {
            Assert.Equal("from-left", effect.Subtype);
            Assert.Contains(effect.CurrentNative!.Format == NativeAnimation.Odf ? "anim:animate" : "p:anim ", effect.CurrentNative.Xml);
            Assert.DoesNotContain("vecchio", effect.CurrentNative.Xml);
            Assert.DoesNotContain("spid=\"99\"", effect.CurrentNative.Xml);
            Assert.Null((effect with { DurationMs = 2000 }).CurrentNative);
        }
    }

    private Presentation Animated(NativeAnimation native)
    {
        var deck = Deck();
        deck.Slides[0].AddAnimation(new AnimationEffect
        {
            Id = "a1", TargetId = "film", Category = AnimationCategory.Entrance, Preset = "fly-in", Subtype = "from-left", DurationMs = 1000, Native = native,
        });
        return deck;
    }

    [Fact]
    public void UnEffettoSuUnGruppoDiPowerPointAnimaTuttiIMembri()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        foreach (var name in new[] { "q1", "q2" })
            slide.Add(new ObjectElement
            {
                Id = name,
                Bounds = new DocumentRect(Twip.FromCentimeters(10), Twip.FromCentimeters(1), Twip.FromCentimeters(2), Twip.FromCentimeters(2)),
                Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = name, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
            });
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "q1", Preset = "appear" });

        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(stream, true))
        {
            var part = document.PresentationPart!.SlideParts.First();
            var tree = part.Slide!.CommonSlideData!.ShapeTree!;
            var shapes = tree.Elements<DocumentFormat.OpenXml.Presentation.Shape>()
                .Where(shape => shape.ShapeProperties?.GetFirstChild<DocumentFormat.OpenXml.Drawing.PresetGeometry>() is not null).ToList();
            var group = new DocumentFormat.OpenXml.Presentation.GroupShape(
                new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                    new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 500, Name = "Gruppo" },
                    new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                    new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                new DocumentFormat.OpenXml.Presentation.GroupShapeProperties());
            foreach (var shape in shapes)
            {
                shape.Remove();
                group.Append(shape);
            }
            tree.Append(group);
            foreach (var target in part.Slide.Descendants<DocumentFormat.OpenXml.Presentation.ShapeTarget>())
                target.ShapeId = "500";
        }

        stream.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(stream).Value!.Slides[0];

        Assert.Equal(2, back.Animations.Count);
        Assert.Equal(AnimationTrigger.WithPrevious, back.Animations[1].Trigger);
        Assert.NotEqual(back.Animations[0].TargetId, back.Animations[1].TargetId);
    }

    [Fact]
    public void LaGrigliaEILeLineeGuidaAttraversanoOdpEPptxLeDichiara()
    {
        var deck = Deck();
        Assert.DoesNotContain("PPTX028", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
        Assert.DoesNotContain("ODP022", new OdpWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
        deck.Grid.ShowGrid = true;
        deck.Grid.SnapToObjectPoints = true;
        deck.Grid.Spacing = Twip.FromCentimeters(1);
        deck.Grid.AddGuide(new SlideGuide(true, 1440));

        var grid = ThroughOdp(deck).Grid;

        Assert.True(grid.ShowGrid);
        Assert.True(grid.SnapToObjectPoints);
        Assert.Equal(Twip.FromCentimeters(1), grid.Spacing);
        Assert.Equal([new SlideGuide(true, 1440)], grid.Guides);
        Assert.Contains("PPTX028", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void UnoSchemaConSfondoProprioNelLayoutEDichiaratoInOdp()
    {
        var deck = Deck();
        deck.Master = deck.Master with { Layouts = [.. deck.Master.Layouts.Select(layout => layout with { Background = PulseOffice.Text.Rich.ColorRole.Accent1 })] };

        Assert.Contains("ODP022", new OdpWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
    }
}
