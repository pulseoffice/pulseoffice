using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed class PasteSketchView : UserControl
{
    private const double CellWidth = 34;
    private const double CellHeight = 20;

    private readonly IBrush Paper = Brushes.White;
    private readonly IBrush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
    private readonly IBrush GridLine = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
    private readonly IBrush Highlight = new SolidColorBrush(Color.FromRgb(0xFE, 0xF0, 0x8A));
    private readonly IBrush CommentMark = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    private readonly IBrush FormulaInk = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));

    private readonly DesignTokens.Metrics _metrics;
    private readonly string _sourceCaption;
    private readonly string _resultCaption;
    private readonly StackPanel _layout;

    public PasteSketchView(DesignTokens.Metrics metrics, string sourceCaption, string resultCaption)
    {
        _metrics = metrics;
        _sourceCaption = sourceCaption;
        _resultCaption = resultCaption;
        _layout = new StackPanel { Spacing = metrics.SpacingMedium, HorizontalAlignment = HorizontalAlignment.Center };
        Content = _layout;
    }

    public PasteSketch? Shown { get; private set; }

    public void Show(PasteSketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        Shown = sketch;
        _layout.Children.Clear();
        _layout.Children.Add(Block(_sourceCaption, sketch.Source));
        _layout.Children.Add(new Path
        {
            Data = Geometry.Parse("M8 0v14 M2 8l6 6 6-6"),
            StrokeThickness = 1.5,
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
        }.Themed(Shape.StrokeProperty, Palette.Keys.TextSecondary));
        _layout.Children.Add(Block(_resultCaption, sketch.Result));
    }

    private Control Block(string caption, IReadOnlyList<IReadOnlyList<PasteSketchCell>> cells)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions(string.Join(',', cells.Select(_ => CellHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
            ColumnDefinitions = new ColumnDefinitions(string.Join(',', cells[0].Select(_ => CellWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
        };

        for (var row = 0; row < cells.Count; row++)
            for (var column = 0; column < cells[row].Count; column++)
            {
                var cell = Cell(cells[row][column]);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }

        return new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock { Text = caption, FontSize = _metrics.FontSizeSmall, HorizontalAlignment = HorizontalAlignment.Center }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                new Border
                {
                    Background = Paper,
                    BorderBrush = GridLine,
                    BorderThickness = new Thickness(1, 1, 0, 0),
                    Child = grid,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            },
        };
    }

    private Control Cell(PasteSketchCell cell)
    {
        var face = new Grid();
        face.Children.Add(new TextBlock
        {
            Text = cell.Text,
            FontSize = _metrics.FontSizeSmall,
            FontWeight = cell.Formatted ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = cell.Formula ? FontStyle.Italic : FontStyle.Normal,
            Foreground = cell.Formula ? FormulaInk : Ink,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, cell.Validated ? 9 : 4, 0),
        });

        if (cell.Comment)
            face.Children.Add(new Path
            {
                Data = Geometry.Parse("M0 0h6v6z"),
                Fill = CommentMark,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            });

        if (cell.Validated)
            face.Children.Add(new Path
            {
                Data = Geometry.Parse("M0 0h6l-3 4z"),
                Fill = Ink,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0),
            });

        return new Border
        {
            Background = cell.Formatted ? Highlight : Paper,
            BorderBrush = GridLine,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = face,
        };
    }
}
