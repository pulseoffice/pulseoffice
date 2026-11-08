using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DisplayFontsTests
{
    private sealed class Installed(params string[] families) : IFontAvailability
    {
        public bool Has(string family) => families.Contains(family, StringComparer.OrdinalIgnoreCase);
    }

    private DisplayFonts Fonts(params string[] installed) =>
        new(new RuntimeFonts(new FontFamilyMappingTable()), new MetricCompatibleSubstitutes(), new Installed(installed));

    [Theory]
    [InlineData("Liberation Serif", "Times New Roman")]
    [InlineData("Liberation Mono", "Courier New")]
    [InlineData("Liberation Sans", "Arial")]
    public Task UnCarattereMancanteUsaIlSostitutoMetrico(string family, string expected) => MacroEventsViewTests.Session.Dispatch(() =>
        Assert.Equal(expected, Fonts("Times New Roman", "Courier New", "Arial").Effective(family)), CancellationToken.None);

    [Fact]
    public Task UnCarattereInstallatoResta() => MacroEventsViewTests.Session.Dispatch(() =>
        Assert.Equal("Liberation Serif", Fonts("Liberation Serif", "Times New Roman").Effective("Liberation Serif")), CancellationToken.None);

    [Fact]
    public Task SenzaSostitutiResta() => MacroEventsViewTests.Session.Dispatch(() =>
        Assert.Equal("Fantasia", Fonts("Arial").Effective("Fantasia")), CancellationToken.None);
}
