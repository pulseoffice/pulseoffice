using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Tables;

public sealed record TableStyleChoice(TableLook Look, bool HeaderRow, TableBanding Banding)
{
    public bool BandedRows => Banding.BandedRows;
}

public interface ITableStyleGallery
{
    Task<TableStyleChoice?> AskAsync(string titleKey, string? current, bool headerRow, TableBanding banding, CancellationToken ct);

    string Name(TableLook look);
}

public sealed class TableStyleGallery(ITableStylePresets presets, IDialogService dialogs, ITextCatalog texts, IGalleryNavigator navigator) : ITableStyleGallery
{
    public async Task<TableStyleChoice?> AskAsync(string titleKey, string? current, bool headerRow, TableBanding banding, CancellationToken ct) =>
        await dialogs.ShowAsync<TableStyleChoice>(
            new TableStyleGalleryView(presets.All, texts, this, current, headerRow, banding, navigator),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 640 },
            ct).ConfigureAwait(true);

    public string Name(TableLook look) => texts[$"table.style.{look.Key}"];
}

public interface ITableLookBanding
{
    bool HeaderRow { get; }

    TableBanding Banding { get; }
}

public sealed class TableLookPainter(IReadOnlyList<TableLook> looks, ITableLookBanding banding) : IThumbnailPainter<int>
{
    public Control Create(int item, Size size) => new TableLookPreview(looks[item]) { Height = size.Height, Rows = 4, Columns = 4 };

    public void Repaint(Control thumbnail, int item)
    {
        if (thumbnail is not TableLookPreview preview) return;
        preview.HeaderRow = banding.HeaderRow;
        preview.Banding = banding.Banding;
    }
}

public sealed class TableStyleGalleryView : UserControl, IDialogContent, IDialogFooterOwner, ITableLookBanding
{
    public const int TilesPerRow = 4;

    private readonly IReadOnlyList<TableLook> _looks;
    private readonly ThumbnailGallery<int> _gallery;
    private readonly CheckBox _header;
    private readonly CheckBox _banded;
    private readonly CheckBox _firstColumn;
    private readonly CheckBox _lastColumn;
    private readonly CheckBox _lastRow;
    private readonly CheckBox _bandedColumns;
    private readonly DialogFooter _footer;
    private TableLook _selected;

    public event EventHandler<object?>? Completed;

    public TableStyleGalleryView(IReadOnlyList<TableLook> looks, ITextCatalog texts, ITableStyleGallery names, string? current, bool headerRow, TableBanding banding,
        IGalleryNavigator navigator, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(looks);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(navigator);
        var metrics = DesignTokens.For(density);
        _looks = looks;
        _selected = looks.FirstOrDefault(look => look.Key == current) ?? looks[0];

        _header = new CheckBox { IsChecked = headerRow, Content = new TextBlock { Text = texts["table.style.header"], FontSize = metrics.FontSize } };
        ArgumentNullException.ThrowIfNull(banding);
        CheckBox Option(bool value, string key) => new() { IsChecked = value, Content = new TextBlock { Text = texts[key], FontSize = metrics.FontSize } };
        _banded = Option(banding.BandedRows, "table.style.banded");
        _firstColumn = Option(banding.FirstColumn, "table.style.firstColumn");
        _lastColumn = Option(banding.LastColumn, "table.style.lastColumn");
        _lastRow = Option(banding.LastRow, "table.style.lastRow");
        _bandedColumns = Option(banding.BandedColumns, "table.style.bandedColumns");

        _gallery = new ThumbnailGallery<int>(
            [.. Enumerable.Range(0, looks.Count).Select(index => new ThumbnailGalleryItem<int>(index, names.Name(looks[index])))],
            new TableLookPainter(looks, this),
            navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 136,
                ThumbnailHeight = 72,
                TilePadding = new Thickness(5),
                FrameRadius = metrics.RadiusTight,
                Columns = TilesPerRow,
                AccessibleName = texts["table.style.explanation"],
            },
            metrics);
        _gallery.Select(IndexOfSelected());
        _gallery.SelectionChanged += (_, index) => _selected = _looks[index];
        _gallery.Invoked += (_, index) =>
        {
            _selected = _looks[index];
            Confirm();
        };

