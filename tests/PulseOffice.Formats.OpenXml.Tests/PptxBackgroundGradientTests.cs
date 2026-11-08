using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxBackgroundGradientTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private readonly ShapeFill _gradient = ShapeFill.Default with
    {
        Kind = ShapeFillKind.Gradient,
        Gradient = ShapeGradient.Default with { AngleDegrees = 45, Stops = [new("#112233", 0), new("#AABBCC", 1)] },
    };

    private Presentation Sample()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme) with { BackgroundFill = _gradient };
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var first = _slides.Create(deck.NextId(), master, SlideLayout.TitleOnly);
        first.BackgroundFill = _gradient with { Gradient = _gradient.Gradient with { Stops = [new("#FF0000", 0), new("#0000FF", 1)] } };
        deck.AddExisting(first);
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleOnly));
        return deck;
    }

    private byte[] Write(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private Presentation Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new PptxReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void GliSfondiAGradienteDiDiapositivaESchemaAttraversanoPPTX()
    {
        var back = Read(Write(Sample()));

        Assert.Equal("#FF0000", back.Slides[0].BackgroundFill!.Gradient.StartHex);
        Assert.Equal(45, back.Slides[0].BackgroundFill!.Gradient.AngleDegrees, 1);
        Assert.Null(back.Slides[1].BackgroundFill);
        Assert.Equal("#AABBCC", back.Master.BackgroundFill!.Gradient.EndHex);
    }

    [Fact]
    public void LoSfondoDalTemaConIlColoreSegnapostoDiventaUnGradiente()
    {
        var bytes = Write(Sample());
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes);
            using (var document = PresentationDocument.Open(stream, true))
            {
                var master = document.PresentationPart!.SlideMasterParts.First();
                var theme = master.ThemePart!.Theme!;
                theme.ThemeElements!.FormatScheme!.BackgroundFillStyleList = new D.BackgroundFillStyleList(
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                    new D.GradientFill(new D.GradientStopList(
                        new D.GradientStop(new D.SchemeColor(new D.Tint { Val = 50000 }) { Val = D.SchemeColorValues.PhColor }) { Position = 0 },
                        new D.GradientStop(new D.SchemeColor(new D.Shade { Val = 50000 }) { Val = D.SchemeColorValues.PhColor }) { Position = 100000 }),
                        new D.LinearGradientFill { Angle = 5400000 }),
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }));
                var slide = document.PresentationPart.SlideParts.First(part => part.Slide!.CommonSlideData!.Background is null);
                slide.Slide!.CommonSlideData!.InsertAt(new P.Background(new P.BackgroundStyleReference(new D.RgbColorModelHex { Val = "808080" }) { Index = 1002 }), 0);
            }
            bytes = stream.ToArray();
        }

        var fill = Read(bytes).Slides.Single(slide => slide.BackgroundFill?.Gradient.StartHex is "#C0C0C0").BackgroundFill!;

        Assert.Equal("#404040", fill.Gradient.EndHex);
        Assert.Equal(90, fill.Gradient.AngleDegrees, 1);
    }

    [Fact]
    public void LeFormeConStileDelTemaPrendonoRiempimentoEBordoDalTemaConIColoriDelloSchema()
    {
        var deck = Sample();
        deck.Slides[1].Add(new ObjectElement
        {
            Id = "s1",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.FromCentimeters(2), PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
                PulseOffice.Foundation.Units.Twip.FromCentimeters(6), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Ellipse, Style = ShapeStyle.Default },
        });
        var bytes = Write(deck);
        string accent;
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes);
            using (var document = PresentationDocument.Open(stream, true))
            {
                var theme = document.PresentationPart!.SlideMasterParts.First().ThemePart!.Theme!;
                accent = theme.ThemeElements!.ColorScheme!.Accent1Color!.RgbColorModelHex!.Val!.Value!;
                var format = theme.ThemeElements.FormatScheme!;
                format.FillStyleList = new D.FillStyleList(
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }));
                format.LineStyleList = new D.LineStyleList(
                    new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })) { Width = 12700 },
                    new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })) { Width = 25400 },
                    new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })) { Width = 38100 });
                var shape = document.PresentationPart.SlideParts.SelectMany(part => part.Slide!.Descendants<P.Shape>())
                    .Single(candidate => candidate.ShapeProperties?.GetFirstChild<D.PresetGeometry>()?.Preset?.Value == D.ShapeTypeValues.Ellipse);
                foreach (var child in shape.ShapeProperties!.ChildElements.Where(child => child is D.SolidFill or D.GradientFill or D.Outline or D.NoFill).ToList()) child.Remove();
                shape.ShapeProperties.Append(new D.Outline { Width = 6350 });
                shape.ShapeStyle?.Remove();
                shape.InsertAfter(new P.ShapeStyle(
                    new D.LineReference(new D.SchemeColor(new D.Shade { Val = 50000 }) { Val = D.SchemeColorValues.Accent1 }) { Index = 2 },
                    new D.FillReference(new D.SchemeColor { Val = D.SchemeColorValues.Accent1 }) { Index = 1 },
                    new D.EffectReference(new D.SchemeColor { Val = D.SchemeColorValues.Accent1 }) { Index = 0 },
                    new D.FontReference(new D.SchemeColor { Val = D.SchemeColorValues.Light1 }) { Index = D.FontCollectionIndexValues.Minor }), shape.ShapeProperties);
            }
            bytes = stream.ToArray();
        }

        var read = Read(bytes).Slides[1].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();

        Assert.Equal(ShapeFillKind.Solid, read.Style.Fill.Kind);
        Assert.Equal("#" + accent, read.Style.Fill.ColorHex, ignoreCase: true);
        Assert.NotEqual(read.Style.Fill.ColorHex, read.Style.Line.ColorHex, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0.5, read.Style.Line.Width.ToPoints(), 1);
    }

    [Fact]
    public void Il_colore_di_sfondo_dello_schema_tiene_i_modificatori_di_luminosita()
    {
        var bytes = Write(Sample());
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes);
            using (var document = PresentationDocument.Open(stream, true))
            {
                var master = document.PresentationPart!.SlideMasterParts.First();
                var data = master.SlideMaster!.CommonSlideData!;
                data.Background = new P.Background(new P.BackgroundProperties(
                    new D.SolidFill(new D.RgbColorModelHex(new D.LuminanceModulation { Val = 60000 }, new D.LuminanceOffset { Val = 40000 }) { Val = "70AD47" }),
                    new D.EffectList()));
            }
            bytes = stream.ToArray();
        }

        var colour = Read(bytes).Master.BackgroundColor!.Value & 0xFFFFFF;

        Assert.InRange((int)(colour >> 16), 0xA0, 0xB0);
        Assert.InRange((int)((colour >> 8) & 0xFF), 0xC8, 0xD8);
    }
}
