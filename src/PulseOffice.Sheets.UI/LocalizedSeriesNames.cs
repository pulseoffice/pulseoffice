using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI;

public sealed class LocalizedSeriesNames(ITextCatalog texts) : IChartSeriesNames
{
    public string Fallback(int number) => texts.Format("chart.series.fallback", number);
}
