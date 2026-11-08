using System.Globalization;
using PulseOffice.Sheets.UI.DataDialogs;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round89FilterCodecTests
{
    private readonly FilterCriteriaCodec _italian = new(new PulseOffice.Sheets.Engine.Operations.FilterValueText(
        new PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("it-IT"))));

    [Fact]
    public void A_condition_typed_with_the_interface_decimal_comma_is_stored_with_the_point()
    {
        Assert.Equal(">1.5", _italian.Write(new FilterConditionDraft(FilterOperator.Greater, "1,5")));
        Assert.Equal("1,5", _italian.Read(">1.5").Value);
    }

    [Fact]
    public void A_date_condition_is_stored_as_its_serial_and_shown_as_a_date()
    {
        Assert.Equal("=45296", _italian.Write(new FilterConditionDraft(FilterOperator.Equal, "05/01/2024")));
        Assert.Equal("05/01/2024", _italian.Read("=45296", dates: true).Value);
    }

    [Fact]
    public void Text_patterns_are_kept_as_typed()
    {
        Assert.Equal("*1,5*", _italian.Write(new FilterConditionDraft(FilterOperator.Contains, "1,5")));
        Assert.Equal("1,5", _italian.Read("*1,5*").Value);
    }
}
