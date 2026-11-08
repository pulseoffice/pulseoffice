using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Documents.Indexes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Documents.UI.Indexes;

public sealed record FolioIndexPreset(string Key, IndexKind Kind, string Group)
{
    public int? Levels { get; init; }

    public bool? PageNumbers { get; init; }

    public bool? Hyperlinks { get; init; }

    public bool? LetterSeparators { get; init; }

    public IndexDefinition Apply(IndexDefinition defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        return defaults with
        {
            Levels = Levels ?? defaults.Levels,
            PageNumbers = PageNumbers ?? defaults.PageNumbers,
            Hyperlinks = Hyperlinks ?? defaults.Hyperlinks,
            LetterSeparators = LetterSeparators ?? defaults.LetterSeparators,
        };
    }
}

public sealed class FolioIndexPresets
{
    public const string ContentsGroup = "contents";
    public const string IndexesGroup = "indexes";

    public IReadOnlyList<FolioIndexPreset> All { get; } =
    [
        new("classic", IndexKind.Contents, ContentsGroup),
        new("noPages", IndexKind.Contents, ContentsGroup) { PageNumbers = false },
        new("twoLevels", IndexKind.Contents, ContentsGroup) { Levels = 2 },
        new("chapters", IndexKind.Contents, ContentsGroup) { Levels = 1 },
        new("alphabetical", IndexKind.Alphabetical, IndexesGroup) { LetterSeparators = true },
        new("illustrations", IndexKind.Illustrations, IndexesGroup),
        new("tables", IndexKind.Tables, IndexesGroup),
        new("objects", IndexKind.Objects, IndexesGroup),
        new("bibliography", IndexKind.Bibliography, IndexesGroup),
        new("user", IndexKind.User, IndexesGroup),
    ];
}

public sealed record FolioIndexPick(FolioIndexPreset Preset, bool Customize);

public interface IFolioIndexGallery
{
    Task<FolioIndexPick?> AskAsync(CancellationToken ct);
}

public sealed class FolioIndexGallery(IDialogService dialogs, FolioIndexPresets presets, ITextCatalog texts, IGalleryNavigator navigator) : IFolioIndexGallery
{
    public async Task<FolioIndexPick?> AskAsync(CancellationToken ct) =>
        await dialogs.ShowAsync<FolioIndexPick>(new FolioIndexGalleryView(presets.All, texts, navigator),
            new DialogOptions { TitleKey = "command.folio.insert.index.title", PreferredWidth = 640, AccessibleDescription = texts["folio.index.gallery.explanation"] }, ct).ConfigureAwait(true);
}

public sealed class FolioIndexThumbnailPainter : IThumbnailPainter<FolioIndexPreset>
{
    public Control Create(FolioIndexPreset item, Size size) => new FolioIndexThumbnail(item) { Height = size.Height };

    public void Repaint(Control thumbnail, FolioIndexPreset item)
    {
        if (thumbnail is FolioIndexThumbnail drawn) drawn.Preset = item;
    }
}

