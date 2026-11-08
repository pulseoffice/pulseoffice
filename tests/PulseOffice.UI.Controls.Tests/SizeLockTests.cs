using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SizeLockTests
{
    private static SizeLock Create(bool locked)
    {
        var units = new MeasureUnits();
        MeasureEntry Entry() => new(new MeasureOptions("cm", 0.1, 100, 0.1), units, CultureInfo.InvariantCulture);
        return new SizeLock(Entry(), Entry(), new LockedSize(8, 4, locked), new TextCatalog(UiLanguage.Italian), DesignTokens.For(InterfaceDensity.Compact));
    }

    [Fact]
    public Task ConIlLucchettoChiusoLAltezzaSegueLaLarghezza() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var size = Create(true);
        size.WidthField.StepBy(1, large: true);
        Assert.Equal(new LockedSize(9, 4.5, true), size.Value);
        size.LockButton.IsChecked = false;
        size.HeightField.StepBy(1, large: true);
        Assert.Equal(new LockedSize(9, 5.5, false), size.Value);
    }, CancellationToken.None);

    [Fact]
    public Task ConLaLarghezzaZeroIlLucchettoNonPortaLAltezzaAlMassimo() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var units = new MeasureUnits();
        MeasureEntry Entry() => new(new MeasureOptions("cm", 0, 100, 0.1), units, CultureInfo.InvariantCulture);
        var size = new SizeLock(Entry(), Entry(), new LockedSize(0, 4, true), new TextCatalog(UiLanguage.Italian), DesignTokens.For(InterfaceDensity.Compact));

        size.WidthField.StepBy(1, large: true);

        Assert.Equal(new LockedSize(1, 4, true), size.Value);
    }, CancellationToken.None);
}
