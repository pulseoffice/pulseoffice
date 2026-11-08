using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

[Collection(nameof(OpenXmlLargeDocumentPerformanceTests))]
public sealed class PptxDeepAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(Action<Slide> fill)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "s1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)),
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Ellipse },
        });
        fill(slide);
        deck.AddExisting(slide);
        return deck;
    }

    private byte[] Write(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private Presentation Read(byte[] package) => new PptxReader(_themes, _masters).Read(new MemoryStream(package)).Value!;

    private byte[] Edit(byte[] package, Action<PresentationPart> change)
    {
        var buffer = new MemoryStream();
        buffer.Write(package);
        using (var document = PresentationDocument.Open(buffer, true)) change(document.PresentationPart!);
        return buffer.ToArray();
    }

    [Theory]
    [InlineData("http://example.org:99999/x")]
    [InlineData("http://[::1/x")]
    public void Un_collegamento_illeggibile_non_impedisce_il_salvataggio(string reference)
    {
        var deck = Deck(slide => slide.AddInteraction(new InteractiveAction { TargetId = "s1", Kind = InteractionKind.Hyperlink, Reference = reference }));

        var back = Read(Write(deck));

        Assert.NotNull(back.Slides[0].Interaction(back.Slides[0].Elements[0].Id)?.Reference);
    }

    [Theory]
    [InlineData("http://example.org:99999/clip.mp4")]
    [InlineData("http://[::1/clip.mp4")]
    public void Un_media_collegato_illeggibile_non_impedisce_il_salvataggio(string link)
    {
        var deck = Deck(slide => slide.Add(new MediaElement
        {
            Id = "v1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Kind = MediaKind.Video, Data = [], LinkedPath = link, MimeType = "video/mp4",
        }));

        Assert.NotNull(Read(Write(deck)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 100)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void Una_dimensione_di_diapositiva_fuori_dai_limiti_resta_nei_limiti(int cx, int cy)
    {
        var package = Edit(Write(Deck(_ => { })), presentation =>
            presentation.Presentation!.SlideSize = new P.SlideSize { Cx = cx, Cy = cy });

        var size = Read(package).Size;

        Assert.InRange(size.Width.Value, 1440, 80640);
        Assert.InRange(size.Height.Value, 1440, 80640);
    }

    [Fact]
    public void Ritardo_e_durata_delle_animazioni_hanno_un_tetto()
    {
        var deck = Deck(slide => slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "s1", DurationMs = 600, DelayMs = 100, Preset = "appear" }));
        var package = Edit(Write(deck), presentation =>
        {
            foreach (var condition in presentation.SlideParts.First().Slide!.Descendants<P.Condition>().Where(c => c.Delay?.Value == "100"))
                condition.Delay = int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });

        var effect = Assert.Single(Read(package).Slides[0].Animations);

        Assert.InRange(effect.DelayMs, 0, 3_600_000);
    }

    [Fact]
    public void Un_carattere_incorporato_illeggibile_si_dichiara()
    {
        var package = Edit(Write(Deck(_ => { })), presentation =>
        {
            var font = presentation.AddFontPart(FontPartType.FontData);
            using (var data = new MemoryStream(new byte[200])) font.FeedData(data);
            presentation.Presentation!.Append(new P.EmbeddedFontList(new P.EmbeddedFont(new P.Font { Typeface = "Rotto" }, new P.RegularFont { Id = presentation.GetIdOfPart(font) })));
        });

        var result = new PptxReader(_themes, _masters).Read(new MemoryStream(package));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PPTX220");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void Il_corpo_delle_note_resta_nei_limiti(int hundredths)
    {
        var package = Edit(Write(Deck(slide => slide.Notes = "Nota")), presentation =>
            presentation.NotesMasterPart!.NotesMaster!.NotesStyle!.GetFirstChild<DocumentFormat.OpenXml.Drawing.Level1ParagraphProperties>()!
                .GetFirstChild<DocumentFormat.OpenXml.Drawing.DefaultRunProperties>()!.FontSize = hundredths);

        Assert.InRange(Read(package).NotesMaster.FontSize, 1, 4000);
    }

    [Fact]
    public void Una_rotazione_enorme_si_riduce_al_giro()
    {
        var deck = Deck(_ => { });
        var slide = deck.Slides[0];
        slide.Replace(slide.Elements[0] with { RotationDegrees = 7.2e17 });

        Assert.Equal(0, Read(Write(deck)).Slides[0].Elements[0].RotationDegrees, 3);
    }

    [Fact]
    public void Molti_gruppi_e_contenuti_alternativi_si_sciolgono_in_tempo_lineare()
    {
        const int count = 20_000;
        var tree = new StringBuilder("<p:spTree xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\">");
        for (var index = 0; index < count; index++)
            tree.Append($"<p:grpSp><p:nvGrpSpPr><p:cNvPr id=\"{index + 2}\" name=\"g\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/><p:sp><p:nvSpPr><p:cNvPr id=\"{count + index + 2}\" name=\"s\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr/></p:sp><mc:AlternateContent><mc:Choice Requires=\"x\"><p:sp/></mc:Choice><mc:Fallback><p:sp/></mc:Fallback></mc:AlternateContent></p:grpSp>");
        tree.Append("</p:spTree>");
        var package = Edit(Write(Deck(_ => { })), presentation =>
        {
            var shapes = presentation.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            var source = new P.ShapeTree(tree.ToString());
            var children = source.ChildElements.ToList();
            source.RemoveAllChildren();
            shapes.Append(children);
        });

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var deck = Read(package);
        clock.Stop();

        Assert.NotEmpty(deck.Slides[0].Elements);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"{clock.Elapsed}");
    }

    [Fact]
    public void Un_riferimento_al_riempimento_di_sfondo_del_tema_si_applica()
    {
        var package = Edit(Write(Deck(_ => { })), presentation =>
        {
            var shape = presentation.SlideParts.First().Slide!.Descendants<P.Shape>().First();
            foreach (var fill in shape.ShapeProperties!.ChildElements.Where(child => child is DocumentFormat.OpenXml.Drawing.SolidFill or DocumentFormat.OpenXml.Drawing.NoFill).ToList()) fill.Remove();
            shape.ShapeStyle?.Remove();
            shape.InsertAfter(new P.ShapeStyle(
                new DocumentFormat.OpenXml.Drawing.LineReference(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex { Val = "000000" }) { Index = 0 },
                new DocumentFormat.OpenXml.Drawing.FillReference(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex { Val = "FF0000" }) { Index = 1001 },
                new DocumentFormat.OpenXml.Drawing.EffectReference(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex { Val = "000000" }) { Index = 0 },
                new DocumentFormat.OpenXml.Drawing.FontReference(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex { Val = "000000" }) { Index = DocumentFormat.OpenXml.Drawing.FontCollectionIndexValues.Minor }), shape.ShapeProperties);
        });

        var element = Assert.IsType<ObjectElement>(Read(package).Slides[0].Elements[0]);
        var fill = Assert.IsType<ShapeObject>(element.Content).Style.Fill;

        Assert.Equal(ShapeFillKind.Solid, fill.Kind);
        Assert.Equal("#FF0000", fill.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void Molte_forme_si_leggono_in_tempo_lineare()
    {
        const int count = 40_000;
        var tree = new StringBuilder("<p:spTree xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">");
        for (var index = 0; index < count; index++)
            tree.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{index + 10}\" name=\"s\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"100\" cy=\"100\"/></a:xfrm><a:prstGeom prst=\"ellipse\"/><a:solidFill><a:srgbClr val=\"FF0000\"/></a:solidFill></p:spPr></p:sp>");
        tree.Append("</p:spTree>");
        var package = Edit(Write(Deck(_ => { })), presentation =>
        {
            var shapes = presentation.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            var source = new P.ShapeTree(tree.ToString());
            var children = source.ChildElements.ToList();
            source.RemoveAllChildren();
            shapes.Append(children);
        });

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var deck = Read(package);
        clock.Stop();

        Assert.True(deck.Slides[0].Elements.Count > count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"{clock.Elapsed}");
    }

    [Fact]
    public void Un_colore_della_penna_non_esadecimale_non_rende_il_file_illeggibile()
    {
        var deck = Deck(_ => { });
        deck.Setup = deck.Setup with { PenColor = "#GGHHII" };

        using var document = PresentationDocument.Open(new MemoryStream(Write(deck)), false);
        var pen = document.PresentationPart!.PresentationPropertiesPart!.PresentationProperties!.Descendants<DocumentFormat.OpenXml.Drawing.RgbColorModelHex>().Single();

        Assert.Matches("^[0-9A-F]{6}$", pen.Val!.Value!);
    }

    [Fact]
    public void Uno_SmartArt_con_dati_illeggibili_e_testo_modificato_si_salva_e_lo_dichiara()
    {
        var deck = Deck(_ => { });
        var slide = deck.Slides[0];
        var member = (ObjectElement)slide.Elements[0] with { Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle, Text = "Prima" } };
        slide.Replace(member);
        const string frame = "<p:graphicFrame xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><p:nvGraphicFramePr><p:cNvPr id=\"5\" name=\"Diagram\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr><p:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"100\" cy=\"100\"/></p:xfrm><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/diagram\"><dgm:relIds xmlns:dgm=\"http://schemas.openxmlformats.org/drawingml/2006/diagram\" r:dm=\"\" r:lo=\"\" r:qs=\"\" r:cs=\"\"/></a:graphicData></a:graphic></p:graphicFrame>";
        slide.AddDiagram(new NativeDiagram([member], frame, [new NativeDiagramPart("dm", "application/vnd.openxmlformats-officedocument.drawingml.diagramData+xml", "<rotto"u8.ToArray())])
        {
            PointsByMember = new Dictionary<string, string> { ["s1"] = "{1}" },
        });
        slide.Replace(member with { Content = ((ShapeObject)member.Content) with { Text = "Dopo" } });

        using var buffer = new MemoryStream();
        var result = new PptxWriter().Write(deck, buffer);

        Assert.True(result.Succeeded);
        Assert.Contains(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code == "PPTX221");
    }
}
