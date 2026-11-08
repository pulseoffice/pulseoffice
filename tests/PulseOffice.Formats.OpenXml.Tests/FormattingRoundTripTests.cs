using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class FormattingRoundTripTests
{
    private const string Folder = "Fixtures/Formatting";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly FormattingSample _sample = new();

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
        "odt-lo" => Fixture("sample.lo.odt", new OdtReader()),
        "docx-lo" => Fixture("sample.lo.docx", _composer.Reader(DocxFlavour.Document)),
        "odt-lo-docx" => Fixture("sample.odt.lo.docx", _composer.Reader(DocxFlavour.Document)),
        _ => Fixture("sample.docx.lo.odt", new OdtReader()),
    };

    [Fact]
    public void IFileDiPartenzaSonoQuelliDelCampione()
    {
        var folder = Environment.GetEnvironmentVariable("PULSE_FORMATTING_FIXTURES");
        if (string.IsNullOrEmpty(folder)) return;
        using (var odt = File.Create(Path.Combine(folder, "sample.odt"))) Assert.True(new OdtWriter().Write(_sample.Build(), odt).Succeeded);
        using (var docx = File.Create(Path.Combine(folder, "sample.docx"))) Assert.True(_composer.Writer(DocxFlavour.Document).Write(_sample.Build(), docx).Succeeded);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void GliEffettiDelCarattereAttraversanoIFormati(string route)
    {
        var document = Route(route);

        var wide = _sample.Run(document, "Largo");
        Assert.Equal(2, wide.LetterSpacing!.Value, 1);
        Assert.False(wide.Kerning);
        Assert.Equal(80, _sample.Run(document, "stretto").ScaleWidth!.Value, 1);
        var raised = _sample.Run(document, "rilievo");
        Assert.Equal(TextCase.Upper, raised.Case);
        Assert.Equal(FontRelief.Embossed, raised.Relief);
        Assert.Equal(FontRelief.Engraved, _sample.Run(document, "incavo").Relief);
        Assert.True(_sample.Run(document, "nascosto").Hidden);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void BordiSfondoCapoletteraEFlussoDelParagrafoAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var paragraph = _sample.Paragraph(document, "incorniciato").Content;

        Assert.Equal("Paragrafo incorniciato con capolettera e tabulazioni\tqui", _sample.Paragraph(document, "incorniciato").Text);
        Assert.Equal(0xFFEEF2FF, paragraph.Background);
        var borders = paragraph.Borders!;
        Assert.Equal(1, borders.Top!.Width, 1);
        Assert.Equal(0xFF336699u, borders.Top.Color);
        Assert.Equal(BorderLineStyle.Double, borders.Bottom!.Style);
        Assert.Null(borders.Left);
        Assert.Equal(4, borders.PaddingTop, 0);
        if (route is not ("odt-lo-docx" or "docx-lo"))
        {
            Assert.Equal(3, paragraph.DropCap!.Lines);
            Assert.Equal(1, paragraph.DropCap.Characters);
            Assert.Equal(6, paragraph.DropCap.Distance, 0);
        }
        var tab = Assert.Single(paragraph.TabStops!);
        Assert.Equal(200, tab.Position, 0);
        Assert.Equal(TabAlignment.Right, tab.Alignment);
        Assert.Equal(TabLeader.Dots, tab.Leader);
        Assert.True(paragraph.KeepTogether);
        Assert.True(paragraph.KeepWithNext);
        Assert.Equal(1, PageBreaksBefore(document, "incorniciato"));
    }

    private int PageBreaksBefore(TextDocument document, string text)
    {
        var paragraphs = _sample.Paragraphs(document.Body).ToList();
        var index = paragraphs.FindIndex(paragraph => paragraph.Text.Contains(text, StringComparison.Ordinal));
        var breaks = paragraphs[index].Content.PageBreakBefore == true ? 1 : 0;
        for (var before = index - 1; before >= 0 && paragraphs[before].Length == 0; before--)
            if (paragraphs[before].Content.PageBreakBefore == true) breaks++;
        return breaks;
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LaNumerazioneDellePagineCheRipartiAttraversaIFormati(string route)
    {
        var document = Route(route);

        var paragraph = _sample.Paragraph(document, "Numerazione da tre").Content;
        Assert.Equal(3, paragraph.PageNumberRestart);
        Assert.NotNull(paragraph.PageStyleName);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void UnoStileDiCarattereNuovoAttraversaIFormati(string route)
    {
        var document = Route(route);

        var run = _sample.Run(document, "marcato");
        var style = document.Styles.Character.Find(run.StyleName!)!;
        Assert.True(style.Character.Bold);
        Assert.Equal(0xFFC00000u, style.Character.Color);
        Assert.Equal(1, style.Character.LetterSpacing!.Value, 1);
    }

    [Fact]
    public void LInterruzioneDiColonnaInUnoStileSiDichiaraPersaInDocx()
    {
        var document = _sample.Build();
        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX018");

        var style = document.Styles.Paragraph.Find(DefaultStyleNames.TextBody)!;
        style.Paragraph = style.Paragraph with { ColumnBreakBefore = true };

        Assert.Contains(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX018");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LInterruzioneDiColonnaAttraversaIFormati(string route)
    {
        var document = Route(route);

        var paragraph = _sample.Paragraph(document, "Colonna nuova").Content;
        if (route != "odt-lo-docx") Assert.True(paragraph.ColumnBreakBefore);
        Assert.NotEqual(true, paragraph.PageBreakBefore);
        Assert.NotEqual(true, _sample.Paragraph(document, "Testo fra due elenchi").Content.ColumnBreakBefore);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void ElenchiRipartenzeEDNumerazioneDeiCapitoliAttraversanoIFormati(string route)
    {
        var document = Route(route);

        var restarted = _sample.Paragraph(document, "Quinto").Content;
        Assert.Equal(5, restarted.ListRestart);
        Assert.Equal(1, _sample.Paragraph(document, "Sottopunto").Content.BulletLevel);
        Assert.Null(_sample.Paragraph(document, "Sesto").Content.ListRestart);
        Assert.Null(_sample.Paragraph(document, "Secondo").Content.ListRestart);
        if (route is "odt" or "odt-lo")
            Assert.True(_sample.Paragraph(document, "senza numero").Content.ListHeader);
        Assert.Equal(NumberFormat.Arabic, Heading(document, 1).Format);
        Assert.Equal(NumberFormat.Arabic, Heading(document, 2).Format);
        Assert.Equal(2, Heading(document, 2).DisplayLevels);
    }

    private ListLevel Heading(TextDocument document, int level)
    {
        if (document.Styles.Outline.Level(level - 1).Format != NumberFormat.None) return document.Styles.Outline.Level(level - 1);
        var resolved = new StyleResolver().ResolveParagraphStyle(document.Styles, DefaultStyleNames.HeadingLevel(level));
        return document.Styles.List.Find(resolved.ListStyleName)!.Level((resolved.ListLevel ?? 1) - 1);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void BordiSfondoEFiligranaDellaPaginaAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var page = document.Styles.Page.Find(document.InitialPageStyle)!;

        Assert.Equal(0xFFFFF8E1u, page.Background);
        var borders = page.Borders!;
        Assert.All(new[] { borders.Top, borders.Bottom, borders.Left, borders.Right }, line => Assert.Equal(BorderLineStyle.Double, line!.Style));
        Assert.Equal(10, borders.PaddingTop, 0);
        Assert.Equal(2.0, page.Margins.Left.ToCentimeters(), 1);
        var watermark = page.Watermark!;
        Assert.Equal("BOZZA", watermark.Text);
        Assert.Equal(45, watermark.Angle, 0);
        Assert.InRange(watermark.Transparency, 59, 61);
        Assert.False(page.Header.Enabled);
    }
}
