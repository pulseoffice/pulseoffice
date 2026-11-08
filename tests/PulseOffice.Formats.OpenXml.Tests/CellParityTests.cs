using System.Globalization;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class CellParityTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TableBlock Table(TextDocument document)
    {
        TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock(document.NextId("p"), text)]));
        var protectedCell = Cell("chiusa");
        protectedCell.Protected = true;
        var number = Cell("1234.50");
        number.NumberFormat = new CellNumberFormat(CellValueKind.Number, 2, true);
        var percent = Cell("12.50%");
        percent.NumberFormat = new CellNumberFormat(CellValueKind.Percent, 2, false);
        return new TableBlock(document.NextId("t"), [new TableRow([protectedCell, number, percent])], [Twip.FromCentimeters(5), Twip.FromCentimeters(5), Twip.FromCentimeters(5)]);
    }

    private static TextDocument Sample()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Insert(0, Table(document));
        return document;
    }

    private static TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private static void AssertCells(TableBlock table)
    {
        var cells = table.Rows[0].Cells;
        Assert.True(cells[0].Protected);
        Assert.False(cells[1].Protected);
        Assert.Equal(new CellNumberFormat(CellValueKind.Number, 2, true), cells[1].NumberFormat);
        Assert.Equal(CellValueKind.Percent, cells[2].NumberFormat?.Kind);
        Assert.Equal(2, cells[2].NumberFormat?.Decimals);
    }

    [Fact]
    public void ProtezioneEFormatoAttraversanoOdtEInDocxSonoDichiarati()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        AssertCells(Through(Sample(), new OdtWriter(), new OdtReader()).Body.Blocks.OfType<TableBlock>().Single());

        var docx = Through(Sample(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)).Body.Blocks.OfType<TableBlock>().Single();
        Assert.Equal("1234.50", ((ParagraphBlock)docx.Rows[0].Cells[1].Content.Blocks[0]).Text);
        Assert.Equal(new CellNumberFormat(CellValueKind.Number, 2, true), docx.Rows[0].Cells[1].NumberFormat);
        Assert.Equal(CellValueKind.Percent, docx.Rows[0].Cells[2].NumberFormat?.Kind);
        var losses = _composer.Writer(DocxFlavour.Document).CheckFidelity(Sample()).Losses.Select(loss => loss.Code).ToList();
        Assert.Contains("DOCX030", losses);
        Assert.DoesNotContain("DOCX031", losses);
    }

    [Fact]
    public void IFileDiPartenzaDelleCelleSonoQuelliDelCampione()
    {
        if (Environment.GetEnvironmentVariable("PULSE_CELL_FIXTURES") is not { Length: > 0 } folder) return;
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using var file = File.Create(Path.Combine(folder, "cells.odt"));
        Assert.True(new OdtWriter().Write(Sample(), file).Succeeded);
    }

    [Fact]
    public void UnAltraSuiteConservaProtezioneEFormatoDelleCelle()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cells", "cells.lo.odt"));
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        AssertCells(read.Value!.Body.Blocks.OfType<TableBlock>().Single());
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(CultureInfo culture) => CultureInfo.CurrentCulture = culture;

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
