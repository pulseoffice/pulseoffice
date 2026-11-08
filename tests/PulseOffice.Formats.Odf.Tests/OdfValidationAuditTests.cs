using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfValidationAuditTests
{
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;

    [Fact]
    public void Un_elenco_con_una_concatenazione_resta_una_formula()
    {
        var validation = Reopened("of:cell-content-is-in-list(\"a\"&\"b\";\"c\")");

        Assert.Equal(ValidationKind.List, validation.Kind);
        Assert.Empty(validation.ListValues);
        Assert.NotNull(validation.FirstFormula);
    }

    [Fact]
    public void Un_elenco_di_testi_con_virgolette_raddoppiate_si_legge_come_valori()
    {
        var validation = Reopened("of:cell-content-is-in-list(\"di \"\"lusso\"\"\";\"base\")");

        Assert.Equal(["di \"lusso\"", "base"], validation.ListValues);
    }

    private DataValidation Reopened(string condition)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("a"));
        sheet.Validations.Add(new DataValidation { Range = new CellRange(CellAddress.First, CellAddress.First), Kind = ValidationKind.List, ListValues = ["x"] });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            content.Descendants(_table + "content-validation").Single().SetAttributeValue(_table + "condition", condition);
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            content.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return Assert.Single(result.Value!.Sheets[0].Validations);
    }
}
