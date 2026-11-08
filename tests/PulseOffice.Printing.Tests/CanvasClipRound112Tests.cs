using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class CanvasClipRound112Tests
{
    private sealed class Recorder : IPageCanvas
    {
        public DocumentSize PageSize { get; } = new(Twip.FromPoints(800), Twip.FromPoints(800));

        public List<string> Events { get; } = [];

        public Twip MeasureText(string text, PrintFont font) => Twip.Zero;

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
        {
        }

        public void DrawPath(PrintPath path) => Events.Add("path");

        public void DrawImage(PageImage image, DocumentRect bounds) => Events.Add("image");

        public IDisposable Clip(DocumentRect area)
        {
            Events.Add($"clip {area.Left.ToPoints():0} {area.Top.ToPoints():0} {area.Width.ToPoints():0} {area.Height.ToPoints():0}");
            return new PageClipScope(() => Events.Add("unclip"));
        }
    }

    private static DocumentRect Points(double left, double top, double width, double height) =>
        new(Twip.FromPoints(left), Twip.FromPoints(top), Twip.FromPoints(width), Twip.FromPoints(height));

    private readonly PageImage _image = new(1, 1, [0, 0, 0]);

    private static PrintPath Square(DocumentRect bounds) =>
        new(new ShapeGeometry().Outline(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle }), bounds) { Fill = PrintColor.Black };

    [Fact]
    public void An_image_across_the_edge_is_clipped_instead_of_dropped_or_spilt()
    {
        var recorder = new Recorder();

        new ClippedPageCanvas(recorder, Points(0, 0, 100, 100)).DrawImage(_image, Points(50, 50, 100, 100));

        Assert.Equal(["clip 0 0 100 100", "image", "unclip"], recorder.Events);
    }

    [Fact]
    public void An_image_inside_is_drawn_without_a_clip_and_one_outside_is_not_drawn()
    {
        var recorder = new Recorder();
        var canvas = new ClippedPageCanvas(recorder, Points(0, 0, 100, 100));

        canvas.DrawImage(_image, Points(10, 10, 20, 20));
        canvas.DrawImage(_image, Points(200, 200, 20, 20));

        Assert.Equal(["image"], recorder.Events);
    }

    [Fact]
    public void A_path_across_the_edge_is_clipped()
    {
        var recorder = new Recorder();

        new ClippedPageCanvas(recorder, Points(0, 0, 100, 100)).DrawPath(Square(Points(-20, 10, 50, 50)));

        Assert.Equal(["clip 0 0 100 100", "path", "unclip"], recorder.Events);
    }

    [Fact]
    public void Scaled_canvases_scale_the_clip_and_nested_clips_intersect()
    {
        var recorder = new Recorder();
        var scaled = new ScaledPageCanvas(recorder, 0.5, new DocumentPoint(Twip.Zero, Twip.Zero), new DocumentPoint(Twip.Zero, Twip.Zero));

        using (new ClippedPageCanvas(scaled, Points(0, 0, 100, 100)).Clip(Points(50, 50, 100, 100)))
        {
        }

        Assert.Equal(["clip 25 25 25 25", "unclip"], recorder.Events);
    }

    [Fact]
    public void Filtered_canvases_pass_the_clip_on()
    {
        var recorder = new Recorder();

        using (new FilteredPageCanvas(recorder, graphics: true, blackText: true).Clip(Points(1, 2, 3, 4)))
        {
        }

        Assert.Equal(["clip 1 2 3 4", "unclip"], recorder.Events);
    }
}
