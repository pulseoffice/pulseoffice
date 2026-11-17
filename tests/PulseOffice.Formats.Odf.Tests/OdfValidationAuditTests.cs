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

    [Fact]
    public void A_validation_shared_by_scattered_cells_does_not_reach_the_cells_between_them()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var corner = new CellAddress(2, 2);
        sheet.SetValue(CellAddress.First, CellValue.Text("a"));
        sheet.SetValue(corner, CellValue.Text("c"));
        sheet.Validations.Add(new DataValidation { Range = new CellRange(CellAddress.First, CellAddress.First), Kind = ValidationKind.List, ListValues = ["x"] });
        sheet.Validations.Add(new DataValidation { Range = new CellRange(corner, corner), Kind = ValidationKind.List, ListValues = ["x"] });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        Rewrite(stream, content =>
        {
            var first = content.Descendants(_table + "content-validation").First().Attribute(_table + "name")!.Value;
            foreach (var cell in content.Descendants(_table + "table-cell"))
                if (cell.Attribute(_table + "content-validation-name") is { } name) name.Value = first;
        });
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        var read = result.Value!.Sheets[0].Validations;

        Assert.DoesNotContain(read, validation => validation.Range.Contains(new CellAddress(1, 1)));
        Assert.Contains(read, validation => validation.Range.Contains(CellAddress.First));
        Assert.Contains(read, validation => validation.Range.Contains(corner));
    }

    [Fact]
    public void A_relative_condition_follows_a_base_cell_other_than_the_first_cell_of_its_range()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var start = new CellAddress(4, 0);
        sheet.SetValue(start, CellValue.Number(1));
        sheet.Validations.Add(new DataValidation
        {
            Range = new CellRange(start, new CellAddress(9, 0)),
            Kind = ValidationKind.Decimal,
            Operator = ValidationOperator.Less,
            FirstFormula = "B1",
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        Rewrite(stream, content =>
        {
            var rule = content.Descendants(_table + "content-validation").Single();
            rule.Attribute(_table + "base-cell-address")!.Value = "Sheet1.A1";
        });

        var read = Assert.Single(new OdsReader().Read(stream).Value!.Sheets[0].Validations);

        Assert.Equal(new CellRange(start, new CellAddress(9, 0)), read.Range);
        Assert.Equal("B5", read.FirstFormula?.TrimStart('='));
    }

    private void Rewrite(MemoryStream stream, Action<XDocument> change)
    {
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            change(content);
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            content.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;
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
