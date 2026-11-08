using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlDeepAuditTests
{
    private static readonly DateTimeOffset When = new(2026, 10, 10, 9, 30, 0, TimeSpan.Zero);
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void UnCommentoSenzaParagrafiSiScriveInDocxESiRilegge()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("Uno") { Runs = [new TextRun("Uno")] })
        {
            Marks = [new CommentMark(0, "c1", MarkEdge.Start), new CommentMark(3, "c1", MarkEdge.End)],
        }]);
        document.Comments.Add(new Comment("c1", "Carla", When, new BlockContainer([])));

        using var stream = new MemoryStream();
        var written = _composer.Writer(DocxFlavour.Document).Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        Assert.Single(read.Value!.Comments.All);
    }

    [Fact]
    public void UnDiagrammaConIDatiRottiNonFaFallireLaLetturaDelPptx()
    {
        const string corpus = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/smartart-rotation2.pptx";
        Assert.SkipWhen(!File.Exists(corpus), "Reference corpus not available.");
        using var buffer = new MemoryStream();
        using (var source = File.OpenRead(corpus)) source.CopyTo(buffer);
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, true))
            foreach (var data in document.PresentationPart!.SlideParts.SelectMany(slide => slide.DiagramDataParts))
                using (var stream = data.GetStream(FileMode.Create)) stream.Write("<dgm:dataModel"u8);
        buffer.Position = 0;

        var read = new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(buffer);

        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public void UnaBibliografiaConDtdNonSiLegge()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        using var buffer = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, buffer).Succeeded);
        using (var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(buffer, true))
        {
            var part = package.MainDocumentPart!.AddCustomXmlPart(DocumentFormat.OpenXml.Packaging.CustomXmlPartType.CustomXml);
            using var stream = part.GetStream(FileMode.Create);
            stream.Write("""
                <!DOCTYPE b:Sources [<!ENTITY t "Rossi">]>
                <b:Sources xmlns:b="http://schemas.openxmlformats.org/officeDocument/2006/bibliography"><b:Source><b:Tag>&t;</b:Tag><b:SourceType>Book</b:SourceType></b:Source></b:Sources>
                """u8);
        }
        buffer.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(buffer);

        Assert.True(read.Succeeded);
        Assert.Empty(read.Value!.Bibliography.All);
    }

    private MemoryStream Workbook(string sheetData)
    {
        var buffer = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(buffer, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook("""<x:workbook xmlns:x="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><x:sheets><x:sheet name="A" sheetId="1" r:id="rS"/></x:sheets></x:workbook>""");
            var sheet = workbook.AddNewPart<WorksheetPart>("rS");
            sheet.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet($"""<x:worksheet xmlns:x="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><x:sheetData>{sheetData}</x:sheetData></x:worksheet>""");
        }
        buffer.Position = 0;
        return buffer;
    }

    [Fact]
    public void LeCelleELeRigheSenzaRiferimentoSiLeggonoAlPostoImplicito()
    {
        using var buffer = Workbook("""<x:row><x:c><x:v>1</x:v></x:c><x:c><x:v>2</x:v></x:c></x:row><x:row r="4"><x:c r="C4"><x:v>3</x:v></x:c><x:c><x:v>4</x:v></x:c></x:row><x:row><x:c><x:v>5</x:v></x:c></x:row>""");

        var read = new XlsxReader().Read(buffer);

        Assert.True(read.Succeeded);
        var sheet = read.Value!.Sheets[0];
        string At(string a1) => sheet[PulseOffice.Sheets.Addressing.CellAddress.TryParseA1(a1, out var address) ? address : default].Value.ToString() ?? string.Empty;
        Assert.Equal(["1", "2", "3", "4", "5"], new[] { "A1", "B1", "C4", "D4", "A5" }.Select(At));
    }

    [Fact]
    public void UnaFormulaMatriceSuTuttoIlFoglioNonBloccaLaLettura()
    {
        using var buffer = Workbook("""<x:row r="1"><x:c r="A1"><x:f t="array" ref="A1:XFD1048576">1</x:f><x:v>1</x:v></x:c></x:row>""");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var read = new XlsxReader().Read(buffer);

        Assert.True(read.Succeeded);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
        Assert.True(read.Value!.Sheets[0][default].IsArrayAnchor);
    }

    [Fact]
    public void UnaPivotSuColonneIntereScriveSoloLeRigheUsate()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var source = workbook.Sheets[0];
        source.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), PulseOffice.Sheets.Values.CellValue.Text("Zona"));
        source.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(1, 0), PulseOffice.Sheets.Values.CellValue.Text("Nord"));
        source.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(2, 0), PulseOffice.Sheets.Values.CellValue.Text("Sud"));
        workbook.AddSheet("Pivot");
        workbook.Pivots.Add(new PulseOffice.Sheets.Pivot.PivotDefinition
        {
            Id = "p1",
            Name = "P",
            SourceSheet = source.Name,
            SourceRange = new PulseOffice.Sheets.Addressing.CellRange(PulseOffice.Sheets.Addressing.CellAddress.First, new PulseOffice.Sheets.Addressing.CellAddress(PulseOffice.Sheets.Addressing.CellAddress.MaxRows - 1, 0)),
            DestinationSheet = "Pivot",
            Destination = new PulseOffice.Sheets.Addressing.CellAddress(0, 0),
            Fields = [new PulseOffice.Sheets.Pivot.PivotField { SourceName = "Zona", SourceColumnIndex = 0, Area = PulseOffice.Sheets.Pivot.PivotArea.Row }],
        });

        using var buffer = new MemoryStream();
        new XlsxWriter().Write(workbook, buffer);
        buffer.Position = 0;
        using var document = SpreadsheetDocument.Open(buffer, false);

        var cache = document.WorkbookPart!.PivotTableCacheDefinitionParts.Single();
        Assert.Equal(2u, cache.PivotCacheDefinition!.RecordCount!.Value);
        Assert.Equal(2, cache.PivotTableCacheRecordsPart!.PivotCacheRecords!.ChildElements.Count);
    }
}
