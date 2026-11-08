using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Tables.Insert;

public sealed record TableInsertServices(ITableStyleGallery Names, IIconCatalog Icons, ITextCatalog Texts, IGalleryNavigator? Navigator = null);

public sealed class TableInsertView : UserControl, IDialogContent, IDialogFooterOwner
{
    public const int TilesPerRow = 4;

    private const double LeftWidth = 250;
    private const double TileWidth = 116;
    private const double ThumbnailHeight = 58;
    private const double PreviewHeight = 196;
    private const double PreviewRowHeight = 26;

    private readonly TableInsertDesigner _designer;
    private readonly TableInsertServices _services;
    private readonly TableInsertRequest _request;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly DialogChrome _chrome;
    private readonly TableSizeGrid _grid;
    private readonly TextBlock _size = new();
    private readonly TextBlock _previewSize = new();
    private readonly TextBlock _lookName = new();
    private readonly TextBlock _truncated = new();
    private readonly NumberBox _rows;
    private readonly NumberBox _columns;
    private readonly CheckBox _header = new();
    private readonly CheckBox _banded = new();
    private readonly TableLookPreview _preview;
    private readonly ThumbnailGallery<int> _gallery;

    public TableInsertView(TableInsertDesigner designer, TableInsertRequest request, TableInsertServices services)
    {
        _designer = designer ?? throw new ArgumentNullException(nameof(designer));
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _chrome = new DialogChrome(services.Icons, _metrics);
        _grid = new TableSizeGrid(designer.Size) { HorizontalAlignment = HorizontalAlignment.Center };
        _grid.Picked += (_, _) => _grid.Focus();
        _preview = new TableLookPreview(designer.Look) { Height = PreviewHeight, MaxRowHeight = PreviewRowHeight, Margin = new Thickness(_metrics.SpacingMedium) };
        _gallery = new ThumbnailGallery<int>(
            [.. Enumerable.Range(0, designer.Looks.Count).Select(index => new ThumbnailGalleryItem<int>(index, services.Names.Name(designer.Looks[index])))],
            new TableLookPainter(designer.Looks, designer),
            services.Navigator ?? new GalleryNavigator(),
            new ThumbnailGalleryOptions
            {
                TileWidth = TileWidth,
                ThumbnailHeight = ThumbnailHeight,
                TilePadding = new Thickness(5),
                FrameRadius = _metrics.RadiusTight,
                Columns = TilesPerRow,
                AccessibleName = services.Texts["table.insert.style"],
            },
            _metrics);
        _gallery.SelectionChanged += (_, index) => _designer.ChooseLook(index);
        _gallery.Invoked += (_, _) => Confirm();
        _rows = Number(designer.Size.Limits.MaxRows, designer.Size.Rows);
        _columns = Number(designer.Size.Limits.MaxColumns, designer.Size.Columns);
        _rows.ValueCommitted += (_, value) => _designer.Size.Rows = (int)Math.Round(value);
        _columns.ValueCommitted += (_, value) => _designer.Size.Columns = (int)Math.Round(value);

        Focusable = true;
        Avalonia.Automation.AutomationProperties.SetName(this, Texts[request.TitleKey]);
        AddHandler(KeyDownEvent, Keys, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Content = Layout();
        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts[request.ConfirmKey], Confirm, primary: true);

        _designer.Changed += (_, _) => Refresh();
        Refresh();
        AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => _grid.Focus(NavigationMethod.Tab), Avalonia.Threading.DispatcherPriority.Background);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public TableInsertDesigner Designer => _designer;

    public TableSizeGrid Grid => _grid;

    public TableLookPreview Preview => _preview;

    public IReadOnlyList<TableLookPreview> Thumbnails => [.. _gallery.Thumbnails.OfType<TableLookPreview>()];

    public ThumbnailGallery<int> Gallery => _gallery;

    public string SizeText => _size.Text ?? string.Empty;

    private ITextCatalog Texts => _services.Texts;

