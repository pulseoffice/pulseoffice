using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ThumbnailGalleryTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly GalleryNavigator _navigator = new();

    private sealed class Painter : IThumbnailPainter<int>
    {
        public int Created { get; private set; }

        public List<int> Repainted { get; } = [];

        public Control Create(int item, Size size)
        {
            Created++;
            return new Border { Width = size.Width, Height = size.Height, Tag = item };
        }

        public void Repaint(Control thumbnail, int item) => Repainted.Add(item);
    }

    [Fact]
    public void Il_navigatore_scorre_righe_e_gruppi_senza_uscire()
    {
        int[] sizes = [5, 3];
        Assert.Equal(4, _navigator.Target(sizes, 4, 0, GalleryMove.Down));
        Assert.Equal(5, _navigator.Target(sizes, 4, 4, GalleryMove.Down));
        Assert.Equal(4, _navigator.Target(sizes, 4, 3, GalleryMove.Down));
        Assert.Equal(4, _navigator.Target(sizes, 4, 6, GalleryMove.Up));
        Assert.Equal(0, _navigator.Target(sizes, 4, 2, GalleryMove.Up));
        Assert.Equal(7, _navigator.Target(sizes, 4, 7, GalleryMove.Down));
        Assert.Equal(0, _navigator.Target(sizes, 4, 5, GalleryMove.First));
        Assert.Equal(7, _navigator.Target(sizes, 4, 0, GalleryMove.Last));
        Assert.Equal(0, _navigator.Target(sizes, 4, 0, GalleryMove.Left));
        Assert.Equal(5, _navigator.Target(sizes, 4, 4, GalleryMove.Right));
        Assert.Equal(-1, _navigator.Target([], 4, 0, GalleryMove.Right));

        var rows = _navigator.Rows([new GallerySection("A", [0, 1, 2, 3, 4]), new GallerySection(null, [5, 6, 7])], 4);
        Assert.Equal(["A", null, null, null], rows.Select(row => row.Header));
        Assert.Equal([4], rows[2].Positions);
    }

    [Fact]
    public Task Frecce_clic_doppio_clic_e_invio_scelgono_e_confermano() => Session.Dispatch(() =>
    {
        var painter = new Painter();
        var gallery = Gallery(painter, 10, columns: 4, groups: false);
        var window = Show(gallery);
        var chosen = new List<int>();
        var invoked = new List<int>();
        gallery.SelectionChanged += (_, item) => chosen.Add(item);
        gallery.Invoked += (_, item) => invoked.Add(item);

        Assert.Equal(10, painter.Created);
        Assert.Equal(10, gallery.Thumbnails.Count);
        gallery.Select(1);
        Assert.Empty(chosen);
        Assert.Contains("checked", gallery.TileOf(1)!.Classes);

        gallery.TileOf(1)!.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Assert.Equal([5, 6, 9], chosen);
        Assert.True(gallery.TileOf(9)!.IsFocused);
        Assert.DoesNotContain("checked", gallery.TileOf(1)!.Classes);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal([9], invoked);

        var tile = gallery.TileOf(2)!;
        var centre = tile.TranslatePoint(new Point(tile.Bounds.Width / 2, tile.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Assert.Equal(2, gallery.SelectedItem);
        Assert.Equal("Voce 2", Avalonia.Automation.AutomationProperties.GetName(tile));

        painter.Repainted.Clear();
        gallery.RefreshThumbnails();
        Assert.Equal(10, painter.Repainted.Count);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task I_gruppi_hanno_intestazione_e_il_filtro_nasconde_e_mostra_il_vuoto() => Session.Dispatch(() =>
    {
        var gallery = Gallery(new Painter(), 6, columns: 3, groups: true, filterable: true);
        var window = Show(gallery);

        var headers = gallery.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text).Where(text => text is "PARI" or "DISPARI").ToList();
        Assert.Equal(["PARI", "DISPARI"], headers);

        gallery.FilterText = "Voce 3";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([3], gallery.VisibleIndices);
        Assert.Single(gallery.Tiles);

        gallery.FilterText = "nulla";
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(gallery.VisibleIndices);
        Assert.Contains(gallery.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "Nessun risultato");

        gallery.FilterText = string.Empty;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(6, gallery.Tiles.Count);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Una_galleria_grande_crea_solo_le_miniature_visibili() => Session.Dispatch(() =>
    {
        var painter = new Painter();
        var gallery = Gallery(painter, 3000, columns: 4, groups: false);
        var window = Show(gallery);

        Assert.True(gallery.IsVirtualizing);
        Assert.InRange(painter.Created, 4, 200);
        gallery.Select(0);
        gallery.TileOf(0)!.Focus();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2999, gallery.SelectedItem);
        Assert.NotNull(gallery.TileOf(2999));
        Assert.True(gallery.TileOf(2999)!.IsFocused);
        Assert.InRange(gallery.Tiles.Count, 4, 200);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    private Window Show(Control content)
    {
        var window = new Window { Width = 600, Height = 500, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private ThumbnailGallery<int> Gallery(Painter painter, int count, int columns, bool groups, bool filterable = false) => new(
        [.. Enumerable.Range(0, count).Select(index => new ThumbnailGalleryItem<int>(index, $"Voce {index}", groups ? index % 2 == 0 ? "Pari" : "Dispari" : null))],
        painter,
        _navigator,
        new ThumbnailGalleryOptions
        {
            Columns = columns,
            TileWidth = 100,
            ThumbnailHeight = 40,
            Filterable = filterable,
            FilterPlaceholder = "Cerca",
            EmptyText = "Nessun risultato",
            MaxViewportHeight = 300,
        },
        DesignTokens.For(InterfaceDensity.Compact));
}
