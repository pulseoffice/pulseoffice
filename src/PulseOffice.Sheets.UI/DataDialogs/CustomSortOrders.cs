using System.Globalization;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record CustomSortOrder(string Label, IReadOnlyList<string> Items);

public interface ICustomSortOrders
{
    IReadOnlyList<CustomSortOrder> For(CultureInfo culture);
}

public sealed class CultureSortOrders : ICustomSortOrders
{
    public IReadOnlyList<CustomSortOrder> For(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var format = culture.DateTimeFormat;
        return
        [
            Order(MondayFirst(format.AbbreviatedDayNames)),
            Order(MondayFirst(format.DayNames)),
            Order(Months(format.AbbreviatedMonthNames)),
            Order(Months(format.MonthNames)),
        ];
    }

    private CustomSortOrder Order(IReadOnlyList<string> items) =>
        new(string.Join(", ", items.Take(3)) + ", …", items);

    private IReadOnlyList<string> MondayFirst(IReadOnlyList<string> days) => [.. days.Skip(1), days[0]];

    private IReadOnlyList<string> Months(IReadOnlyList<string> months) => [.. months.Where(month => month.Length > 0)];
}

public interface ILocaleSortOrders
{
    IReadOnlyList<CustomSortOrder> Current();

    IReadOnlyList<CustomSortOrder> Including(IEnumerable<IReadOnlyList<string>> remembered);
}

public sealed class LocaleSortOrders(ICustomSortOrders orders, PulseOffice.Sheets.Formatting.ISpreadsheetLocale locale) : ILocaleSortOrders
{
    public IReadOnlyList<CustomSortOrder> Current() => orders.For(locale.Culture);

    public IReadOnlyList<CustomSortOrder> Including(IEnumerable<IReadOnlyList<string>> remembered)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        var compare = locale.Culture.CompareInfo;
        var all = Current().ToList();
        foreach (var items in remembered.Where(items => items.Count > 0))
            if (!all.Any(order => order.Items.Count == items.Count
                && order.Items.Zip(items).All(pair => compare.Compare(pair.First, pair.Second, CompareOptions.IgnoreCase) == 0)))
                all.Add(new CustomSortOrder(string.Join(", ", items.Take(3)) + (items.Count > 3 ? ", …" : string.Empty), items));
        return all;
    }
}
