using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round253SortedListTests
{
    private static Workbook Sorted()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = new CellRange(CellAddress.First, CellAddress.First),
            Kind = ValidationKind.List,
            ListValues = ["pear", "10", "Apple", "2"],
            SortDropdown = true,
        });
        return workbook;
    }

    [Fact]
    public void Ods_keeps_a_sorted_list_sorted()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Sorted(), stream);
        stream.Position = 0;

        var read = Assert.Single(new OdsReader().Read(stream).Value!.Sheets[0].Validations);

        Assert.True(read.SortDropdown);
        Assert.True(read.ShowDropdown);
    }

    [Fact]
    public void Xlsx_writes_typed_entries_already_sorted_and_declares_the_setting()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Sorted(), stream);
        stream.Position = 0;

        var read = Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].Validations);

        Assert.Equal(["2", "10", "Apple", "pear"], read.ListValues);
        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(Sorted()).Losses, loss => loss.Code == "XLSX244");
    }
}
