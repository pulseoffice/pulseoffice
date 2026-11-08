using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Shapes.Fontwork;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class FontworkGalleryTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    private readonly FontworkLooks _looks = new();

    private FontworkDesigner Designer() => new(_looks.All, "Title", "Inter");

    [Fact]
    public void Il_progettista_compone_il_fontwork_con_testo_forma_colori_e_ombra()
    {
        var designer = Designer();
        var changes = 0;
        designer.Changed += (_, _) => changes++;

        designer.Text = "  Ciao  ";
        designer.Warp = FontworkWarp.Wave;
        designer.ChooseLook(1);
        designer.Shadow = false;
        designer.Italic = true;
        designer.Italic = true;

        var art = designer.Build("x");
        Assert.Equal("Ciao", art.Text);
        Assert.Equal(FontworkWarp.Wave, art.Warp);
        Assert.Equal("#DC2626", art.Style.Fill.ColorHex);
        Assert.Equal("#7F1D1D", art.Style.Line.ColorHex);
        Assert.False(art.Style.Shadow.Enabled);
        Assert.True(art.Italic);
        Assert.Equal(1, designer.Look);
        Assert.Equal(5, changes);
    }

    [Fact]
    public void Il_progettista_rifiuta_il_testo_vuoto_e_ripiega_sul_campione_in_anteprima()
    {
        var designer = Designer();
        designer.Text = "   ";
        Assert.False(designer.CanConfirm);
        Assert.Equal("Title", designer.Preview().Text);
        designer.Text = "Un testo molto lungo per una miniatura";
        Assert.Equal(12, designer.ThumbnailText.Length);
    }

    [Fact]
    public void Colori_personalizzati_e_contorno_assente()
    {
        var designer = Designer();
        designer.Fill = "#123456";
        designer.Line = string.Empty;
        Assert.Equal(-1, designer.Look);
        var art = designer.Build("x");
        Assert.Equal("#123456", art.Style.Fill.ColorHex);
        Assert.False(art.Style.Line.IsVisible);
    }

    [Fact]
    public void Le_frecce_spostano_la_forma_senza_uscire_dalla_galleria()
    {
        var designer = Designer();
        designer.Warp = FontworkWarp.Plain;
        designer.MoveWarp(-1);
        Assert.Equal(FontworkWarp.Plain, designer.Warp);
        designer.MoveWarp(FontworkGalleryView.TilesPerRow);
        Assert.Equal(designer.Warps[FontworkGalleryView.TilesPerRow], designer.Warp);
        designer.MoveWarp(100);
        Assert.Equal(designer.Warps[^1], designer.Warp);
    }

    [Fact]
    public Task La_galleria_aggiorna_anteprima_e_miniature_e_conferma_con_invio() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = View(provider, Designer());
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        object? result = null;
        view.Completed += (_, value) => result = value;

        var text = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view).OfType<TextBox>().First();
        text.Text = "Pulse";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Pulse", view.Preview.Fontwork!.Text);
        Assert.All(view.Thumbnails, thumbnail => Assert.Equal("Pulse", thumbnail.Fontwork!.Text));
        Assert.Equal(view.Designer.Warps, view.Thumbnails.Select(thumbnail => thumbnail.Fontwork!.Warp));

        view.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(FontworkWarp.ArchDown, view.Designer.Warp);
        Assert.Equal(FontworkWarp.ArchDown, view.Preview.Fontwork!.Warp);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Same(view.Designer, result);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Miniature_e_anteprima_dipingono_con_il_motore_del_documento() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IFontworkRenderer>();
        var designer = Designer();
        designer.Text = "Wave";

        var arch = Paint(new FontworkPreview(renderer, default), designer.Thumbnail(FontworkWarp.ArchUp));
        var wave = Paint(new FontworkPreview(renderer, default), designer.Thumbnail(FontworkWarp.Wave));
        Assert.NotEqual(arch.Pixels, wave.Pixels);
        Assert.Contains(Enumerable.Range(0, arch.Width * arch.Height), index => Near(arch, index, 0x25, 0x63, 0xEB));

        designer.ChooseLook(1);
        var red = Paint(new FontworkPreview(renderer, default), designer.Thumbnail(FontworkWarp.Wave));
        Assert.Contains(Enumerable.Range(0, red.Width * red.Height), index => Near(red, index, 0xDC, 0x26, 0x26));

        using var direct = new RenderTargetBitmap(new PixelSize(160, 60), new Vector(96, 96));
        using (var context = direct.CreateDrawingContext())
        {
            context.FillRectangle(Avalonia.Media.Brushes.White, new Rect(0, 0, 160, 60));
            renderer.Render(context, designer.Thumbnail(FontworkWarp.Wave), new Rect(0, 0, 160, 60));
        }
        Assert.Equal(new BitmapPixels().Read(direct).Pixels, red.Pixels);
    }, CancellationToken.None);

    private BgraImage Paint(FontworkPreview preview, FontworkObject art)
    {
        preview.Show(art);
        preview.Measure(new Size(160, 60));
        preview.Arrange(new Rect(0, 0, 160, 60));
        using var bitmap = new RenderTargetBitmap(new PixelSize(160, 60), new Vector(96, 96));
        bitmap.Render(preview);
        return new BitmapPixels().Read(bitmap);
    }

    private bool Near(BgraImage image, int index, byte r, byte g, byte b) =>
        Math.Abs(image.Pixels[index * 4 + 2] - r) < 12 && Math.Abs(image.Pixels[index * 4 + 1] - g) < 12 && Math.Abs(image.Pixels[index * 4] - b) < 12;

    private ServiceProvider Provider() =>
        ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);

    private FontworkGalleryView View(IServiceProvider provider, FontworkDesigner designer) => new(designer, new FontworkGalleryServices(
        provider.GetRequiredService<IFontworkRenderer>(),
        provider.GetRequiredService<IFontPickerFactory>(),
        provider.GetRequiredService<IIconCatalog>(),
        provider.GetRequiredService<ITextCatalog>()));
}
