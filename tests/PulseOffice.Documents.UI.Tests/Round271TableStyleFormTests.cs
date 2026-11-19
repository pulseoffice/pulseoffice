using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round271TableStyleFormTests
{
    private readonly FolioTableStyleForm _form = new(new TextCatalog(UiLanguage.English));

    [Fact]
    public void The_form_offers_both_bands_with_their_axes()
    {
        var keys = _form.Tabs(TableProperties.None).SelectMany(tab => tab.Fields).Select(field => field.Key).ToList();

        Assert.Contains("firstBand", keys);
        Assert.Contains("secondBandAxes", keys);
        Assert.Contains("headerBold", keys);
    }

    [Fact]
    public void Only_changed_values_enter_the_style()
    {
        var effective = new TableProperties { BorderWidth = 0.5, HeaderBold = true };
        var values = new Dictionary<string, object>
        {
            ["borderWidth"] = 0.5,
            ["headerBold"] = true,
            ["innerLines"] = true,
            ["firstBand"] = "#DDEEFF",
            ["firstBandAxes"] = 1,
            ["secondBandAxes"] = 0,
        };

        var read = _form.Read(effective, TableProperties.None, values);

        Assert.Null(read.BorderWidth);
        Assert.Null(read.HeaderBold);
        Assert.Equal(0xFFDDEEFFu, read.FirstBandBackground);
        Assert.Equal(TableBandAxes.Columns, read.FirstBandAxes);
        Assert.Null(read.BandAxes);
    }
}
