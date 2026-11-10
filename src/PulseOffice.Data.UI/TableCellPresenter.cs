using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PulseOffice.Data.UI;

public sealed record TableCellAppearance(
    IBrush Background,
    IBrush Foreground,
    IBrush BorderBrush,
    Thickness BorderThickness,
    FontWeight Weight,
    HorizontalAlignment Horizontal,
    VerticalAlignment Vertical,
    Thickness Padding,
    double FontSize);

public sealed class TableCellPresenter : Border
{
    private readonly TextBlock _text;

    public TableCellPresenter()
    {
        _text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        Child = _text;
        ClipToBounds = true;
    }

    public TableCellRef Cell { get; private set; }

    public string Text => _text.Text ?? string.Empty;

    public void Bind(TableCellRef cell, string text, TableCellAppearance look)
    {
        ArgumentNullException.ThrowIfNull(look);
        Cell = cell;
        if (!ReferenceEquals(_text.Text, text)) _text.Text = text;
        Background = look.Background;
        BorderBrush = look.BorderBrush;
        BorderThickness = look.BorderThickness;
        Padding = look.Padding;
        _text.Foreground = look.Foreground;
        _text.FontWeight = look.Weight;
        _text.FontSize = look.FontSize;
        _text.HorizontalAlignment = look.Horizontal;
        _text.VerticalAlignment = look.Vertical;
        _text.TextAlignment = look.Horizontal == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Left;
        IsVisible = true;
    }
}
