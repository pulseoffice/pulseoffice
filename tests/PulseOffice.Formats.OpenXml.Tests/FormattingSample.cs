using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

internal sealed class FormattingSample
{
    public TextDocument Build()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var effects = new ParagraphBlock("effects", new TextParagraph(string.Empty).WithRuns(
        [
            new TextRun("Largo ") { LetterSpacing = 2, Kerning = false },
            new TextRun("stretto ") { ScaleWidth = 80 },
            new TextRun("rilievo ") { Case = TextCase.Upper, Relief = FontRelief.Embossed },
            new TextRun("incavo ") { Relief = FontRelief.Engraved },
            new TextRun("nascosto") { Hidden = true },
            new TextRun(" marcato") { StyleName = "Marcato" },
        ]));
        var framed = new ParagraphBlock("framed", new TextParagraph("Paragrafo incorniciato con capolettera e tabulazioni\tqui").WithRuns(
            [new TextRun("Paragrafo incorniciato con capolettera e tabulazioni\tqui")]) with
        {
            Borders = BoxBorders.Uniform(new BorderLine(1, 0xFF336699), 4) with
            {
                Bottom = new BorderLine(2.25, 0xFF000000) { Style = BorderLineStyle.Double },
                Left = null,
                PaddingLeft = 0,
            },
            Background = 0xFFEEF2FF,
            DropCap = new DropCap(3, 1) { Distance = 6 },
            TabStops = [new TabStop(200) { Alignment = TabAlignment.Right, Leader = TabLeader.Dots }],
            KeepTogether = true,
            KeepWithNext = true,
            Widows = 2,
            Orphans = 2,
            PageBreakBefore = true,
        });
        document.Styles.Character.Add(new CharacterStyle("Marcato") { Character = new() { Bold = true, Color = 0xFFC00000, LetterSpacing = 1 } });
        var page = document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        page.Borders = BoxBorders.Uniform(new BorderLine(1.5, 0xFF1F4E79) { Style = BorderLineStyle.Double }, 10);
        page.Background = 0xFFFFF8E1;
        page.Watermark = new PageWatermark("BOZZA") { Font = "Liberation Sans", Color = 0xFF808080, Transparency = 60, Angle = 45 };
        document.Styles.Outline = new ListStyle(StyleSheet.OutlineStyleName,
            [.. Enumerable.Range(0, ListStyle.LevelCount).Select(level => new ListLevel(level < 2 ? NumberFormat.Arabic : NumberFormat.None) { Suffix = ".", DisplayLevels = level + 1, Hanging = 0 })]);
        var chapter = new ParagraphBlock("chapter", new TextParagraph("Capitolo primo") { StyleName = DefaultStyleNames.HeadingLevel(1) }.WithRuns([new TextRun("Capitolo primo")]));
        var section = new ParagraphBlock("section", new TextParagraph("Sezione prima") { StyleName = DefaultStyleNames.HeadingLevel(2) }.WithRuns([new TextRun("Sezione prima")]));
        ParagraphBlock Item(string id, string text, int level, int? restart = null, bool header = false) =>
            new(id, new TextParagraph(text)
            {
                ListStyleName = DefaultStyleNames.Numbering123,
                BulletLevel = level,
                Numbered = !header,
                ListHeader = header,
                ListRestart = restart,
            }.WithRuns([new TextRun(text)]));
        document.Body.Replace([effects, framed, chapter, section,
            Item("i1", "Primo punto", 0), Item("i2", "Sottopunto", 1), Item("i3", "Secondo punto", 0),
            new ParagraphBlock("gap", "Testo fra due elenchi", DefaultStyleNames.TextBody),
            new ParagraphBlock("column", new TextParagraph("Colonna nuova") { ColumnBreakBefore = true }.WithRuns([new TextRun("Colonna nuova")])),
            Item("i4", "Quinto punto", 0, 5), Item("i5", "Nota senza numero", 0, header: true), Item("i6", "Sesto punto", 0),
            new ParagraphBlock("restart", new TextParagraph("Numerazione da tre") { PageStyleName = DefaultStyleNames.Standard, PageNumberRestart = 3 }.WithRuns([new TextRun("Numerazione da tre")]))]);
        return document;
    }

    public ParagraphBlock Paragraph(TextDocument document, string text) =>
        Paragraphs(document.Body).First(paragraph => paragraph.Text.Contains(text, StringComparison.Ordinal));

    public TextRun Run(TextDocument document, string text) =>
        Paragraphs(document.Body).SelectMany(paragraph => paragraph.Content.Runs).First(run => run.Text.Contains(text, StringComparison.Ordinal));

    public IEnumerable<ParagraphBlock> Paragraphs(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        ParagraphBlock paragraph => [paragraph],
        SectionBlock section => Paragraphs(section.Content),
        TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Paragraphs(cell.Content)),
        _ => [],
    });
}
