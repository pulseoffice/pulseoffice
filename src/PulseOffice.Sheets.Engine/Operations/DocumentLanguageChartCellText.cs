using System.Collections.Concurrent;
using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Rich;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed class DocumentLanguageChartCellText(ITextCultures cultures) : IChartCellText
{
    private const double UnboundedWidth = 1000;

    private readonly ConcurrentDictionary<(CultureInfo Culture, DateOnly Epoch), ICellPresenter> _presenters = new();

    public DocumentLanguageChartCellText() : this(new TextCultures(CultureInfo.InvariantCulture))
    {
    }

    public string Text(Workbook workbook, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.Value.Kind == ValueKind.Text) return cell.Value.ToString();
        var presenter = _presenters.GetOrAdd((cultures.For(workbook.TextLanguage), workbook.Calculation.DateEpoch), Presenter);
        return presenter.Present(cell, workbook.Styles, workbook.NumberFormats, UnboundedWidth).Text;
    }

    private ICellPresenter Presenter((CultureInfo Culture, DateOnly Epoch) key)
    {
        var epoch = key.Epoch.ToDateTime(TimeOnly.MinValue);
        return new CellPresenter(new NumberFormatter(key.Culture, serial =>
            serial is >= -657434 and <= 2958465 ? epoch.AddDays(serial) : epoch));
    }
}
