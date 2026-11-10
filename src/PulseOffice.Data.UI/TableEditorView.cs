using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public sealed record TableEdit(
    DataSnapshot Snapshot,
    IReadOnlyList<TableMerge> Merges,
    IReadOnlyList<TableCellFormat> CellFormats);

public sealed class TableEditorView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const double ColumnWidth = 132;
    private const double RowNumberWidth = 44;
    private const double GridMaxWidth = 640;
    private const double GridMaxHeight = 360;
    private const int MaxLinesPerRow = 12;

    private readonly ITextCatalog _texts;
    private readonly DataSnapshot _original;
    private readonly DesignTokens.Metrics _metrics;
    private readonly TableGridPanel _panel;
    private readonly ScrollBar _vertical;
    private readonly ScrollBar _horizontal;
    private Button _mergeButton = null!;
    private Button _splitButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private bool _syncing;

    public TableEditorView(DataTableObject table, ITextCatalog texts, ITableEditModel? model = null,
        ICellLineEstimator? lines = null, ITableEditorPaletteSource? palettes = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _original = table.Snapshot;
        _metrics = DesignTokens.For(InterfaceDensity.Compact);

        Sheet = new TableEditorSheet(table, texts, model ?? new TableEditModel());
        var lineHeight = Math.Ceiling(_metrics.FontSize * 1.35);
        var layout = new TableEditorLayout(Sheet, lines ?? new CellLineEstimator(), new TableEditorMetrics(
            RowNumberWidth: RowNumberWidth,
            LetterHeight: Math.Max(20, _metrics.RowHeight - 4),
            ColumnWidth: ColumnWidth,
            FontSize: _metrics.FontSize,
            LineHeight: lineHeight,
            PaddingX: _metrics.SpacingSmall + 2,
            PaddingY: Math.Max(3, _metrics.SpacingSmall / 2 + 1),
            MinRowHeight: _metrics.RowHeight,
            MaxLines: MaxLinesPerRow));
        _panel = new TableGridPanel(Sheet, layout, palettes ?? new TableEditorPaletteSource())
        {
            MaxWidth = GridMaxWidth,
            MaxHeight = GridMaxHeight,
        };

        _vertical = new ScrollBar { Orientation = Orientation.Vertical, Minimum = 0 };
        _horizontal = new ScrollBar { Orientation = Orientation.Horizontal, Minimum = 0 };
        Grid.SetColumn(_vertical, 1);
        Grid.SetRow(_horizontal, 1);
        _vertical.ValueChanged += (_, e) => Scroll(new Vector(_panel.Offset.X, e.NewValue));
        _horizontal.ValueChanged += (_, e) => Scroll(new Vector(e.NewValue, _panel.Offset.Y));
        _panel.ScrollChanged += (_, _) => SyncScrollBars();
        _panel.LayoutUpdated += (_, _) => SyncScrollBars();

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Children = { _panel, _vertical, _horizontal },
            },
        }
        .Themed(Border.BorderBrushProperty, Palette.Keys.BorderStrong)
        .Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new TextBlock
                {
                    Text = _texts["deck.table.selectHint"],
                    FontSize = _metrics.FontSizeSmall,
                    TextWrapping = TextWrapping.Wrap,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                frame,
                BuildToolbar(),
            },
        };

        Sheet.Changed += (_, _) => RefreshCommands();
        RefreshCommands();

        Footer = new DialogFooter(InterfaceDensity.Compact)
            .WithCancel(_texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(_texts["prompt.confirm"], () => Completed?.Invoke(this, Build()), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public TableEditorSheet Sheet { get; }

    public TableGridPanel Cells => _panel;

    private void Scroll(Vector offset)
    {
        if (_syncing) return;
        _panel.Offset = offset;
    }

    private void SyncScrollBars()
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            var view = _panel.BodyViewport;
            var extent = _panel.BodyExtent;
            Sync(_vertical, extent.Height, view.Height, _panel.Offset.Y);
            Sync(_horizontal, extent.Width, view.Width, _panel.Offset.X);
        }
        finally { _syncing = false; }
    }

    private static void Sync(ScrollBar bar, double extent, double viewport, double value)
    {
        var maximum = Math.Max(0, extent - viewport);
        var visible = maximum > 0.5;
        if (bar.IsVisible != visible) bar.IsVisible = visible;
        if (bar.Maximum != maximum) bar.Maximum = maximum;
        if (bar.ViewportSize != viewport) bar.ViewportSize = viewport;
        if (bar.Value != value) bar.Value = value;
        bar.SmallChange = 24;
        bar.LargeChange = Math.Max(24, viewport - 24);
    }

    private void RefreshCommands()
    {
        _mergeButton.IsEnabled = Sheet.CanMerge;
        _splitButton.IsEnabled = Sheet.CanSplit;
        _undoButton.IsEnabled = Sheet.CanUndo;
        _redoButton.IsEnabled = Sheet.CanRedo;
    }

    private void Run(Action action)
    {
        _panel.Commit();
        action();
        RefreshCommands();
        _panel.Focus();
    }

    private TableEdit Build()
    {
        _panel.Commit();
        return Sheet.Build(_original);
    }

    private Control BuildToolbar()
    {
        var rows = Group(_texts["deck.table.group.rows"],
            ToolButton("＋", _texts["command.deck.table.addRow.title"], () => Sheet.AppendRow()),
            ToolButton("－", _texts["command.deck.table.removeRow.title"], () => Sheet.RemoveLastRow()));

        var columns = Group(_texts["deck.table.group.columns"],
            ToolButton("＋", _texts["command.deck.table.addColumn.title"], () => Sheet.AppendColumn()),
            ToolButton("－", _texts["command.deck.table.removeColumn.title"], () => Sheet.RemoveLastColumn()));

        _mergeButton = ToolButton("⊞", _texts["command.deck.table.merge.title"], () => Sheet.Merge());
        _splitButton = ToolButton("⊟", _texts["command.deck.table.split.title"], () => Sheet.Split());
        var cells = Group(_texts["deck.table.group.cells"], _mergeButton, _splitButton);

        var align = Group(_texts["deck.table.group.align"],
            ToolButton("⤒", _texts["command.deck.table.valignTop.title"], () => Sheet.SetVerticalAlign(CellVerticalAlign.Top)),
            ToolButton("⛶", _texts["command.deck.table.valignCenter.title"], () => Sheet.SetVerticalAlign(CellVerticalAlign.Center)),
            ToolButton("⤓", _texts["command.deck.table.valignBottom.title"], () => Sheet.SetVerticalAlign(CellVerticalAlign.Bottom)));

        _undoButton = ToolButton("↶", _texts["command.shell.edit.undo.title"], () => Sheet.Undo());
        _redoButton = ToolButton("↷", _texts["command.shell.edit.redo.title"], () => Sheet.Redo());
        var history = Group(_texts["deck.table.group.history"], _undoButton, _redoButton);

        return new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = _metrics.SpacingLarge,
            LineSpacing = _metrics.SpacingMedium,
            Children = { rows, columns, cells, align, history },
        };
    }

    private Control Group(string caption, params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        foreach (var b in buttons) row.Children.Add(b);

        return new StackPanel
        {
            Spacing = _metrics.SpacingSmall / 2,
            Children =
            {
                new TextBlock
                {
                    Text = caption.ToUpperInvariant(),
                    FontSize = _metrics.FontSizeSmall - 1,
                    FontWeight = FontWeight.SemiBold,
                    LetterSpacing = 0.8,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                row,
            },
        };
    }

    private Button ToolButton(string glyph, string label, Action run)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                new TextBlock { Text = glyph, FontSize = _metrics.FontSize, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = label, FontSize = _metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center },
            },
        };

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            Focusable = false,
        }
        .Themed(Button.BackgroundProperty, Palette.Keys.Surface)
        .Themed(Button.BorderBrushProperty, Palette.Keys.Border)
        .Themed(Button.ForegroundProperty, Palette.Keys.TextPrimary);

        button.Click += (_, _) => Run(run);
        return button;
    }
}
