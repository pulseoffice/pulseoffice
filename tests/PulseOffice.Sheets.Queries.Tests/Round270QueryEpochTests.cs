using PulseOffice.Sheets;
using PulseOffice.Sheets.Queries;
using Xunit;

namespace PulseOffice.Sheets.Queries.Tests;

public sealed class Round270QueryEpochTests
{
    [Fact]
    public void Query_dates_become_serials_of_the_workbook_epoch()
    {
        var converter = new QueryValueConverter();
        var mac = new CalculationSettings { DateEpoch = CalculationSettings.Mac1904DateEpoch };

        Assert.Equal(45366, converter.Convert(new DateTime(2024, 3, 15)).AsNumber());
        Assert.Equal(43904, converter.Convert(new DateOnly(2024, 3, 15), mac).AsNumber());
    }
}
