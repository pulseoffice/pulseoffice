using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.Tables;

public sealed class TableLookOptions : WrapPanel
{
    private readonly CheckBox _header;
    private readonly CheckBox _banded;
    private readonly CheckBox _bandedColumns;
    private readonly CheckBox _firstColumn;
    private readonly CheckBox _lastColumn;
    private readonly CheckBox _lastRow;

    public TableLookOptions(ITextCatalog texts, bool headerRow, TableBanding banding, double fontSize, Thickness optionMargin, Orientation orientation = Orientation.Horizontal)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(banding);
        Orientation = orientation;
        CheckBox Option(bool value, string key)
        {
            var option = new CheckBox
            {
                IsChecked = value,
                Margin = optionMargin,
                Content = new TextBlock { Text = texts[key], FontSize = fontSize, TextWrapping = TextWrapping.Wrap },
            };
            option.IsCheckedChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
            Children.Add(option);
            return option;
        }
        _header = Option(headerRow, "table.style.header");
        _banded = Option(banding.BandedRows, "table.style.banded");
        _bandedColumns = Option(banding.BandedColumns, "table.style.bandedColumns");
        _firstColumn = Option(banding.FirstColumn, "table.style.firstColumn");
        _lastColumn = Option(banding.LastColumn, "table.style.lastColumn");
        _lastRow = Option(banding.LastRow, "table.style.lastRow");
    }

    public event EventHandler? Changed;

    public CheckBox HeaderOption => _header;

    public CheckBox BandedRowsOption => _banded;

    public bool HeaderRow => _header.IsChecked == true;

    public TableBanding Banding => new()
    {
        BandedRows = _banded.IsChecked == true,
        BandedColumns = _bandedColumns.IsChecked == true,
        FirstColumn = _firstColumn.IsChecked == true,
        LastColumn = _lastColumn.IsChecked == true,
        LastRow = _lastRow.IsChecked == true,
    };
}
