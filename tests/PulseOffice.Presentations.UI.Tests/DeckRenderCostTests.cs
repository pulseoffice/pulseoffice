using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Assets.Gif;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Foundation.Units;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DeckPerformanceCollection
{
    public const string Name = "Deck performance";
}

[Collection(DeckPerformanceCollection.Name)]
public sealed class DeckRenderCostTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static void Record(string line) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-deck-render.txt"), $"{DateTimeOffset.Now:O} {line}\n");

    private static RasterImageObject AnimatedGif()
    {
        var codec = new GifCodec();
        var red = new BgraImage([0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255], 2, 2);
        var blue = new BgraImage([255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255], 2, 2);
        return new RasterImageObject
        {
            Id = "gif",
            MediaType = "image/gif",
            Data = codec.Encode([new GifFrame(red, 100), new GifFrame(blue, 100)], loops: 0),
            PixelWidth = 2,
            PixelHeight = 2,
        };
    }

    private static (Presentation Deck, DeckActions Actions) Deck(int slides)
    {
        var deck = new Presentation();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        for (var i = 0; i < slides; i++) deck.AddExisting(factory.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        return (deck, new DeckActions(deck, new UndoStack(), factory, new FakeTextCatalog()));
    }

    private static SlideRenderer Renderer() => new(new PulseOffice.UI.Controls.Shapes.DrawingObjectRenderer(), new FakeTextCatalog());

    [Fact]
    public Task A_thumbnail_with_an_animated_gif_does_not_tick_while_the_canvas_does() => Session.Dispatch(() =>
    {
        var (deck, actions) = Deck(1);
        var slide = deck.Slides[0];
        actions.InsertObject(slide, new ObjectElement
        {
            Id = deck.NextId(),
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(4), Twip.FromCentimeters(4)),
            ZOrder = slide.Elements.Count,
            Content = AnimatedGif(),
        });

        var canvas = new SlideView(Renderer()) { Deck = deck, Size = deck.Size, Master = deck.Master };
        var thumbnail = new SlideView(Renderer(), new StillImageProbe()) { Deck = deck, Size = deck.Size, Master = deck.Master };
        var window = new Window { Content = new StackPanel { Children = { canvas, thumbnail } }, Width = 800, Height = 600 };
        window.Show();
        canvas.Show(slide);
        thumbnail.Show(slide);
        Dispatcher.UIThread.RunJobs();

        Assert.True(canvas.PlaysAnimatedImages);
        Assert.False(thumbnail.PlaysAnimatedImages);
        window.Close();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public void Editing_one_slide_of_two_hundred_rebuilds_one_thumbnail_in_bounded_time()
    {
        var (deck, actions) = Deck(200);
        var cache = new SlideFrameCache<object>();
        cache.Refresh(deck, deck.Slides[0], (_, _) => new object());

        var clock = Stopwatch.StartNew();
        const int edits = 50;
        var rebuilt = 0;
        for (var i = 0; i < edits; i++)
        {
            actions.SetSlideText(deck.Slides[100], PlaceholderRole.Title, $"Edit {i}");
            cache.Refresh(deck, deck.Slides[100], (_, _) => new object());
            rebuilt += cache.Rebuilt;
        }
        var perEdit = clock.Elapsed / edits;
        Record($"slides=200 filmstrip refresh per edit={perEdit.TotalMilliseconds:0.00} rebuilt={rebuilt}");

        Assert.Equal(edits + 1, rebuilt);
        Assert.True(perEdit < TimeSpan.FromMilliseconds(20), $"{perEdit.TotalMilliseconds:0.0} ms per edit");
    }
}
