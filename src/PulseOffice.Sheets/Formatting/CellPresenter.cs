using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.Formatting;

public interface ICellPresenter
{
    FormattedCell Present(Cell cell, StyleCatalog styles, NumberFormatCatalog formats, double columnWidthInCharacters);
}

public sealed class CellPresenter(INumberFormatter formatter) : ICellPresenter
{
    public FormattedCell Present(Cell cell, StyleCatalog styles, NumberFormatCatalog formats, double columnWidthInCharacters)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(formats);

        var formatId = styles.Get(cell.StyleId).NumberFormatId;
        if (formatId == 0 && cell.HasFormula) formatId = cell.InferredFormatId;
        var format = formats.Get(formatId);
        return formatter.Format(cell.Value, format, columnWidthInCharacters);
    }
}