    private Control Layout()
    {
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LeftWidth},Auto,*") };
        var divider = new Border { Width = 1, Margin = new Thickness(_metrics.SpacingLarge, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);
        var right = Right();
        Avalonia.Controls.Grid.SetColumn(divider, 1);
        Avalonia.Controls.Grid.SetColumn(right, 2);
        layout.Children.Add(Left());
        layout.Children.Add(divider);
        layout.Children.Add(right);
        return new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children = { _chrome.Description(Texts[_request.ExplanationKey]), layout },
        };
    }

    private Control Left()
    {
        _size.FontSize = _metrics.FontSizeHeading + 2;
        _size.FontWeight = FontWeight.SemiBold;
        _size.HorizontalAlignment = HorizontalAlignment.Center;
        _size.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var hint = new TextBlock
        {
            Text = Texts["table.insert.sizeHint"],
            FontSize = _metrics.FontSizeSmall,
            HorizontalAlignment = HorizontalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            Padding = new Thickness(_metrics.SpacingMedium),
            MinHeight = 244,
            Child = new StackPanel
            {
                Spacing = 2,
                Children = { _grid, new Border { Height = _metrics.SpacingSmall }, _size, hint },
            },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);

        var numbers = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = _metrics.SpacingMedium };
        var columns = Labelled(Texts["table.insert.columns"], _columns);
        var rows = Labelled(Texts["table.insert.rows"], _rows);
        Avalonia.Controls.Grid.SetColumn(rows, 1);
        numbers.Children.Add(columns);
        numbers.Children.Add(rows);

        _header.Content = new TextBlock { Text = Texts["table.style.header"], TextWrapping = TextWrapping.Wrap };
        _header.IsChecked = _designer.HeaderRow;
        _header.IsCheckedChanged += (_, _) => _designer.HeaderRow = _header.IsChecked == true;
        _banded.Content = new TextBlock { Text = Texts["table.style.banded"], TextWrapping = TextWrapping.Wrap };
        _banded.IsChecked = _designer.BandedRows;
        _banded.IsCheckedChanged += (_, _) => _designer.BandedRows = _banded.IsChecked == true;

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                _chrome.Caption(Texts["table.insert.size"]),
                card,
                new Border { Height = _metrics.SpacingSmall },
                numbers,
                _chrome.Caption(Texts["table.insert.options"], new Thickness(0, _metrics.SpacingMedium, 0, 0)),
                _header,
                _banded,
            },
        };
    }

    private Control Right()
    {
        _lookName.FontWeight = FontWeight.SemiBold;
        _lookName.FontSize = _metrics.FontSizeSmall;
        _lookName.VerticalAlignment = VerticalAlignment.Center;
        _lookName.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _previewSize.FontSize = _metrics.FontSizeSmall;
        _previewSize.VerticalAlignment = VerticalAlignment.Center;
        _previewSize.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _truncated.FontSize = _metrics.FontSizeSmall;
        _truncated.Margin = new Thickness(0, 4, 0, 0);
        _truncated.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var styleHeader = Header(_chrome.Caption(Texts["table.insert.style"]), _lookName, new Thickness(0, 0, 0, 4));
        var previewHeader = Header(_chrome.Caption(Texts["table.insert.preview"]), _previewSize, new Thickness(0, _metrics.SpacingMedium, 0, 4));

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            ClipToBounds = true,
            Child = _preview,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        return new StackPanel { Children = { styleHeader, _gallery, previewHeader, frame, _truncated } };
    }

    private Grid Header(Control caption, Control detail, Thickness margin)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = margin };
        header.Children.Add(caption);
        Avalonia.Controls.Grid.SetColumn(detail, 1);
        header.Children.Add(detail);
        return header;
    }

    private Control Labelled(string label, Control editor)
    {
        Avalonia.Automation.AutomationProperties.SetName(editor, label);
        return LabelledPanel(label, editor);
    }

    private Control LabelledPanel(string label, Control editor) => new StackPanel
    {
        Spacing = 3,
        Children =
        {
            new TextBlock { Text = label, FontSize = _metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            editor,
        },
    };

    private NumberBox Number(int maximum, int value) =>
        new(new NumberEntry(new NumericBounds(1, maximum), true, CultureInfo.CurrentCulture), value, _metrics, _services.Icons, boxWidth: 70);

    private void Refresh()
    {
        var size = _designer.Size;
        _size.Text = Texts.Format("table.insert.dimensions", size.ShownColumns, size.ShownRows);
        _previewSize.Text = _size.Text;
        if (Math.Abs(_rows.Value - size.Rows) > 0.5) _rows.Value = size.Rows;
        if (Math.Abs(_columns.Value - size.Columns) > 0.5) _columns.Value = size.Columns;

        _gallery.Select(_designer.LookIndex);
        _gallery.RefreshThumbnails();

        _lookName.Text = _services.Names.Name(_designer.Look);
        _preview.Look = _designer.Look;
        _preview.Rows = _designer.PreviewRows;
        _preview.Columns = _designer.PreviewColumns;
        _preview.HeaderRow = _designer.HeaderRow;
        _preview.BandedRows = _designer.BandedRows;
        _truncated.IsVisible = _designer.PreviewTruncated;
        _truncated.Text = Texts.Format("table.insert.truncated", TableInsertDesigner.PreviewColumnLimit, TableInsertDesigner.PreviewRowLimit);
    }

    private void Confirm()
    {
        _rows.Read();
        _columns.Read();
        Completed?.Invoke(this, _designer.Choice());
    }

    private void Keys(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            Confirm();
            e.Handled = true;
            return;
        }
    }
}
