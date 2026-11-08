using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.UI.Indexes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioIndexGalleryTests
{
    private readonly FolioIndexPresets _presets = new();

    [Fact]
    public void IPresetCambianoSoloCiòCheDichiarano()
    {
        var defaults = new IndexDefinition(IndexKind.Contents) { Title = "Indice", Levels = 10 };
        var twoLevels = _presets.All.Single(preset => preset.Key == "twoLevels").Apply(defaults);
        Assert.Equal(2, twoLevels.Levels);
        Assert.Equal("Indice", twoLevels.Title);
        Assert.True(twoLevels.PageNumbers);
        Assert.False(_presets.All.Single(preset => preset.Key == "noPages").Apply(defaults).PageNumbers);
        Assert.Equal(7, _presets.All.Select(preset => preset.Kind).Distinct().Count());
    }

    [Fact]
    public Task LaGalleriaRaggruppaSommariEIndiciEConInvioSceglie() => FolioScene.Session.Dispatch(() =>
    {
        var view = new FolioIndexGalleryView(_presets.All, new TextCatalog(UiLanguage.Italian), new GalleryNavigator());
        object? result = null;
        view.Completed += (_, value) => result = value;
        var window = new Window { Width = 700, Height = 520, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Sommari", "Indici"], view.Gallery.Items.Select(item => item.Group).Distinct());
        Assert.True(view.Gallery.TileOf(0)!.IsFocused);
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        var pick = Assert.IsType<FolioIndexPick>(result);
        Assert.Equal("noPages", pick.Preset.Key);
        Assert.False(pick.Customize);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task LaMiniaturaDisegnaRigheConPuntiniSoloSeCiSonoINumeri() => FolioScene.Session.Dispatch(() =>
    {
        int Dark(FolioIndexPreset preset)
        {
            var thumbnail = new FolioIndexThumbnail(preset) { Width = 72, Height = 92 };
            thumbnail.Measure(new Size(72, 92));
            thumbnail.Arrange(new Rect(0, 0, 72, 92));
            using var bitmap = new RenderTargetBitmap(new PixelSize(72, 92));
            bitmap.Render(thumbnail);
            var pixels = new byte[72 * 92 * 4];
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(pixels.Length);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, 72, 92), buffer, pixels.Length, 72 * 4);
                System.Runtime.InteropServices.Marshal.Copy(buffer, pixels, 0, pixels.Length);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
            }
            var count = 0;
            for (var i = 0; i < pixels.Length; i += 4)
                if (pixels[i] < 200 && pixels[i + 3] > 0) count++;
            return count;
        }

        var classic = Dark(_presets.All.Single(preset => preset.Key == "classic"));
        var plain = Dark(_presets.All.Single(preset => preset.Key == "noPages"));
        Assert.True(classic > plain, $"{classic} <= {plain}");
        Assert.True(plain > 0);
    }, CancellationToken.None);
}
