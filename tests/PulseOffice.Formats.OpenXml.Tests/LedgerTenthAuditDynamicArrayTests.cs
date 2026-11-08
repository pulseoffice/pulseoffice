using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class LedgerTenthAuditDynamicArrayTests
{
    private readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private readonly XNamespace _loext = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";
    private readonly XlsxFormulaSyntax _xlsx = new();
    private readonly OdfFormulaSyntax _odf = new();

    [Theory]
    [InlineData("=LET(x;1;x+1)", "_xlfn.LET(_xlpm.x,1,_xlpm.x+1)")]
    [InlineData("=LET(x;\"x\";y;x&\"x\";y)", "_xlfn.LET(_xlpm.x,\"x\",_xlpm.y,_xlpm.x&\"x\",_xlpm.y)")]
    [InlineData("=LAMBDA(a;b;a*b)(2;3)", "_xlfn.LAMBDA(_xlpm.a,_xlpm.b,_xlpm.a*_xlpm.b)(2,3)")]
    [InlineData("=LET(_xlpm.x;1;_xlpm.x+A1)", "_xlfn.LET(_xlpm.x,1,_xlpm.x+A1)")]
    [InlineData("=x+LET(x;2;x)", "x+_xlfn.LET(_xlpm.x,2,_xlpm.x)")]
    public void IParametriDiLetELambdaPortanoIlPrefissoDiExcel(string formula, string expected) =>
        Assert.Equal(expected, _xlsx.ToOpenXml(formula));

    [Fact]
    public void InOdsIParametriPortanoIlPrefissoComeInCalc() =>
        Assert.Contains("COM.MICROSOFT.LET(_xlpm.x;1;_xlpm.x+1)", _odf.ToOdf("=LET(x;1;x+1)"));

    [Theory]
    [InlineData("=@A1:A10", "_xlfn.SINGLE(A1:A10)")]
    [InlineData("=SUM(@(A1:B2))*2", "SUM(_xlfn.SINGLE(A1:B2))*2")]
    [InlineData("=@INDEX(A1:A3;2)+1", "_xlfn.SINGLE(INDEX(A1:A3,2))+1")]
    [InlineData("=\"@\"&A1", "\"@\"&A1")]
    public void LaChiocciolaDiventaSingleInXlsx(string formula, string expected) =>
        Assert.Equal(expected, _xlsx.ToOpenXml(formula));

    [Fact]
    public void SingleSiRileggeComeChiocciola() =>
        Assert.Equal("=@(A1:A10)+1", _xlsx.ToInternal("_xlfn.SINGLE(A1:A10)+1"));

    private Workbook SpillingWorkbook()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[new CellAddress(0, 0)] = new Cell
        {
            Formula = "=SEQUENCE(3)",
            Value = CellValue.Number(1),
            ImplicitIntersection = false,
            SpillRange = new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
        };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Number(2), IsSpilled = true };
        sheet[new CellAddress(2, 0)] = new Cell { Value = CellValue.Number(3), IsSpilled = true };
        return workbook;
    }

    [Fact]
    public void OdsMarcaLaMatriceDinamicaConLoextSpillComeCalc()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(SpillingWorkbook(), stream);
        using var archive = new ZipArchive(new MemoryStream(stream.ToArray()));
        var content = XDocument.Load(archive.GetEntry("content.xml")!.Open());

        var anchor = content.Descendants(_table + "table-cell").First(cell => cell.Attribute(_table + "formula") is not null);
        Assert.Equal("true", (string?)anchor.Attribute(_loext + "spill"));
        Assert.Equal("3", (string?)anchor.Attribute(_table + "number-matrix-rows-spanned"));

        stream.Position = 0;
        var read = new OdsSpreadsheetReader().Read(stream).Value!.Sheets[0];
        Assert.False(read[new CellAddress(0, 0)].ImplicitIntersection);
        Assert.Equal(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), read[new CellAddress(0, 0)].SpillRange);
        Assert.Null(read[new CellAddress(0, 0)].ArrayFormulaRange);
    }

    [Fact]
    public void UnaMatriceDinamicaDiCalcSiRileggeComeDinamica()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(SpillingWorkbook(), stream);
        using var edited = new MemoryStream();
        edited.Write(stream.ToArray());
        using (var archive = new ZipArchive(edited, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var source = entry.Open()) content = XDocument.Load(source);
            var anchor = content.Descendants(_table + "table-cell").First(cell => cell.Attribute(_table + "formula") is not null);
            anchor.Attribute(_table + "number-matrix-rows-spanned")!.Remove();
            anchor.Attribute(_table + "number-matrix-columns-spanned")!.Remove();
            entry.Delete();
            using var target = archive.CreateEntry("content.xml").Open();
            content.Save(target);
        }

        edited.Position = 0;
        var read = new OdsSpreadsheetReader().Read(edited).Value!.Sheets[0];
        Assert.False(read[new CellAddress(0, 0)].ImplicitIntersection);
        Assert.Null(read[new CellAddress(0, 0)].ArrayFormulaRange);
    }

    [Fact]
    public void XlsxRiconosceLaMatriceDinamicaSoloDalTipoXldapr()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(SpillingWorkbook(), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart!.CellMetadataPart!;
            const string metadata =
                "<metadata xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:xda=\"http://schemas.microsoft.com/office/spreadsheetml/2017/dynamicarray\">" +
                "<metadataTypes count=\"2\"><metadataType name=\"XLRICHVALUE\" minSupportedVersion=\"120000\"/><metadataType name=\"XLDAPR\" minSupportedVersion=\"120000\" cellMeta=\"1\"/></metadataTypes>" +
                "<futureMetadata name=\"XLDAPR\" count=\"1\"><bk><extLst><ext uri=\"{bdbb8cdc-fa1e-496e-a857-3c3f30c029c3}\"><xda:dynamicArrayProperties fDynamic='1' fCollapsed='0'/></ext></extLst></bk></futureMetadata>" +
                "<cellMetadata count=\"2\"><bk><rc t=\"1\" v=\"0\"/></bk><bk><rc t=\"2\" v=\"0\"/></bk></cellMetadata></metadata>";
            using var data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(metadata));
            part.FeedData(data);
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            var cell = worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>().First();
            Assert.Equal(1u, cell.CellMetaIndex!.Value);
            worksheet.Save();
        }

        stream.Position = 0;
        var notDynamic = new XlsxReader().Read(stream).Value!.Sheets[0][new CellAddress(0, 0)];
        Assert.Null(notDynamic.SpillRange);
        Assert.NotNull(notDynamic.ArrayFormulaRange);
    }

    [Fact]
    public void XlsxScriveERileggeLaMatriceDinamica()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(SpillingWorkbook(), stream);
        stream.Position = 0;

        var cell = new XlsxReader().Read(stream).Value!.Sheets[0][new CellAddress(0, 0)];
        Assert.Equal(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), cell.SpillRange);
        Assert.False(cell.ImplicitIntersection);
        Assert.Equal("=SEQUENCE(3)", cell.Formula);
    }
}
