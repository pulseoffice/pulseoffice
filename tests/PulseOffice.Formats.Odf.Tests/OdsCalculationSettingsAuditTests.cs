using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsCalculationSettingsAuditTests
{
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;

    private XElement Spreadsheet(string steps) =>
        new(OdfPackage.Namespaces.Office + "spreadsheet",
            new XElement(_table + "calculation-settings",
                new XElement(_table + "iteration",
                    new XAttribute(_table + "status", "enable"),
                    new XAttribute(_table + "steps", steps))));

    [Fact]
    public void I_passi_dell_iterazione_letti_hanno_un_tetto()
    {
        var settings = new OdfCalculationSettingsMapper().Read(Spreadsheet("2147483647"));

        Assert.Equal(CalculationSettings.MaximumIterations, settings.MaxIterations);
    }

    [Fact]
    public void Il_tetto_vale_per_ogni_origine_del_modello()
    {
        Assert.Equal(CalculationSettings.MaximumIterations, new CalculationSettings { MaxIterations = int.MaxValue }.MaxIterations);
        Assert.Equal(1, new CalculationSettings { MaxIterations = -4 }.MaxIterations);
    }
}
