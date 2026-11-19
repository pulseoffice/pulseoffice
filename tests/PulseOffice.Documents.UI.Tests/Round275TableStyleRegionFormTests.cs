using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round275TableStyleRegionFormTests
{
    private readonly FolioTableStyleForm _form = new(new TextCatalog(UiLanguage.English));

    [Fact]
    public void The_form_offers_total_row_columns_and_corners()
    {
        var keys = _form.Tabs(TableProperties.None).SelectMany(tab => tab.Fields).Select(field => field.Key).ToList();

        Assert.Contains("TotalRow.background", keys);
        Assert.Contains("FirstColumn.color", keys);
        Assert.Contains("SouthEastCell.bold", keys);
    }

    [Fact]
    public void A_cleared_inherited_colour_becomes_none_and_a_region_is_read()
    {
        var effective = new TableProperties { Background = 0xFF00FF00 };
        var values = new Dictionary<string, object>
        {
            ["background"] = string.Empty,
            ["TotalRow.background"] = "#DDEEFF",
            ["TotalRow.bold"] = true,
        };

        var read = _form.Read(effective, TableProperties.None, values);

        Assert.Equal(TableRegionFormat.NoColour, read.Background);
        Assert.Equal(new TableRegionFormat { Background = 0xFFDDEEFF, Bold = true }, read.TotalRow);
        Assert.Null(read.FirstColumn);
    }
}
