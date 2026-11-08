using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsQueryBindingTests
{
    [Fact]
    public void Un_area_legata_a_un_interrogazione_torna_uguale_dall_ODS()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati 2026");
        sheet[new CellAddress(1, 1)] = new Cell { Value = CellValue.Text("regione") };
        var binding = new QueryBinding
        {
            Id = "q1",
            DisplayName = "Vendite",
            ConnectionId = "c-42",
            Query = new PulseOffice.Data.QueryDefinition { Sql = "SELECT * FROM \"vendite\" WHERE importo > 1000 AND regione <> 'Sud'", MaxRows = 500 },
            Anchor = new CellAddress(1, 1),
            Range = new CellRange(new CellAddress(1, 1), new CellAddress(5, 4)),
            IncludeHeaders = true,
            Policy = RefreshPolicy.Periodic,
            RefreshInterval = TimeSpan.FromMinutes(3),
            RowShift = RowShiftBehavior.Overwrite,
            PreserveCellFormatting = false,
            LastRowCount = 4,
            LastRefresh = new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero),
        };
        sheet.QueryBindings.AddOrUpdate(binding);
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Clienti", SheetIndex = 0, Range = new CellRange(new CellAddress(10, 0), new CellAddress(12, 2)), HasHeader = true });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Require();

        Assert.Equal(binding with { Query = read.Sheets[0].QueryBindings.All.Single().Query }, read.Sheets[0].QueryBindings.All.Single());
        Assert.Equal(binding.Query.Sql, read.Sheets[0].QueryBindings.All.Single().Query.Sql);
        Assert.Equal(500, read.Sheets[0].QueryBindings.All.Single().Query.MaxRows);
        Assert.Equal("Clienti", Assert.Single(read.DatabaseRanges.All).Name);
    }
}
