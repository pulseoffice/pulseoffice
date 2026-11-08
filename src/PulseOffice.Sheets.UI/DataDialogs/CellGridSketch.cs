using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record SketchCell(string Text, bool Header = false, bool Marked = false, bool Number = false);

public sealed class CellGridSketch : UserControl
{
    private readonly IBrush _line = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
    private readonly IBrush _headerFill = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
    private readonly IBrush _markFill = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7));

    private readonly LedgerDialogKit _kit;
    private readonly Grid _grid = new();
    private readonly double _cellWidth;

    public CellGridSketch(LedgerDialogKit kit, double cellWidth = 58)
    {
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        _cellWidth = cellWidth;
        Content = new Border
        {
            Background = Brushes.White,
            BorderBrush = _line,
            BorderThickness = new Thickness(1, 1, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _grid,
        };
    }

    public IReadOnlyList<IReadOnlyList<SketchCell>> Shown { get; private set; } = [];

    public void Show(IReadOnlyList<IReadOnlyList<SketchCell>> rows)
    {
        Shown = rows ?? throw new ArgumentNullException(nameof(rows));
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();
        IsVisible = rows.Count > 0;
        if (rows.Count == 0) return;

        var columns = rows.Max(row => row.Count);
        for (var column = 0; column < columns; column++) _grid.ColumnDefinitions.Add(new ColumnDefinition(_cellWidth, GridUnitType.Pixel));
        for (var row = 0; row < rows.Count; row++)
        {
            _grid.RowDefinitions.Add(new RowDefinition(20, GridUnitType.Pixel));
            for (var column = 0; column < rows[row].Count; column++)
            {
                var cell = rows[row][column];
                var border = new Border
                {
                    Background = cell.Marked ? _markFill : cell.Header ? _headerFill : Brushes.White,
                    BorderBrush = _line,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = new TextBlock
                    {
                        Text = cell.Text,
                        FontSize = _kit.Metrics.FontSizeSmall,
                        FontWeight = cell.Header ? FontWeight.SemiBold : FontWeight.Normal,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = cell.Number ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    },
                };
                ToolTip.SetTip(border, cell.Text.Length > 8 ? cell.Text : null);
                Grid.SetRow(border, row);
                Grid.SetColumn(border, column);
                _grid.Children.Add(border);
            }
        }
    }
}
