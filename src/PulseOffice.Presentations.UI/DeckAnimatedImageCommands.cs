using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Gif;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public sealed class DeckAnimatedImageCommands(
    IActiveDeckContext context,
    IDrawingObjectRenderer renderer,
    IBitmapPixels pixels,
    IGifCodec gifs,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts) : DeckCommandSet(context)
{
    private const double PixelsPerTwip = 2.0 / 15;

    private const int MaxFrameSide = 2048;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.insert.animatedImage") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Frames().Count >= 2,
            Execute = (_, ct) => CreateAsync(ct),
        },
    ];

    private IReadOnlyList<ObjectElement> Frames() =>
        [.. Context.Target?.SelectedMany().OfType<ObjectElement>().Where(element => renderer.CanRender(element.Content)) ?? []];

    private async Task CreateAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var frames = Frames();
        if (frames.Count < 2) return;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts.Format("animatedImage.explanation", frames.Count),
                [
                    new NumberField("delay", texts["animatedImage.delay"], 500, 20, 10000),
                    new NumberField("loops", texts["animatedImage.loops"], 0, 0, 1000),
                ],
                texts),
            new DialogOptions { TitleKey = "command.deck.insert.animatedImage.title", PreferredWidth = 440 }, ct).ConfigureAwait(true);
        if (values is null || frames.Any(frame => !Holds(t, slide, frame))) return;

        var delay = (int)Convert.ToDouble(values["delay"], System.Globalization.CultureInfo.InvariantCulture);
        var loops = (int)Convert.ToDouble(values["loops"], System.Globalization.CultureInfo.InvariantCulture);

        var widthTwips = frames.Max(frame => frame.Bounds.Width.Value);
        var heightTwips = frames.Max(frame => frame.Bounds.Height.Value);
        var scale = Math.Min(PixelsPerTwip, MaxFrameSide / (double)Math.Max(1, Math.Max(widthTwips, heightTwips)));
        var width = Math.Max(1, (int)Math.Round(widthTwips * scale));
        var height = Math.Max(1, (int)Math.Round(heightTwips * scale));

        var images = frames.Select(frame =>
        {
            var w = frame.Bounds.Width.Value * scale;
            var h = frame.Bounds.Height.Value * scale;
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
            using (var drawing = bitmap.CreateDrawingContext())
                renderer.Render(drawing, frame.Content, new Rect((width - w) / 2, (height - h) / 2, w, h));
            return new GifFrame(pixels.Read(bitmap), delay);
        }).ToList();

        var first = frames[0];
        var image = new RasterImageObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Data = gifs.Encode(images, loops),
            MediaType = "image/gif",
            PixelWidth = width,
            PixelHeight = height,
            NaturalSize = new DocumentSize(new Twip(widthTwips), new Twip(heightTwips)),
        };
        var element = new ObjectElement
        {
            Id = image.Id,
            Bounds = new DocumentRect(first.Bounds.Left, first.Bounds.Top, new Twip(widthTwips), new Twip(heightTwips)),
            ZOrder = frames.Max(frame => frame.ZOrder),
            Content = image,
        };

        t.Actions.ReplaceElementsWith(slide, [.. frames.Select(frame => frame.Id)], element, "command.deck.insert.animatedImage.title");
        t.Select([element]);
        status.Report(texts.Format("animatedImage.created", frames.Count));
    }
}
