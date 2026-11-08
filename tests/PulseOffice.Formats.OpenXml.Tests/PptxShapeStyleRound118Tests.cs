using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxShapeStyleRound118Tests
{
    private static readonly DocumentRect Box = new(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(5), Twip.FromCentimeters(4));
    private static readonly ShapeStyle Elliptical = ShapeStyle.Default with
    {
        Fill = ShapeFill.Default with { Kind = ShapeFillKind.Gradient, Gradient = ShapeGradient.Default with { Kind = GradientKind.Elliptical } },
    };

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private (Presentation Deck, Slide Slide) Deck()
    {
        var theme = _themes.Default;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private static FreeformPath Triangle() => new([new PathFigure([
        new PathNode(new ShapePoint(0.1, 0.1)), new PathNode(new ShapePoint(0.9, 0.4)), new PathNode(new ShapePoint(0.4, 0.9))]) { Closed = true }]) { Filled = true };

    [Fact]
    public void A_gradient_lost_on_a_freeform_is_declared()
    {
        var (deck, slide) = Deck();
        slide.Add(new ObjectElement { Id = "f", Bounds = Box, Content = new FreeformObject { Id = "f", Path = Triangle(), Style = Elliptical } });

        Assert.Contains("PPTX029", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void A_gradient_lost_on_a_master_shape_or_inside_a_group_is_declared()
    {
        var (deck, slide) = Deck();
        deck.Master = deck.Master with { Objects = [new ObjectElement { Id = "m", Bounds = Box, Content = new ShapeObject { Id = "m", Shape = ShapeKind.Rectangle, Style = Elliptical } }] };
        Assert.Contains("PPTX029", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));

        var (grouped, groupSlide) = Deck();
        groupSlide.Add(new ObjectElement
        {
            Id = "g", Bounds = Box,
            Content = new ShapeGroupObject { Id = "g", Children = [new GroupChild(new ShapeObject { Id = "c", Shape = ShapeKind.Rectangle, Style = Elliptical }, 0, 0, 1, 1)] },
        });
        Assert.Contains("PPTX029", new PptxWriter().CheckFidelity(grouped).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void A_hatch_without_background_stays_without_background()
    {
        var (deck, slide) = Deck();
        var hatch = ShapeStyle.Default with { Fill = ShapeFill.Default with { Kind = ShapeFillKind.Hatch, Hatch = ShapeHatch.Default with { BackgroundHex = null } } };
        slide.Add(new ObjectElement { Id = "h", Bounds = Box, Content = new ShapeObject { Id = "h", Shape = ShapeKind.Rectangle, Style = hatch } });

        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(stream).Value!;

        var shape = back.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();
        Assert.Equal(ShapeFillKind.Hatch, shape.Style.Fill.Kind);
        Assert.Null(shape.Style.Fill.Hatch.BackgroundHex);
    }
}
