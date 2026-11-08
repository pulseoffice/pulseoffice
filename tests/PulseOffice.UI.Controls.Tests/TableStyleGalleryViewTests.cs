using Avalonia.Controls;
using Avalonia.Input;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Tables;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class TableStyleGalleryViewTests
{
    private static readonly TextCatalog Texts = new(UiLanguage.English);

    private sealed class PresetNames : ITableStyleGallery
    {
        public Task<TableStyleChoice?> AskAsync(string titleKey, string? current, bool headerRow, bool bandedRows, CancellationToken ct) =>
            Task.FromResult<TableStyleChoice?>(null);

        public string Name(TableLook look) => look.Name;
    }

    [Fact]
    public Task LaGalleriaUsaLeTessereCondiviseEParteDalloStileCorrente() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var looks = new TableStylePresets().All;
        var view = new TableStyleGalleryView(looks, Texts, new PresetNames(), "elegant", headerRow: true, bandedRows: false, new GalleryNavigator());
        var window = new Window { Content = view, Width = 700, Height = 600 };
        window.Show();

        Assert.Equal(looks.Count, view.Gallery.Items.Count);
        Assert.Equal("elegant", looks[view.Gallery.SelectedIndex].Key);
        Assert.All(view.Gallery.Tiles, tile => Assert.Contains("tile", tile.Classes));
        Assert.All(view.Gallery.Thumbnails.OfType<TableLookPreview>(), preview => Assert.False(preview.BandedRows));

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.Gallery.HandleKey(Key.Right);
        view.Gallery.Invoke();

        var choice = Assert.IsType<TableStyleChoice>(result);
        Assert.Equal(looks[7].Key, choice.Look.Key);
        Assert.True(choice.HeaderRow);
        Assert.False(choice.BandedRows);
        window.Close();
    }, CancellationToken.None);
}
