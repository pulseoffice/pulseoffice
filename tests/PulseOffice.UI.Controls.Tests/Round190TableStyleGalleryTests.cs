using Avalonia.Controls;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Tables;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round190TableStyleGalleryTests
{
    private static readonly TextCatalog Texts = new(UiLanguage.English);

    private sealed class PresetNames : ITableStyleGallery
    {
        public Task<TableStyleChoice?> AskAsync(string titleKey, string? current, bool headerRow, TableBanding banding, CancellationToken ct) =>
            Task.FromResult<TableStyleChoice?>(null);

        public string Name(TableLook look) => look.Name;
    }

    [Fact]
    public Task The_shared_gallery_offers_every_table_style_option() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var options = new TableBanding { BandedRows = false, BandedColumns = true, FirstColumn = true, LastColumn = true, LastRow = true };
        var view = new TableStyleGalleryView(new TableStylePresets().All, Texts, new PresetNames(), "elegant", headerRow: false, options, new GalleryNavigator());
        var window = new Window { Content = view, Width = 700, Height = 600 };
        window.Show();

        Assert.Equal(options, view.Banding);
        Assert.All(view.Gallery.Thumbnails.OfType<TableLookPreview>(), preview => Assert.Equal(options, preview.Banding));

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.Gallery.Invoke();

        Assert.Equal(options, Assert.IsType<TableStyleChoice>(result).Banding);
        window.Close();
    }, CancellationToken.None);
}
