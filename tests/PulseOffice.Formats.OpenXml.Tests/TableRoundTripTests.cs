using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class TableRoundTripTests
{
    private const string Folder = "Fixtures/Tables";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly TableSample _sample = new();
    private readonly TableAppearance _appearance = new(new StyleResolver());

    public static TheoryData<string> Routes() => new() { "odt", "docx", "odt-lo", "docx-lo", "odt-lo-docx", "docx-lo-odt" };

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Fixture(string name, ITextDocumentReader reader)
    {
        using var stream = File.OpenRead(Path.Combine(Folder, name));
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Route(string route) => route switch
    {
        "odt" => Through(_sample.Build(), new OdtWriter(), new OdtReader()),
        "docx" => Through(_sample.Build(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
        "odt-lo" => Fixture("tables.lo.odt", new OdtReader()),
        "docx-lo" => Fixture("tables.lo.docx", _composer.Reader(DocxFlavour.Document)),
        "odt-lo-docx" => Fixture("tables.odt.lo.docx", _composer.Reader(DocxFlavour.Document)),
        _ => Fixture("tables.docx.lo.odt", new OdtReader()),
    };

    private TableBlock Table(TextDocument document) => document.Body.Blocks.OfType<TableBlock>().Single();

    private string Text(TableBlock table, int row, int column) =>
        string.Join("\n", table.Cell(row, column).Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)).Trim();

    private CellAppearance Look(TextDocument document, TableBlock table, int row, int column) =>
        _appearance.Cell(table, _appearance.Style(document.Styles, table), row, column);

    [Fact]
    public void IFileDiPartenzaSonoQuelliDelCampione()
    {
        var folder = Environment.GetEnvironmentVariable("PULSE_TABLE_FIXTURES");
        if (string.IsNullOrEmpty(folder)) return;
        using (var odt = File.Create(Path.Combine(folder, "tables.odt"))) Assert.True(new OdtWriter().Write(_sample.Build(), odt).Succeeded);
        using (var docx = File.Create(Path.Combine(folder, "tables.docx"))) Assert.True(_composer.Writer(DocxFlavour.Document).Write(_sample.Build(), docx).Succeeded);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void StrutturaUnioniEDLarghezzeAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var table = Table(document);

        Assert.Equal(5, table.Rows.Count);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal([2000, 3000, 4000], table.Columns.Select(column => (int)Math.Round(column.Value / 10.0) * 10));
        Assert.Equal(2, table.Cell(1, 0).ColumnSpan);
        Assert.True(table.Cell(1, 1).Covered);
        Assert.Equal(2, table.Cell(1, 2).RowSpan);
        Assert.True(table.Cell(2, 2).Covered);
        Assert.Equal(CellVerticalAlign.Middle, table.Cell(1, 2).VerticalAlign);
        Assert.Equal("Unite", Text(table, 1, 0));
        Assert.Equal("fine", Text(table, 3, 2));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void IntestazioneRipetutaERigheCheNonSiSpezzanoAttraversanoIFormati(string route)
    {
        var table = Table(Route(route));

        Assert.Equal(1, table.HeaderRows);
        Assert.True(table.RepeatHeaderRows);
        Assert.False(table.Rows[2].AllowBreakAcrossPages);
        Assert.True(table.Rows[1].AllowBreakAcrossPages);
        Assert.InRange(table.Rows[3].Height!.Value.Value, 580, 620);
        Assert.False(table.Rows[3].MinimumHeight);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void BordiESfondiPerCellaEDelloStileAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var table = Table(document);

        var header = Look(document, table, 0, 1);
        Assert.Equal(TableSample.HeaderFill, header.Background);
        Assert.Equal(TableSample.Accent, header.Borders.Top!.Color);
        Assert.Equal(0.5, header.Borders.Top.Width, 1);
        var dashed = Look(document, table, 2, 0).Borders.Bottom!;
        Assert.Equal(BorderLineStyle.Dashed, dashed.Style);
        Assert.Equal(TableSample.Dashed.Color, dashed.Color);
        Assert.Equal(1.5, dashed.Width, 1);
        Assert.Equal(0xFFFFF3C4, Look(document, table, 3, 0).Background);
        Assert.Equal(TableSample.BandFill, Look(document, table, 4, 0).Background);
        Assert.Equal(TableSample.BandFill, Look(document, table, 2, 1).Background);
        Assert.Null(Look(document, table, 3, 1).Background);
        Assert.Equal(TableSample.Accent, Look(document, table, 2, 0).Borders.Top!.Color);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void IlTestoDellIntestazionePrendeIlColoreDelloStile(string route)
    {
        var document = Route(route);
        var table = Table(document);

        var run = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().First().Content.Runs.First();
        Assert.Equal(TableSample.HeaderInk, run.Color ?? Look(document, table, 0, 0).Ink);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LaFormulaDellaCellaAttraversaIFormati(string route)
    {
        var table = Table(Route(route));

        Assert.Equal("SUM(B3:B4)", table.Cell(4, 1).Formula);
        Assert.Equal("5", Text(table, 4, 1));
    }

    [Theory]
    [InlineData("odt")]
    [InlineData("docx")]
    public void LoStileDiTabellaTornaConLeSueProprieta(string route)
    {
        var document = Route(route);

        var style = document.Styles.Table.Find(TableSample.StyleName)!.Table;
        Assert.Equal(TableSample.HeaderFill, style.HeaderBackground);
        Assert.Equal(TableSample.HeaderInk, style.HeaderColor);
        Assert.True(style.HeaderBold);
        Assert.Equal(TableSample.BandFill, style.BandBackground);
        Assert.Equal(TableSample.Accent, style.BorderColor);
        Assert.Equal("Prezzi", Table(document).Name);
    }

    [Fact]
    public void UnIntestazioneCheNonSiRipeteSiDichiaraPersaInDocx()
    {
        var document = _sample.Build();
        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX019");

        Table(document).RepeatHeaderRows = false;

        Assert.Contains(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX019");
        Assert.False(Table(Through(document, new OdtWriter(), new OdtReader())).RepeatHeaderRows);
    }

    [Theory]
    [InlineData("odt", TableAlignment.Center, 0)]
    [InlineData("odt", TableAlignment.Right, 0)]
    [InlineData("odt", TableAlignment.Left, 567)]
    [InlineData("docx", TableAlignment.Center, 0)]
    [InlineData("docx", TableAlignment.Right, 0)]
    [InlineData("docx", TableAlignment.Left, 567)]
    public void LaPosizioneDellaTabellaNellaPaginaAttraversaIFormati(string format, TableAlignment alignment, int indent)
    {
        var document = _sample.Build();
        Table(document).Alignment = alignment;
        Table(document).Indent = new PulseOffice.Foundation.Units.Twip(indent);

        var read = format == "odt"
            ? Through(document, new OdtWriter(), new OdtReader())
            : Through(document, _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));

        Assert.Equal(alignment, Table(read).Alignment);
        Assert.Equal(indent, Table(read).Indent.Value);
    }
}
