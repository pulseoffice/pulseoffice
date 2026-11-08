using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class TextBackdropTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Sample(ShapeKind kind)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        var body = slide.Placeholder(PlaceholderRole.Body)!;
        slide.Replace(body with
        {
            Text = body.Text.WithPlainText("Testo"),
            Backdrop = new TextBackdrop(kind, ShapeFill.Default with { ColorHex = "#77BF3E" }, ShapeLine.Default with { ColorHex = "#112233", Dash = ShapeStrokeStyle.Solid }),
        });
        deck.AddExisting(slide);
        return deck;
    }

    private TextBackdrop? Backdrop(Presentation deck) => deck.Slides[0].Placeholder(PlaceholderRole.Body)!.Backdrop;

    [Fact]
    public void Lo_sfondo_dei_segnaposto_attraversa_PPTX_e_ODP()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Sample(ShapeKind.RoundedRectangle), pptx).Succeeded);
        pptx.Position = 0;
        var fromPptx = Backdrop(new PptxReader(_themes, _masters).Read(pptx).Value!)!;
        Assert.Equal(ShapeKind.RoundedRectangle, fromPptx.Shape);
        Assert.Equal("#77BF3E", fromPptx.Fill.ColorHex, ignoreCase: true);
        Assert.Equal("#112233", fromPptx.Line.ColorHex, ignoreCase: true);

        using var odp = new MemoryStream();
        Assert.Contains("ODP024", new OdpWriter().CheckFidelity(Sample(ShapeKind.RoundedRectangle)).Losses.Select(loss => loss.Code));
        Assert.DoesNotContain("ODP024", new OdpWriter().CheckFidelity(Sample(ShapeKind.Rectangle)).Losses.Select(loss => loss.Code));
        Assert.True(new OdpWriter().Write(Sample(ShapeKind.RoundedRectangle), odp).Succeeded);
        odp.Position = 0;
        var fromOdp = Backdrop(new OdpReader(_themes, _masters).Read(odp).Value!)!;
        Assert.Equal(ShapeKind.Rectangle, fromOdp.Shape);
        Assert.Equal("#77BF3E", fromOdp.Fill.ColorHex, ignoreCase: true);
        Assert.Equal("#112233", fromOdp.Line.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void I_segnaposto_senza_sfondo_restano_senza()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, pptx).Succeeded);
        pptx.Position = 0;
        Assert.Null(Backdrop(new PptxReader(_themes, _masters).Read(pptx).Value!));
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        odp.Position = 0;
        Assert.Null(Backdrop(new OdpReader(_themes, _masters).Read(odp).Value!));
    }

    [Fact]
    public void Una_casella_di_testo_riempita_resta_testo_con_il_suo_sfondo()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));
        using var stream = new MemoryStream();
        using (var buffer = new MemoryStream())
        {
            Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
            stream.Write(buffer.ToArray());
        }
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(stream, true))
        {
            var tree = document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!;
            tree.Append(new DocumentFormat.OpenXml.Presentation.Shape(
                "<p:sp xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">" +
                "<p:nvSpPr><p:cNvPr id=\"40\" name=\"TextBox 1\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr>" +
                "<p:spPr><a:xfrm><a:off x=\"100000\" y=\"100000\"/><a:ext cx=\"3000000\" cy=\"1000000\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val=\"FFCC00\"/></a:solidFill></p:spPr>" +
                "<p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"it-IT\" sz=\"2400\" b=\"1\"/><a:t>Riquadro</a:t></a:r></a:p></p:txBody></p:sp>"));
        }
        stream.Position = 0;

        var read = new PptxReader(_themes, _masters).Read(stream).Value!.Slides[0].Elements.OfType<TextElement>().Single(text => text.Text.PlainText == "Riquadro");

        Assert.Equal("#FFCC00", read.Backdrop!.Fill.ColorHex, ignoreCase: true);
        Assert.True(read.Text.Paragraphs[0].Runs.Count == 0 ? read.Text.Paragraphs[0].Bold : read.Text.Paragraphs[0].Runs[0].Bold);
    }

    [Fact]
    public void Un_riempimento_a_immagine_senza_modo_dichiarato_si_affianca_come_in_PowerPoint()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");
        slide.Add(new ObjectElement
        {
            Id = "s",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(PulseOffice.Foundation.Units.Twip.FromCentimeters(1), PulseOffice.Foundation.Units.Twip.FromCentimeters(1), PulseOffice.Foundation.Units.Twip.FromCentimeters(5), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Content = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Style = ShapeStyle.Default with { Fill = ShapeFill.Default with { Kind = ShapeFillKind.Bitmap, Bitmap = new ShapeBitmap { Data = png } } } },
        });
        deck.AddExisting(slide);
        using var stream = new MemoryStream();
        using (var buffer = new MemoryStream())
        {
            Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
            stream.Write(buffer.ToArray());
        }
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(stream, true))
            foreach (var stretch in document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<DocumentFormat.OpenXml.Drawing.Stretch>()).ToList())
                stretch.Remove();
        stream.Position = 0;

        var read = new PptxReader(_themes, _masters).Read(stream).Value!.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();

        Assert.True(read.Style.Fill.Bitmap!.Tiled);
    }
}
