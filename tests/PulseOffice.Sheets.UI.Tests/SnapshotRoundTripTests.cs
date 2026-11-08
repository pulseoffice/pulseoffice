using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class SnapshotRoundTripTests
{
    [Fact]
    public void UnaCopiaScrittaNelFormatoNativoSiRiconosceERilegge()
    {
        var registry = new SpreadsheetFormatRegistry(
            [new OdsSpreadsheetReader()], [new OdsSpreadsheetWriter()], new PulseOffice.Formats.Intake.DocumentIntake());

        var workbook = Workbook.CreateDefault();


        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dati");

        using (var stream = File.Create(file))
            Assert.True(registry.WriterFor(registry.Native)!.Write(workbook, stream).Succeeded);

        using var source = File.OpenRead(file);
        var reader = registry.ReaderForContent(source);

        Assert.NotNull(reader);

        source.Position = 0;
        var result = reader.Read(source);

        Assert.True(result.Succeeded);
        File.Delete(file);
    }
}
