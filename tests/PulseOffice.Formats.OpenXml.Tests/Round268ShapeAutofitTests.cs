using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round268ShapeAutofitTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck(TextAutofit fit)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        var shape = new ShapeObject
        {
            Id = "forma",
            Shape = ShapeKind.Rectangle,
            Text = "Testo",
            Style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Autofit = fit } },
        };
        slide.Add(new ObjectElement { Id = "forma", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)), Content = shape });
        deck.AddExisting(slide);
        return deck;
    }

    private TextAutofit Fit(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single().Style.Text.Autofit;

    [Theory]
    [InlineData(TextAutofit.None)]
    [InlineData(TextAutofit.ShrinkText)]
    [InlineData(TextAutofit.ResizeShape)]
    public void Shape_autofit_crosses_pptx_and_odp(TextAutofit fit)
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(fit), pptx).Succeeded);
        pptx.Position = 0;
        var fromPptx = new PptxReader(_themes, _masters).Read(pptx).Value!;
        Assert.Equal(fit, Fit(fromPptx));

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(fromPptx, odp).Succeeded);
        odp.Position = 0;
        Assert.Equal(fit, Fit(new OdpReader(_themes, _masters).Read(odp).Value!));
    }

    [Fact]
    public void Shape_body_writes_the_autofit_element_powerpoint_reads()
    {
        var mapper = new XlsxShapeStyleMapper();

        Assert.NotNull(mapper.Body(ShapeTextStyle.Default with { Autofit = TextAutofit.ShrinkText }).GetFirstChild<DocumentFormat.OpenXml.Drawing.NormalAutoFit>());
        Assert.NotNull(mapper.Body(ShapeTextStyle.Default with { Autofit = TextAutofit.ResizeShape }).GetFirstChild<DocumentFormat.OpenXml.Drawing.ShapeAutoFit>());
    }
}
