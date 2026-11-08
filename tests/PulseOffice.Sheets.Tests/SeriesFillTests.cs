using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class SeriesFillTests
{
    private readonly SeriesFill _series = new();

    private static Worksheet Sheet(double first)
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(first));
        return sheet;
    }

    private static CellRange Column(int rows) =>
        new(new CellAddress(0, 0), new CellAddress(rows - 1, 0));

    [Fact]
    public void UnaSerieLineareAvanzaDelPasso()
    {
        var plan = _series.Plan(Sheet(10), new SeriesRequest(Column(4), SeriesKind.Linear) { Step = 5 });

        Assert.Equal([15, 20, 25], plan.Select(entry => entry.Value.AsNumber()));
    }

    [Fact]
    public void UnPassoNegativoScende()
    {
        var plan = _series.Plan(Sheet(10), new SeriesRequest(Column(3), SeriesKind.Linear) { Step = -2 });

        Assert.Equal([8, 6], plan.Select(entry => entry.Value.AsNumber()));
    }

    [Fact]
    public void UnaSerieGeometricaMoltiplica()
    {
        var plan = _series.Plan(Sheet(2), new SeriesRequest(Column(4), SeriesKind.Growth) { Step = 3 });

        Assert.Equal([6, 18, 54], plan.Select(entry => entry.Value.AsNumber()));
    }

    [Fact]
    public void IlValoreDArrestoFermaLaSerie()
    {
        var plan = _series.Plan(
            Sheet(1), new SeriesRequest(Column(10), SeriesKind.Linear) { Step = 3, Stop = 8 });

        Assert.Equal([4, 7], plan.Select(entry => entry.Value.AsNumber()));
    }

    [Fact]
    public void UnaSerieDiGiorniFerialiSaltaIlFineSettimana()
    {
        var monday = new DateTime(2026, 9, 21).ToOADate();

        var plan = _series.Plan(
            Sheet(monday),
            new SeriesRequest(Column(6), SeriesKind.Date) { DateUnit = SeriesDateUnit.Weekday });

        var days = plan.Select(entry => DateTime.FromOADate(entry.Value.AsNumber()).DayOfWeek).ToList();

        Assert.DoesNotContain(DayOfWeek.Saturday, days);
        Assert.DoesNotContain(DayOfWeek.Sunday, days);
        Assert.Equal(5, days.Count);
    }

    [Fact]
    public void UnaSerieDiMesiCambiaMeseNonGiorni()
    {
        var january = new DateTime(2026, 1, 31).ToOADate();

        var plan = _series.Plan(
            Sheet(january),
            new SeriesRequest(Column(3), SeriesKind.Date) { DateUnit = SeriesDateUnit.Month });

        var months = plan.Select(entry => DateTime.FromOADate(entry.Value.AsNumber()).Month).ToList();

        Assert.Equal([2, 3], months);
    }

    [Fact]
    public void SenzaUnNumeroDiPartenzaNonSiRiempieNulla()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("ciao"));

        Assert.Empty(_series.Plan(sheet, new SeriesRequest(Column(4), SeriesKind.Linear)));
    }

    [Fact]
    public void UnAreaDiUnaCellaSolaNonHaSerie()
    {
        Assert.Empty(_series.Plan(Sheet(1), new SeriesRequest(Column(1), SeriesKind.Linear)));
    }
}

public sealed class WorkbookProtectionTests
{
    [Fact]
    public void SenzaPasswordLaProtezioneAccettaChiunque()
    {
        Assert.True(new WorkbookProtection { Structure = true }.Accepts(null));
    }

    [Fact]
    public void ConPasswordSoloQuellaGiustaPassa()
    {
        var protection = new WorkbookProtection
        {
            Structure = true,
            PasswordHash = WorkbookProtection.Hash("segreta"),
        };

        Assert.True(protection.Accepts("segreta"));
        Assert.False(protection.Accepts("sbagliata"));
        Assert.False(protection.Accepts(null));
    }

    [Fact]
    public void LaPasswordNonSiConservaInChiaro()
    {
        var hash = WorkbookProtection.Hash("segreta");

        Assert.DoesNotContain("segreta", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void SenzaStrutturaNeFinestreLaProtezioneESpenta()
    {
        Assert.False(WorkbookProtection.None.IsEnabled);
    }
}
