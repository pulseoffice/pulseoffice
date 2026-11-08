using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsCalcErrorTests
{
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Calcext = "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0";

    [Fact]
    public void Calc_error_is_written_as_an_error_cell_and_read_back()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(new CellAddress(0, 0), CellValue.FromError(CellError.Calc));

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var content = archive.GetEntry("content.xml")!.Open();
            var cell = XDocument.Load(content).Descendants(Table + "table-cell").First();
            Assert.Equal("error", cell.Attribute(Calcext + "value-type")?.Value);
            Assert.Equal("#CALC!", cell.Value);
        }

        stream.Position = 0;
        var read = new OdsReader().Read(stream).Require();
        Assert.Equal(CellError.Calc, read.Sheets[0].GetValue(new CellAddress(0, 0)).Error);
    }

    [Fact]
    public void Calc_error_is_declared_for_other_odf_readers()
    {
        var workbook = Workbook.CreateDefault();
        Assert.DoesNotContain(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF271");
        workbook.Sheets[0].SetValue(new CellAddress(0, 0), CellValue.FromError(CellError.Calc));
        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF271");
    }
}