public sealed class FolioIndexThumbnail(FolioIndexPreset preset) : Control
{
    private readonly IBrush _paper = Brushes.White;
    private readonly IBrush _ink = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));
    private readonly IBrush _soft = new SolidColorBrush(Color.FromRgb(0xA8, 0xB0, 0xBC));
    private readonly IBrush _accent = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));

    public FolioIndexPreset Preset
    {
        get => preset;
        set
        {
            preset = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(_paper, bounds);
        var left = bounds.Width * 0.12;
        var right = bounds.Width * 0.88;
        var y = bounds.Height * 0.14;
        context.FillRectangle(_ink, new Rect(left, y, bounds.Width * 0.34, 3));
        y += 9;
        var rows = Rows();
        var step = Math.Max(5, (bounds.Height - y - 6) / Math.Max(1, rows.Count));
        foreach (var (indent, width, heading, page) in rows)
        {
            var start = left + indent * bounds.Width * 0.07;
            if (heading)
            {
                context.FillRectangle(_accent, new Rect(start, y, 5, 3));
                y += step;
                continue;
            }
            var end = start + (right - start - 10) * width;
            context.FillRectangle(indent == 0 ? _ink : _soft, new Rect(start, y, end - start, 2));
            if (page)
            {
                for (var dot = end + 3; dot < right - 8; dot += 3) context.FillRectangle(_soft, new Rect(dot, y + 1, 1, 1));
                context.FillRectangle(_ink, new Rect(right - 5, y, 5, 2));
            }
            y += step;
        }
    }

    private IReadOnlyList<(int Indent, double Width, bool Heading, bool Page)> Rows()
    {
        var pages = preset.PageNumbers ?? preset.Kind != IndexKind.Bibliography;
        return preset.Kind switch
        {
            IndexKind.Contents => preset.Levels switch
            {
                1 => [(0, 0.7, false, pages), (0, 0.55, false, pages), (0, 0.8, false, pages), (0, 0.6, false, pages)],
                2 => [(0, 0.7, false, pages), (1, 0.6, false, pages), (1, 0.5, false, pages), (0, 0.65, false, pages), (1, 0.55, false, pages)],
                _ => [(0, 0.7, false, pages), (1, 0.6, false, pages), (2, 0.5, false, pages), (0, 0.65, false, pages), (1, 0.55, false, pages)],
            },
            IndexKind.Alphabetical => [(0, 0, true, false), (0, 0.45, false, true), (1, 0.4, false, true), (0, 0, true, false), (0, 0.5, false, true)],
            IndexKind.Bibliography => [(0, 0.9, false, false), (1, 0.7, false, false), (0, 0.85, false, false), (1, 0.6, false, false)],
            _ => [(0, 0.75, false, pages), (0, 0.6, false, pages), (0, 0.7, false, pages), (0, 0.55, false, pages)],
        };
    }
}

public sealed class FolioIndexGalleryView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ThumbnailGallery<FolioIndexPreset> _gallery;
    private readonly DialogFooter _footer;
    private FolioIndexPreset _selected;

    public event EventHandler<object?>? Completed;

    public FolioIndexGalleryView(IReadOnlyList<FolioIndexPreset> presets, ITextCatalog texts, IGalleryNavigator navigator, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);
        _selected = presets[0];
        _gallery = new ThumbnailGallery<FolioIndexPreset>(
            [.. presets.Select(preset => new ThumbnailGalleryItem<FolioIndexPreset>(preset, texts["folio.index.preset." + preset.Key], texts["folio.index.group." + preset.Group]))],
            new FolioIndexThumbnailPainter(),
            navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 132,
                ThumbnailHeight = 92,
                ThumbnailWidth = 72,
                TilePadding = new Thickness(6),
                FrameRadius = metrics.RadiusTight,
                Columns = 4,
                AccessibleName = texts["folio.index.gallery.explanation"],
            },
            metrics);
        _gallery.Select(0);
        _gallery.SelectionChanged += (_, preset) => _selected = preset;
        _gallery.Invoked += (_, preset) =>
        {
            _selected = preset;
            Confirm();
        };
        Loaded += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _gallery.TileOf(_gallery.SelectedIndex)?.Focus(Avalonia.Input.NavigationMethod.Directional), Avalonia.Threading.DispatcherPriority.Input);

        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["folio.index.gallery.customize"], Customize)
            .WithAction(texts["folio.fields.insert"], Confirm, primary: true);

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = texts["folio.index.gallery.explanation"], FontSize = metrics.FontSize, TextWrapping = TextWrapping.Wrap }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _gallery,
            },
        };
    }

    public Control? Footer => _footer;

    public ThumbnailGallery<FolioIndexPreset> Gallery => _gallery;

    public FolioIndexPreset Selected => _selected;

    private void Customize() => Completed?.Invoke(this, new FolioIndexPick(_selected, true));

    private void Confirm() => Completed?.Invoke(this, new FolioIndexPick(_selected, false));
}
