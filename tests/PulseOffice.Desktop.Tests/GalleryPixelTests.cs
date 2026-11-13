using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Themes;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Shapes.Fontwork;
using PulseOffice.Document.Tables;
using PulseOffice.UI.Controls.Tables;
using PulseOffice.UI.Controls.Tables.Insert;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class GalleryPixelTests(HeadlessDesktopSession headless)
{
    private const string WriteVariable = "PULSE_GOLDEN_WRITE";

    private readonly BitmapPixels _pixels = new();

    [Fact]
    public Task La_galleria_del_fontwork_resta_identica_al_pixel() => headless.Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new FontworkGalleryView(new FontworkDesigner(new FontworkLooks().All, "Title", "Inter"), new FontworkGalleryServices(
            provider.GetRequiredService<IFontworkRenderer>(),
            provider.GetRequiredService<IFontPickerFactory>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<ITextCatalog>()));
        Compare("fontwork-gallery", view, 900, 700);
    }, CancellationToken.None);

    [Fact]
    public Task La_galleria_degli_stili_di_tabella_resta_identica_al_pixel() => headless.Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var request = new TableInsertRequest("command.folio.insert.table.title") { Rows = 3, Columns = 3 };
        var view = new TableInsertView(
            new TableInsertDesigner(new TableStylePresets().All, request),
            request,
            new TableInsertServices(provider.GetRequiredService<ITableStyleGallery>(), provider.GetRequiredService<IIconCatalog>(), provider.GetRequiredService<ITextCatalog>()));
        Compare("table-style-gallery", view, 900, 700);
    }, CancellationToken.None);

    [Fact]
    public Task La_galleria_dei_temi_resta_identica_al_pixel() => headless.Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new ThemeDialogView(new Presentation(), new ThemeDialogServices(
            provider.GetRequiredService<IThemeChoices>(),
            provider.GetRequiredService<IThemeNames>(),
            provider.GetRequiredService<IThemePreviews>(),
            provider.GetRequiredService<IFontPickerFactory>(),
            provider.GetRequiredService<ITextCatalog>()));
        Compare("theme-gallery", view, 960, 640);
    }, CancellationToken.None);

    private void Compare(string name, Control view, double width, double height)
    {
        var window = new Window { Width = width, Height = height, FontFamily = new Avalonia.Media.FontFamily("fonts:Inter#Inter"), Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var tiles = view.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tile") && button.Width > 60).ToList();
        Assert.NotEmpty(tiles);
        var area = tiles.Select(tile => new Rect(tile.TranslatePoint(default, window)!.Value, tile.Bounds.Size)).Aggregate((a, b) => a.Union(b));
        var top = Math.Max(0, (int)area.Y - 24);
        var crop = new PixelRect((int)area.X, top, (int)Math.Ceiling(area.Width), (int)Math.Ceiling(area.Bottom) - top);

        using var frame = window.CaptureRenderedFrame()!;
        var actual = Crop(_pixels.Read(frame), crop);
        window.Close();

        if (Environment.GetEnvironmentVariable(WriteVariable) is { Length: > 0 } target)
        {
            Directory.CreateDirectory(target);
            using var output = _pixels.Create(actual);
            File.WriteAllBytes(Path.Combine(target, name + ".png"), _pixels.EncodePng(output));
            return;
        }

        using var stored = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Golden", name + ".png"));
        var expected = _pixels.Read(stored);
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        var different = Enumerable.Range(0, expected.Width * expected.Height).Count(index => Differs(expected, actual, index));
        Assert.True(different <= expected.Width * expected.Height / 1000, $"{different} pixels differ in {name}");
    }

    private bool Differs(BgraImage expected, BgraImage actual, int index)
    {
        for (var channel = 0; channel < 4; channel++)
            if (Math.Abs(expected.Pixels[index * 4 + channel] - actual.Pixels[index * 4 + channel]) > 6) return true;
        return false;
    }

    private BgraImage Crop(BgraImage source, PixelRect area)
    {
        var width = Math.Min(area.Width, source.Width - area.X);
        var height = Math.Min(area.Height, source.Height - area.Y);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            Array.Copy(source.Pixels, ((area.Y + y) * source.Width + area.X) * 4, pixels, y * width * 4, width * 4);
        return new BgraImage(pixels, width, height);
    }

    private ServiceProvider Provider() =>
        ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
}
