using Avalonia;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round268ShapeAutofitTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static (Presentation Deck, Slide Slide, ObjectElement Element) Slide(ShapeObject shape, double rotation = 0)
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var element = new ObjectElement
        {
            Id = "forma",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(6), Twip.FromCentimeters(1)),
            RotationDegrees = rotation,
            Content = shape,
        };
        slide.Add(element);
        return (deck, slide, element);
    }

    private static ShapeObject Shape(string text, TextAutofit fit) => new()
    {
        Id = "forma",
        Shape = ShapeKind.Rectangle,
        Text = text,
        Style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Autofit = fit, Size = 18 } },
    };

    [Fact]
    public Task A_shape_that_resizes_to_its_text_grows_with_more_lines() => Session.Dispatch(() =>
    {
        var (deck, slide, element) = Slide(Shape("Uno\nDue\nTre\nQuattro", TextAutofit.ResizeShape));
        var fitting = new TextFrameFitting(new TextFrameMeasure(new ListLabels()), new TextFrameResize());

        var fitted = fitting.Fit(deck, slide, element);
        var fixedShape = fitting.Fit(deck, slide, element with { Content = Shape("Uno\nDue\nTre\nQuattro", TextAutofit.None) });

        Assert.True(fitted.Bounds.Height.Value > element.Bounds.Height.Value);
        Assert.Equal(element.Bounds, fixedShape.Bounds);
    }, CancellationToken.None);

    [Fact]
    public void A_line_box_placed_by_a_rotation_is_hit_in_its_own_frame()
    {
        var turn = Matrix.CreateTranslation(-50, -50) * Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(50, 50);
        var placement = new SlideElementPlacement(0, 0, 100, 100, 0.05, 0)
        {
            Lines =
            [
                new RichTextLineBox(0, null, new Rect(10, 10, 80, 10)) { Placement = turn },
                new RichTextLineBox(1, null, new Rect(10, 30, 80, 10)) { Placement = turn },
            ],
        };
        var body0 = new PrintTag("p:0", PrintRole.Paragraph);
        var body1 = new PrintTag("p:1", PrintRole.Paragraph);
        var locator = new SlideParagraphLocator(placement, new Dictionary<int, SlideParagraphTags> { [0] = new(body0, null), [1] = new(body1, null) });

        var first = new Point(50, 15).Transform(turn);
        var second = new Point(50, 35).Transform(turn);

        Assert.Same(body0, locator.At(first.X, first.Y));
        Assert.Same(body1, locator.At(second.X, second.Y));
    }

    [Fact]
    public Task Shape_text_in_columns_reports_its_painted_lines() => Session.Dispatch(() =>
    {
        var shape = Shape("Uno\nDue\nTre\nQuattro", TextAutofit.None) with
        {
            Style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Layout = TextLayout.Default with { Columns = 2 } } },
        };
        var painted = new PaintedTextLineLog();
        var drawing = new Avalonia.Media.DrawingGroup();
        using (var context = drawing.Open())
            new PulseOffice.UI.Controls.Shapes.ShapeRenderer().Render(context, shape, new Rect(0, 0, 200, 60), new PulseOffice.UI.Controls.Shapes.ShapeSurroundings() { Painted = painted });

        Assert.Equal([0, 1, 2, 3], painted.Lines.Select(line => line.Paragraph).Distinct().Order());
    }, CancellationToken.None);
}
