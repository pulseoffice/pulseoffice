using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round216RelativePeriodMenuTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    private ServiceProvider Provider() =>
        ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);

    private static FilterColumnFacts Dates() =>
        new([new FilterValueCount("2026-11-18", 1) { Date = new DateTime(2026, 11, 18) }], 0, true, []);

    [Fact]
    public Task A_date_column_offers_the_relative_periods_and_keeps_the_one_read_from_the_file() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var kit = provider.GetRequiredService<LedgerDialogKit>();
        var codec = provider.GetRequiredService<IFilterCriteriaCodec>();

        var kept = new FilterMenuView("Day", Dates(), new ColumnFilter(0) { Period = RelativeDatePeriod.LastMonth }, kit, codec);
        Assert.Equal(Enum.GetValues<RelativeDatePeriod>().Length + 1, kept.Period.ItemCount);
        Assert.Equal(RelativeDatePeriod.LastMonth, kept.Choice.Period);

        var fresh = new FilterMenuView("Day", Dates(), null, kit, codec);
        Assert.Null(fresh.Choice.Period);
        fresh.Period.SelectedIndex = (int)RelativeDatePeriod.ThisQuarter + 1;
        Assert.Equal(RelativeDatePeriod.ThisQuarter, fresh.Choice.Period);
    }, CancellationToken.None);

    [Fact]
    public Task A_text_column_has_no_period() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new FilterMenuView("Zone", new FilterColumnFacts([new FilterValueCount("North", 1)], 0, false, []),
            new ColumnFilter(0) { Period = RelativeDatePeriod.Today },
            provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<IFilterCriteriaCodec>());

        Assert.Null(view.Choice.Period);
    }, CancellationToken.None);
}
