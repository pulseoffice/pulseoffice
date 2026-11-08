using Xunit;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Tests;

public class SheetEditGuardTests
{
    private readonly ISheetEditGuard _guard = new SheetEditGuard();

    private static CellRange Range(int r1, int c1, int r2, int c2) => new(new CellAddress(r1, c1), new CellAddress(r2, c2));

    [Fact]
    public void SenzaProtezioneSiScriveOvunque()
    {
        var workbook = Workbook.CreateDefault();
        Assert.True(_guard.CanWrite(workbook, workbook.Sheets[0], Range(0, 0, 1_000_000, 0)));
    }

    [Fact]
    public void SuFoglioProtettoLeCelleBloccateRifiutano()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Protection = new SheetProtection { IsEnabled = true };

        Assert.False(_guard.CanWrite(workbook, sheet, Range(0, 0, 0, 0)));
    }

    [Fact]
    public void LeCelleSbloccateSiScrivonoAncheSuFoglioProtetto()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var unlocked = workbook.Styles.Intern(workbook.Styles.Get(0) with { Locked = false });
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.SetStyle(new CellAddress(0, 0), unlocked);
        sheet.Layout.SetColumnStyle(1, unlocked);
        sheet.Protection = new SheetProtection { IsEnabled = true };

        Assert.True(_guard.CanWrite(workbook, sheet, Range(0, 0, 0, 0)));
        Assert.True(_guard.CanWrite(workbook, sheet, Range(0, 1, 1_000_000, 1)));
        Assert.False(_guard.CanWrite(workbook, sheet, Range(0, 0, 1, 0)));
    }
}
