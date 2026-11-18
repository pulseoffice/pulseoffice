using PulseOffice.Formats.Odf;
using PulseOffice.Formats;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round180ContentSpoolTests
{
    private sealed class RecordingSpool : IContentSpool
    {
        public int Created { get; private set; }

        public Stream Create()
        {
            Created++;
            return new MemoryStream();
        }
    }

    [Fact]
    public void The_spreadsheet_body_is_spooled_through_the_injected_storage()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Data");
        var spool = new RecordingSpool();

        using var output = new MemoryStream();
        new OdsWriter(false, new SignatureLineImage(), new VectorFallback(), spool).Write(workbook, output);

        Assert.Equal(1, spool.Created);
        Assert.True(output.Length > 0);
    }

    [Fact]
    public void The_default_spool_stays_in_memory()
    {
        Assert.IsType<MemoryStream>(new MemoryContentSpool().Create());
    }
}