        Loaded += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _gallery.TileOf(_gallery.SelectedIndex)?.Focus(Avalonia.Input.NavigationMethod.Directional),
            Avalonia.Threading.DispatcherPriority.Input);

        foreach (var option in new[] { _header, _banded, _firstColumn, _lastColumn, _lastRow, _bandedColumns })
        {
            option.Margin = new Thickness(0, 0, metrics.SpacingLarge, metrics.SpacingSmall);
            option.IsCheckedChanged += (_, _) => _gallery.RefreshThumbnails();
        }

        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], Confirm, primary: true);

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock
                {
                    Text = texts["table.style.explanation"],
                    FontSize = metrics.FontSize,
                    TextWrapping = TextWrapping.Wrap,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _gallery,
                new WrapPanel { Orientation = Orientation.Horizontal, Children = { _header, _banded, _bandedColumns, _firstColumn, _lastColumn, _lastRow } },
            },
        };
    }

    public Control? Footer => _footer;

    public ThumbnailGallery<int> Gallery => _gallery;

    public TableLook Selected => _selected;

    public bool HeaderRow => _header.IsChecked == true;

    public bool BandedRows => _banded.IsChecked == true;

    public TableBanding Banding => new()
    {
        BandedRows = BandedRows,
        BandedColumns = _bandedColumns.IsChecked == true,
        FirstColumn = _firstColumn.IsChecked == true,
        LastColumn = _lastColumn.IsChecked == true,
        LastRow = _lastRow.IsChecked == true,
    };

    private int IndexOfSelected()
    {
        for (var index = 0; index < _looks.Count; index++)
            if (ReferenceEquals(_looks[index], _selected)) return index;
        return 0;
    }

    private void Confirm() => Completed?.Invoke(this, new TableStyleChoice(_selected, HeaderRow, Banding));
}

public sealed class TableLookPreview(TableLook look) : Control
{
    public TableLook Look
    {
        get => look;
        set
        {
            look = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }

    public int Rows
    {
        get;
        set
        {
            field = Math.Max(1, value);
            InvalidateVisual();
        }
    } = 5;

    public int Columns
    {
        get;
        set
        {
            field = Math.Max(1, value);
            InvalidateVisual();
        }
    } = 4;

    public double MaxRowHeight
    {
        get;
        set
        {
            field = Math.Max(2, value);
            InvalidateVisual();
        }
    } = double.PositiveInfinity;

    public bool HeaderRow
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = true;

    public bool BandedRows
    {
        get => Banding.BandedRows;
        set => Banding = Banding with { BandedRows = value };
    }

    public TableBanding Banding
    {
        get;
        set
        {
            field = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    } = TableBanding.Standard;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size).Deflate(1);
        var height = Math.Min(bounds.Height / Rows, MaxRowHeight);
        var area = new Rect(bounds.X, bounds.Y + (bounds.Height - height * Rows) / 2, bounds.Width, height * Rows);
        context.FillRectangle(Brushes.White, area);
        var width = area.Width / Columns;
        var line = new Pen(new SolidColorBrush(Color.FromUInt32(look.BorderColor)), Math.Max(0.6, look.BorderWidth * 1.2));
        var bodyInk = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63), 0.55);
        var strongInk = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
        var headerInk = new SolidColorBrush(look.HeaderInk is { } headerColour ? Color.FromUInt32(headerColour) : Color.FromRgb(0x4B, 0x55, 0x63));
        for (var r = 0; r < Rows; r++)
        {
            var header = HeaderRow && r == 0;
            var body = r - (HeaderRow ? 1 : 0);
            var fill = header ? look.HeaderFill : Banding.BandsRow(body) ? look.BandFill : look.BodyFill;
            var row = new Rect(area.X, area.Y + r * height, area.Width, height);
            if (fill is { } color) context.FillRectangle(new SolidColorBrush(Color.FromUInt32(color)), row);
            var ink = header ? headerInk : bodyInk;
            var thickness = Math.Min(6, Math.Max(1.5, height * (header && look.HeaderBold ? 0.26 : 0.2)));
            for (var c = 0; c < Columns; c++)
            {
                if (!header && !Banding.BandsRow(body) && Banding.BandsColumn(c) && look.BandFill is { } columnBand)
                    context.FillRectangle(new SolidColorBrush(Color.FromUInt32(columnBand)), new Rect(area.X + c * width, row.Y, width, height));
                var emphasized = !header && Banding.Emphasizes(body, c, Rows - (HeaderRow ? 1 : 0), Columns);
                var strong = header && look.HeaderBold || emphasized;
                var weight = emphasized ? Math.Min(6, thickness * 1.3) : thickness;
                var bar = new Rect(area.X + c * width + width * 0.18, row.Y + (height - weight) / 2, width * (strong ? 0.6 : 0.5), weight);
                context.FillRectangle(emphasized ? strongInk : ink, bar);
            }
            if (look.InnerLines || r == 0 || header) context.DrawLine(line, row.TopLeft, row.TopRight);
        }
        context.DrawLine(line, area.BottomLeft, area.BottomRight);
        if (!look.InnerLines) return;
        for (var c = 0; c <= Columns; c++) context.DrawLine(line, new Point(area.X + c * width, area.Y), new Point(area.X + c * width, area.Bottom));
    }
}
