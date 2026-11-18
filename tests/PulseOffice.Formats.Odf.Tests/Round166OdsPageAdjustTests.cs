using PulseOffice.Foundation.Pages;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round166OdsPageAdjustTests
{
    [Fact]
    public void Page_number_offsets_survive_as_page_adjust()
    {
        var source = Workbook.CreateDefault();
        source.PageStyles.Define("Offset", new PageSetup { Paper = new PulseOffice.Foundation.Pages.PaperCatalog().Default, Footer = new HeaderFooter(Center: "&P+2 / &P-1") });
        source.Sheets[0].PageStyleName = "Offset";
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;

        var read = new OdsReader().Read(stream);

        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics));
        Assert.Equal("&P+2 / &P-1", read.Value!.PageStyles.For(read.Value.Sheets[0]).Setup.Footer.Center);
    }
}
