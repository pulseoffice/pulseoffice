using System.Collections.Concurrent;
using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Rich;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed class DocumentLanguageChartCellText(ITextCultures cultures) : IChartCellText
{
    private const double UnboundedWidth = 1000;

    private readonly DateTime _epoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);
    private readonly ConcurrentDictionary<CultureInfo, ICellPresenter> _presenters = new();

    public DocumentLanguageChartCellText() : this(new TextCultures(CultureInfo.InvariantCulture))
    {
    }

    public string Text(Workbook workbook, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.Value.Kind == ValueKind.Text) return cell.Value.ToString();
        var presenter = _presenters.GetOrAdd(cultures.For(workbook.TextLanguage), Presenter);
        return presenter.Present(cell, workbook.Styles, workbook.NumberFormats, UnboundedWidth).Text;
    }

    private ICellPresenter Presenter(CultureInfo culture) => new CellPresenter(new NumberFormatter(culture, Date));

    private DateTime Date(double serial) =>
        serial is >= -657434 and <= 2958465 ? _epoch.AddDays(serial) : _epoch;
}
