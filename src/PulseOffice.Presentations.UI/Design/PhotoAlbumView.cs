using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record AlbumPhoto(byte[] Data, string Name);

public sealed record PhotoAlbumResult(IReadOnlyList<AlbumPhoto> Photos, int PerSlide, bool Captions);

public sealed record PhotoAlbumServices(IGalleryNavigator Navigator, IIconCatalog Icons, ITextCatalog Texts, PulseOffice.Presentations.ICustomShowEditing Ordering);

public sealed class AlbumLayoutPainter : IThumbnailPainter<int>
{
    public Control Create(int item, Size size)
    {
        var (columns, rows) = item switch { 2 => (2, 1), 4 => (2, 2), _ => (1, 1) };
        var grid = new UniformGrid { Columns = columns, Rows = rows, Margin = new Thickness(size.Width * 0.12, size.Height * 0.14) };
        for (var index = 0; index < item; index++)
            grid.Children.Add(new Border { Margin = new Thickness(2), CornerRadius = new CornerRadius(1) }.Themed(Border.BackgroundProperty, "PulseAccent"));
        return new Border { Width = size.Width, Height = size.Height, Child = grid }.Themed(Border.BackgroundProperty, Palette.Keys.Surface);
    }

    public void Repaint(Control thumbnail, int item)
    {
    }
}

public sealed class PhotoAlbumView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly PhotoAlbumServices _services;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly ThumbnailGallery<int> _layouts;
    private const int ThumbnailPixels = 128;

    private readonly CheckBox _captions = new() { IsChecked = true };
    private readonly Dictionary<AlbumPhoto, Bitmap?> _thumbnails = new(ReferenceEqualityComparer.Instance);
    private readonly TextBlock _note;
    private List<AlbumPhoto> _photos;

    public PhotoAlbumView(IReadOnlyList<AlbumPhoto> photos, PhotoAlbumServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _photos = [.. photos];
        _layouts = new ThumbnailGallery<int>(
            [.. new[] { 1, 2, 4 }.Select(count => new ThumbnailGalleryItem<int>(count, Texts.Format("deck.album.layout", count)))],
            new AlbumLayoutPainter(), services.Navigator,
            new ThumbnailGalleryOptions { TileWidth = 96, ThumbnailHeight = 54, Columns = 3, FrameRadius = _metrics.RadiusTight, AccessibleName = Texts["deck.album.perSlide"] },
            _metrics);
        _layouts.Select(0);
        _captions.Content = Texts["deck.album.captions"];

        var right = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children = { Caption(Texts["deck.album.perSlide"]), _layouts, _captions },
        };
        var listFrame = new Border
        {
            Child = new ScrollViewer { Content = _list, MaxHeight = 380 },
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border).Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
        var left = new StackPanel { Spacing = _metrics.SpacingSmall, Children = { Caption(Texts["deck.album.photos"]), listFrame } };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingLarge };
        Grid.SetColumn(right, 1);
        layout.Children.Add(left);
        layout.Children.Add(right);
        Content = layout;

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithNote(Texts.Format("deck.album.explanation", _photos.Count), out _note)
            .WithAction(Texts["deck.album.create"], () => Completed?.Invoke(this, Result()), primary: true);
        Rebuild();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 720;

    public ThumbnailGallery<int> Layouts => _layouts;

    public IReadOnlyList<AlbumPhoto> Photos => _photos;

    public string Note => _note.Text ?? string.Empty;

    public int CachedThumbnails => _thumbnails.Count;

    private ITextCatalog Texts => _services.Texts;

    public PhotoAlbumResult Result() => new(_photos, Math.Max(1, _layouts.SelectedItem), _captions.IsChecked == true);

    public void Move(int from, int to)
    {
        var ids = _photos.Select((_, index) => index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var order = _services.Ordering.Move(ids, from, to);
        _photos = [.. order.Select(id => _photos[int.Parse(id, System.Globalization.CultureInfo.InvariantCulture)])];
        Rebuild();
    }

    public void Remove(int index)
    {
        if (index < 0 || index >= _photos.Count || _photos.Count == 1) return;
        var removed = _photos[index];
        _photos.RemoveAt(index);
        if (_thumbnails.Remove(removed, out var bitmap)) bitmap?.Dispose();
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_list.Children.Count == 0) Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _list.Children.Clear();
        foreach (var bitmap in _thumbnails.Values) bitmap?.Dispose();
        _thumbnails.Clear();
    }

    private void Rebuild()
    {
        _note.Text = Texts.Format("deck.album.explanation", _photos.Count);
        _list.Children.Clear();
        for (var i = 0; i < _photos.Count; i++)
        {
            var index = i;
            var photo = _photos[i];
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = _metrics.SpacingSmall };
            row.Children.Add(Thumbnail(photo));
            var name = new TextBlock { Text = photo.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            var up = Action("deck.motion.moveEarlier", Texts["deck.animationDialog.earlier"], () => Move(index, index - 1));
            up.IsEnabled = index > 0;
            var down = Action("deck.motion.moveLater", Texts["deck.animationDialog.later"], () => Move(index, index + 2));
            down.IsEnabled = index < _photos.Count - 1;
            var remove = Action("deck.motion.remove", Texts["deck.album.remove"], () => Remove(index));
            remove.IsEnabled = _photos.Count > 1;
            Grid.SetColumn(up, 2);
            Grid.SetColumn(down, 3);
            Grid.SetColumn(remove, 4);
            row.Children.Add(up);
            row.Children.Add(down);
            row.Children.Add(remove);
            _list.Children.Add(row);
        }
    }

    private Control Thumbnail(AlbumPhoto photo)
    {
        if (!_thumbnails.TryGetValue(photo, out var bitmap))
        {
            bitmap = Decode(photo);
            _thumbnails[photo] = bitmap;
        }
        return bitmap is null
            ? new Border { Width = 64, Height = 44 }
            : new Image { Source = bitmap, Width = 64, Height = 44, Stretch = Stretch.UniformToFill };
    }

    private Bitmap? Decode(AlbumPhoto photo)
    {
        try
        {
            using var stream = new MemoryStream(photo.Data);
            return Bitmap.DecodeToWidth(stream, ThumbnailPixels);
        }
        catch (Exception failure) when (failure is ArgumentException or NotSupportedException or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private Button Action(string icon, string label, Action run)
    {
        Control content = _services.Icons.Find(icon) is { } geometry
            ? new IconView(geometry, _metrics.IconSize * 0.8).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary)
            : new TextBlock { Text = label };
        var button = new Button { Content = content, Padding = new Thickness(4, 2), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        button.Click += (_, _) => run();
        return button;
    }

    private TextBlock Caption(string text) => new TextBlock
    {
        Text = text.ToUpperInvariant(),
        FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
        LetterSpacing = 0.6,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
}
