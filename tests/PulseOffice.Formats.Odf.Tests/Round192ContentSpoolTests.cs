using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round192ContentSpoolTests
{
    private sealed class CountingOverflow
    {
        public List<MemoryStream> Created { get; } = [];

        public Stream Create()
        {
            var stream = new MemoryStream();
            Created.Add(stream);
            return stream;
        }
    }

    [Fact]
    public void A_body_over_the_threshold_moves_to_the_overflow_spool_and_reads_back_whole()
    {
        var overflow = new CountingOverflow();
        var workbook = Workbook.CreateDefault();
        for (var row = 0; row < 50; row++) workbook.Sheets[0].SetValue(new CellAddress(row, 0), CellValue.Text($"riga {row}"));

        using var stream = new MemoryStream();
        new OdsWriter(false, new SignatureLineImage(), new VectorFallback(), new OverflowContentSpool(overflow.Create, 256)).Write(workbook, stream);
        stream.Position = 0;
        var back = new OdsReader().Read(stream);

        Assert.Single(overflow.Created);
        Assert.True(back.Succeeded);
        Assert.Equal("riga 49", back.Value!.Sheets[0].GetValue(new CellAddress(49, 0)).AsText());
    }

    [Fact]
    public void A_body_under_the_threshold_stays_in_memory()
    {
        var overflow = new CountingOverflow();
        using var spooled = new OverflowContentSpool(overflow.Create, 1024).Create();

        spooled.Write([1, 2, 3]);
        spooled.Position = 0;

        Assert.Empty(overflow.Created);
        Assert.Equal(1, spooled.ReadByte());
    }
}
