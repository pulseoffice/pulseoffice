using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round282ImageToneViewTests
{
    [Fact]
    public Task The_picture_corrections_panel_starts_from_the_tone_and_answers_a_tone() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var pixels = new BitmapPixels();
        var bitmap = pixels.Create(new BgraImage([.. Enumerable.Repeat((byte)128, 64)], 4, 4));
        var initial = new ImageTone(10, -20, 30, ImageColorMode.BlackWhite);
        var view = new ImageToneView(bitmap, initial, pixels, new PixelFilterCatalog(), new MeasureUnits(), new TextCatalog(UiLanguage.English));
        var window = new Window { Content = view, Width = 820, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(initial, view.Tone);
        Assert.Equal(0.7, view.After.Opacity, 3);
        Assert.NotSame(bitmap, view.After.Source);

        view.Apply(ImageTone.Neutral);
        Assert.Same(ImageTone.Neutral, view.Tone);
        Assert.Same(bitmap, view.After.Source);

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.Apply(new ImageTone(Mode: ImageColorMode.Watermark));
        window.Close();
        Assert.Null(result);
        Assert.Equal(ImageColorMode.Watermark, view.Tone.Mode);
    }, CancellationToken.None);

    [Fact]
    public void Black_and_white_splits_pixels_at_half_luminance()
    {
        var image = new BgraImage([10, 10, 10, 255, 200, 200, 200, 255], 2, 1);
        new PixelFilterCatalog().BlackWhite().Apply(image);
        Assert.Equal(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }, image.Pixels);
    }
}
