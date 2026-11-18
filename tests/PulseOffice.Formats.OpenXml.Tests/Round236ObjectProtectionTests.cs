using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round236ObjectProtectionTests
{
    [Theory]
    [InlineData("ods", true)]
    [InlineData("ods", false)]
    [InlineData("xlsx", true)]
    [InlineData("xlsx", false)]
    public void The_edit_objects_permission_crosses_both_formats(string format, bool allowed)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Protection = new SheetProtection { IsEnabled = true, AllowEditObjects = allowed };

        var back = Through(format, workbook).Sheets[0].Protection;

        Assert.True(back.IsEnabled);
        Assert.Equal(allowed, back.AllowEditObjects);
        Assert.Equal(!allowed, back.BlocksObjects);
    }

    [Fact]
    public void An_unprotected_sheet_never_blocks_objects() =>
        Assert.False(new SheetProtection { AllowEditObjects = false }.BlocksObjects);

    private Workbook Through(string format, Workbook workbook)
    {
        using var stream = new MemoryStream();
        if (format == "ods") new OdsWriter().Write(workbook, stream); else new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return (format == "ods" ? new OdsReader().Read(stream) : new XlsxReader().Read(stream)).Value!;
    }
}
