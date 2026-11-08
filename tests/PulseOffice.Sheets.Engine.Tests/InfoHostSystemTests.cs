using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Platform;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class InfoHostSystemTests
{
    [Theory]
    [InlineData(HostSystemKind.Windows, "pcdos")]
    [InlineData(HostSystemKind.MacOS, "mac")]
    [InlineData(HostSystemKind.Linux, "unix")]
    [InlineData(HostSystemKind.OtherUnix, "unix")]
    public void Info_names_the_system_given_by_the_platform(HostSystemKind kind, string expected) =>
        Assert.Equal(expected, Evaluate(new FixedHostSystem(kind, "Test 1.0"), "=INFO(\"system\")"));

    [Fact]
    public void Info_reports_the_version_given_by_the_platform() =>
        Assert.Equal("Test 1.0", Evaluate(new FixedHostSystem(HostSystemKind.Linux, "Test 1.0"), "=INFO(\"osversion\")"));

    private static string Evaluate(IHostSystem host, string formula)
    {
        using var provider = new ServiceCollection()
            .AddSingleton<PulseOffice.Sheets.Formatting.ISpreadsheetLocale>(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant())
            .AddSingleton(host)
            .AddSheetsEngine()
            .BuildServiceProvider();
        var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook.CreateDefault());
        var cell = new SheetCell(0, new CellAddress(0, 0));
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell).ToString();
    }
}
