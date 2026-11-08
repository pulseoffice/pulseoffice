using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class RibbonMemoryTests
{
    [Fact]
    public void ChoicesSurviveANewInstance()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ribbon-{Guid.NewGuid():N}.json");
        try
        {
            var first = new RibbonMemory(path);
            Assert.Equal("#C00000", first.Recall("ledger.font.color", "#C00000"));

            first.Remember("ledger.font.color", "#2563EB");
            first.Remember("ledger.fill.color", null);
            first.Remember("ledger.border.preset", "diagonalUp");

            var second = new RibbonMemory(path);
            Assert.Equal("#2563EB", second.Recall("ledger.font.color", "#C00000"));
            Assert.Null(second.Recall("ledger.fill.color", "#FFFF00"));
            Assert.Equal("diagonalUp", second.Recall("ledger.border.preset", "bottom"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnUnreadableFileFallsBackToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ribbon-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ rotto");
            Assert.Equal("bottom", new RibbonMemory(path).Recall("ledger.border.preset", "bottom"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
