using System.Globalization;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Layout.Flow;

public interface IPageNumberText
{
    NumberFormat FormatOf(StyleSheet sheet, string? styleName);

    string Render(NumberFormat format, int number);
}

public sealed class PageNumberText(IEnumerable<INumberFormatter> formatters) : IPageNumberText
{
    private readonly IReadOnlyDictionary<NumberFormat, INumberFormatter> _formatters = formatters.ToDictionary(formatter => formatter.Format);

    public NumberFormat FormatOf(StyleSheet sheet, string? styleName) =>
        styleName is null ? NumberFormat.Arabic : sheet.Page.Find(styleName)?.NumberFormat ?? NumberFormat.Arabic;

    public string Render(NumberFormat format, int number) =>
        _formatters.TryGetValue(format, out var formatter) ? formatter.Render(number) : number.ToString(CultureInfo.InvariantCulture);
}
