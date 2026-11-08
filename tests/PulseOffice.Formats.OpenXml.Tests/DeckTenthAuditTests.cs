using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckTenthAuditTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/";

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

    private MemoryStream Odp(Presentation deck)
    {
        var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return buffer;
    }

    private Presentation FromPptx(Stream stream)
    {
        stream.Position = 0;
        return new PptxReader(_themes, _masters).Read(stream).Value!;
    }

    private Presentation FromOdp(Stream stream)
    {
        stream.Position = 0;
        return new OdpReader(_themes, _masters).Read(stream).Value!;
    }

    private Presentation Corpus_(string name)
    {
        using var stream = File.OpenRead(Corpus + name);
        return new PptxReader(_themes, _masters).Read(stream).Value!;
    }

    private P.Shape Shape(uint id, string name, long x, long y, string? rot = null, string? text = null)
    {
        var transform = new D.Transform2D(new D.Offset { X = x, Y = y }, new D.Extents { Cx = 914400, Cy = 457200 });
        if (rot is not null) transform.Rotation = int.Parse(rot, System.Globalization.CultureInfo.InvariantCulture);
        return new P.Shape(
            new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = name }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(transform, new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Ellipse }, new D.SolidFill(new D.RgbColorModelHex { Val = "00FF00" })),
            new P.TextBody(new D.BodyProperties(), new D.ListStyle(), text is null ? new D.Paragraph() : new D.Paragraph(new D.Run(new D.Text(text)))));
    }

    private MemoryStream WithRotatedGroup(int rotation)
    {
        var stream = Pptx(Deck());
        using (var document = PresentationDocument.Open(stream, true))
        {
            var tree = document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            var frame = new D.TransformGroup(new D.Offset { X = 914400, Y = 914400 }, new D.Extents { Cx = 2743200, Cy = 457200 },
                new D.ChildOffset { X = 914400, Y = 914400 }, new D.ChildExtents { Cx = 2743200, Cy = 457200 }) { Rotation = rotation };
            tree.Append(new P.GroupShape(
                new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 60, Name = "Girato" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(frame),
                Shape(61, "Ellisse A", 914400, 914400),
                Shape(62, "Ellisse B", 2743200, 914400)));
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Un_clic_su_un_membro_di_un_gruppo_letto_seleziona_tutto_il_gruppo_e_dentro_il_gruppo_aperto_il_membro()
    {
        var slide = FromPptx(WithRotatedGroup(0)).Slides[0];
        var units = new SlideGroupUnits();
        var group = Assert.Single(slide.Groups);
        var member = slide.Find(group.Members[0])!;

        var unit = units.Unit(slide, member, null);
        Assert.Equal(2, unit.Count);
        Assert.Same(group, units.Matching(slide, unit));
        Assert.Equal([member], units.Unit(slide, member, group.Id));
    }

    [Fact]
    public void I_gruppi_annidati_si_selezionano_dall_esterno_e_un_livello_per_volta()
    {
        var slide = _slides.Create("s", _masters.CreateStandard(_themes.Default), SlideLayout.Blank);
        foreach (var id in new[] { "a", "b", "c" })
            slide.Add(new ObjectElement { Id = id, Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle } });
        slide.AddGroup(new SlideGroup("inner", ["a", "b"]));
        slide.AddGroup(new SlideGroup("outer", ["inner", "c"]));
        var units = new SlideGroupUnits();

        Assert.Equal(3, units.Unit(slide, slide.Find("a")!, null).Count);
        Assert.Equal(["a", "b"], units.Unit(slide, slide.Find("a")!, "outer").Select(element => element.Id));
        Assert.Equal(["c"], units.Unit(slide, slide.Find("c")!, "outer").Select(element => element.Id));
        Assert.Equal(["a", "b", "c"], units.Leaves(slide, slide.Groups[1]));
    }

    [Fact]
    public void Il_gruppo_fatto_nell_editor_torna_unita_dopo_salvataggio_e_riapertura()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var grouping = new DeckGrouping();
        var group = grouping.Group([
            new ObjectElement { Id = "x", Bounds = new PulseOffice.Foundation.Units.DocumentRect(new(0), new(0), new(1440), new(1440)), Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "x", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle } },
            new ObjectElement { Id = "y", Bounds = new PulseOffice.Foundation.Units.DocumentRect(new(2880), new(0), new(1440), new(1440)), Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "y", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse } },
        ], "g")!;
        slide.Add(group);

        foreach (var reopened in new[] { FromPptx(Pptx(deck)), FromOdp(Odp(deck)) })
        {
            var again = reopened.Slides[0];
            var leaf = again.Elements.OfType<ObjectElement>().First(element => again.Groups.Any(candidate => candidate.Members.Contains(element.Id)));
            Assert.Equal(2, new SlideGroupUnits().Unit(again, leaf, null).Count);
        }
    }

    [Fact]
    public void La_rotazione_propria_di_un_gruppo_PPTX_si_disegna_si_conserva_in_PPTX_e_in_ODP()
    {
        var read = FromPptx(WithRotatedGroup(90 * 60000));
        var slide = read.Slides[0];
        var group = Assert.Single(slide.Groups);
        Assert.Equal(90, group.RotationDegrees, 3);
        var members = group.Members.Select(id => slide.Find(id)!).ToList();
        Assert.All(members, member => Assert.Equal(90, member.RotationDegrees, 3));
        Assert.InRange(members[0].Bounds.Left.Value - members[1].Bounds.Left.Value, -2, 2);

        var framed = new SlideGroupRotation().InGroupFrames(slide);
        Assert.Equal(2, framed.Count);
        Assert.All(framed.Values, value => Assert.Equal(0, value.RotationDegrees, 3));

        using var pptx = Pptx(read);
        using (var document = PresentationDocument.Open(pptx, false))
        {
            var written = document.PresentationPart!.SlideParts.First().Slide!.Descendants<P.GroupShape>().Single();
            Assert.Equal(90 * 60000, written.GroupShapeProperties!.TransformGroup!.Rotation!.Value);
            var children = written.Elements<P.Shape>().Select(shape => shape.ShapeProperties!.Transform2D!).ToList();
            Assert.True(children.All(child => child.Rotation is null), written.OuterXml);
            Assert.InRange(children[0].Offset!.Y!.Value - children[1].Offset!.Y!.Value, -2000, 2000);
            Assert.InRange(written.GroupShapeProperties.TransformGroup.Offset!.X!.Value, 912400, 916400);
            Assert.InRange(written.GroupShapeProperties.TransformGroup.Offset.Y!.Value, 912400, 916400);
        }

        var back = FromPptx(pptx).Slides[0];
        Assert.Equal(90, Assert.Single(back.Groups).RotationDegrees, 3);
        Assert.Equal(members.Select(member => member.Bounds), back.Groups[0].Members.Select(id => back.Find(id)!.Bounds));

        var fromOdp = FromOdp(Odp(read)).Slides[0];
        Assert.Equal(90, Assert.Single(fromOdp.Groups).RotationDegrees, 3);
        Assert.All(fromOdp.Groups[0].Members.Select(id => fromOdp.Find(id)!), member => Assert.Equal(90, member.RotationDegrees, 3));
    }

    [Fact]
    public void Le_forme_custGeom_con_testo_di_group_pptx_restano_tracciati_con_testo_bianco_da_fontRef()
    {
        Assert.SkipWhen(!File.Exists(Corpus + "group.pptx"), "Reference corpus not available.");
        var deck = Corpus_("group.pptx");
        var outlined = deck.Slides.SelectMany(slide => slide.Elements).OfType<TextElement>().Where(text => text.Backdrop?.Outline is not null).ToList();
        Assert.NotEmpty(outlined);
        var values = outlined.First(text => text.Text.Paragraphs.Any(paragraph => paragraph.Text.Contains("Values", StringComparison.Ordinal)));
        Assert.Equal(PulseOffice.Text.Rich.ColorRole.Background, values.Text.Paragraphs[0].Runs[0].ColorRole ?? values.Text.Paragraphs[0].ColorRole);

        using var pptx = Pptx(deck);
        using (var document = PresentationDocument.Open(pptx, false))
        {
            var shapes = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<P.Shape>()).ToList();
            Assert.True(shapes.Any(shape => shape.ShapeProperties?.GetFirstChild<D.CustomGeometry>() is not null && shape.TextBody?.InnerText.Contains("Values", StringComparison.Ordinal) == true),
                string.Join("\n", shapes.Where(shape => shape.TextBody?.InnerText.Contains("Values", StringComparison.Ordinal) == true).Select(shape => shape.OuterXml[..Math.Min(1500, shape.OuterXml.Length)])));
        }
        Assert.Contains(FromPptx(pptx).Slides.SelectMany(slide => slide.Elements).OfType<TextElement>(), text => text.Backdrop?.Outline is not null);

        var odp = Odp(deck);
        using (var archive = new System.IO.Compression.ZipArchive(odp, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
        using (var reader = new StreamReader(archive.GetEntry("content.xml")!.Open()))
        {
            var xml = reader.ReadToEnd();
            var at = xml.IndexOf("pulseoffice-text-outline", StringComparison.Ordinal);
            Assert.True(at >= 0, xml[..Math.Min(xml.Length, 4000)]);
        }
        Assert.Contains(FromOdp(odp).Slides.SelectMany(slide => slide.Elements).OfType<TextElement>(),
            text => text.Backdrop?.Outline is { } outline && outline.Figures.Count > 0 && text.Text.Paragraphs.Any(paragraph => paragraph.Text.Contains("Values", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("tdf169088.pptx")]
    [InlineData("tdf169694.pptx")]
    public void Le_animazioni_degli_oggetti_OLE_attraversano_PPTX_e_ODP(string name)
    {
        Assert.SkipWhen(!File.Exists(Corpus + name), "Reference corpus not available.");
        var deck = Corpus_(name);
        var effect = Assert.Single(deck.Slides[0].Animations);
        Assert.NotNull(deck.Slides[0].Find(effect.TargetId));

        using var pptx = Pptx(deck);
        using (var document = PresentationDocument.Open(pptx, false))
        {
            var slide = document.PresentationPart!.SlideParts.First().Slide!;
            var frame = slide.CommonSlideData!.ShapeTree!.Elements<P.GraphicFrame>().Single();
            var frameId = frame.NonVisualGraphicFrameProperties!.NonVisualDrawingProperties!.Id!.Value;
            Assert.All(frame.Descendants<P.NonVisualDrawingProperties>().Skip(1), inner => Assert.NotEqual(frameId, inner.Id!.Value));
            var targets = slide.Descendants<P.ShapeTarget>().ToList();
            Assert.NotEmpty(targets);
            Assert.All(targets, target => Assert.Equal(frameId.ToString(System.Globalization.CultureInfo.InvariantCulture), target.ShapeId!.Value));
        }

        var again = Assert.Single(FromPptx(pptx).Slides[0].Animations);
        Assert.Equal((effect.Category, effect.Preset, effect.DurationMs, effect.Trigger), (again.Category, again.Preset, again.DurationMs, again.Trigger));

        var fromOdp = Assert.Single(FromOdp(Odp(deck)).Slides[0].Animations);
        Assert.Equal((effect.Category, effect.Preset, effect.Trigger), (fromOdp.Category, fromOdp.Preset, fromOdp.Trigger));
    }
}
