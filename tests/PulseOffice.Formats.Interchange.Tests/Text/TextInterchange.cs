using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Formats.Interchange.Text.Outline;
using PulseOffice.Formats.Interchange.Text.Plain;
using PulseOffice.Formats.Interchange.Text.Rtf;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

internal sealed class TextInterchange
{
    public TextInterchange()
    {
        var direct = new DirectFormatting();
        var factory = new TextDocumentFactory(new DefaultStyleSheetFactory());
        var codePages = new CodePages();
        var detector = new TextEncodingDetector(codePages);
        var resolver = new StyleResolver();
        var semantics = new ParagraphSemantics(resolver, new StyleChain());
        var vocabulary = new RtfVocabulary();
        var header = new ImageHeader();
        Factory = factory;
        PlainReader = new PlainTextReader(detector, direct, factory);
        PlainWriter = new PlainTextWriter(semantics, "\n");
        var tree = new BlockTree();
        var threads = new PulseOffice.Documents.Editing.ListThreads(new PulseOffice.Documents.Editing.StoryCatalog(tree), tree, resolver);
        HtmlReader = new HtmlDocumentReader(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations()), detector, codePages, direct, factory, header, threads);
        HtmlWriter = new HtmlDocumentWriter(new HtmlPageRenderer(semantics, resolver, direct), new HtmlFidelity());
        RtfReader = new RtfDocumentReader(new RtfTokenizer(), codePages, vocabulary, direct, resolver, factory, header, threads);
        RtfWriter = new RtfDocumentWriter(semantics, resolver, direct, vocabulary);
    }

    public TextDocumentFactory Factory { get; }
    public PlainTextReader PlainReader { get; }
    public PlainTextWriter PlainWriter { get; }
    public HtmlDocumentReader HtmlReader { get; }
    public HtmlDocumentWriter HtmlWriter { get; }
    public RtfDocumentReader RtfReader { get; }
    public RtfDocumentWriter RtfWriter { get; }

    public static byte[] Bytes(TextDocument document, ITextDocumentWriter writer)
    {
        using var stream = new MemoryStream();
        var written = writer.Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        return stream.ToArray();
    }

    public static TextDocument Read(byte[] data, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream(data);
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    public static TextDocument Rich()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([]);
        document.Properties.Title = "Relazione";
        document.Properties.Author = "Ada";

        var heading = new ParagraphBlock("p1", new TextParagraph("Titolo") { StyleName = DefaultStyleNames.HeadingLevel(1), Runs = [new TextRun("Titolo")] })
        {
            Marks = [new BookmarkMark(0, "inizio", MarkEdge.Point)],
        };
        TextRun[] runs =
        [
            new("Ciao ") { Bold = true },
            new("bel") { Italic = true, Underline = true, Color = 0xFFFF0000, Hyperlink = "https://example.org/a?b=1&c=2" },
            new(" mondo è") { Strikethrough = true, Baseline = TextBaseline.Superscript, FontSize = 9, Font = "Liberation Sans", Highlight = 0xFFFFFF00 },
        ];
        var body = new ParagraphBlock("p2", new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.TextBody, MarginLeft = 12, Indent = -6 }.WithRuns(runs)
            with { Explicit = ParagraphOverrides.Align, Align = TextAlign.Center })
        {
            Marks = [new NoteMark(4, "ftn1"), new ObjectMark(10, "img1")],
        };
        var list1 = new ParagraphBlock("p3", new TextParagraph("Primo") { Runs = [new TextRun("Primo")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true });
        var list2 = new ParagraphBlock("p4", new TextParagraph("Secondo") { Runs = [new TextRun("Secondo")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, BulletLevel = 1 });
        var bullet = new ParagraphBlock("p4b", new TextParagraph("Punto") { Runs = [new TextRun("Punto")], ListStyleName = DefaultStyleNames.List1, Bullet = true });
        var fields = new ParagraphBlock("p5", new TextParagraph(string.Empty).WithRuns([new TextRun("Pagina "), new TextRun("1") { Field = TextFieldKind.PageNumber }, new TextRun(" di "), new TextRun("3") { Field = TextFieldKind.PageCount }]));
        var table = new TableBlock("tbl1",
        [
            new TableRow([Heading("A"), Heading("B"), Heading("C")]),
            new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("tc3", "Unita", DefaultStyleNames.TableContents)])) { ColumnSpan = 2, Background = 0xFFCCFFCC }, new TableCell(new BlockContainer()) { Covered = true }, Cell("D")]),
        ], [Twip.FromCentimeters(5), Twip.FromCentimeters(7), Twip.FromCentimeters(3)])
        {
            HeaderRows = 1,
        };
        var tail = new ParagraphBlock("p7", new TextParagraph(string.Empty)
        {
            SpaceBefore = 6,
            SpaceAfter = 12,
            KeepWithNext = true,
            PageBreakBefore = true,
            Explicit = ParagraphOverrides.SpaceBefore | ParagraphOverrides.SpaceAfter,
        }.WithRuns([new TextRun("Fine\tqui  doppio") { StyleName = DefaultStyleNames.Emphasis }, new TextRun("\na capo")]))
        {
            Marks = [new NoteMark(4, "end1")],
        };

        document.Body.Replace([heading, body, list1, list2, bullet, fields, table, tail]);
        document.Notes.Add(new Note("ftn1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np1", "Nota a piè", DefaultStyleNames.Footnote)])));
        document.Notes.Add(new Note("end1", NoteKind.Endnote, new BlockContainer([new ParagraphBlock("np2", "Nota finale", DefaultStyleNames.Endnote)])));
        document.Objects.Add(new AnchoredObject("img1", new AsCharacterAnchor("p2"), new EmbeddedContent(new RasterImageObject
        {
            Id = "raster1",
            Data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="),
            MediaType = "image/png",
            PixelWidth = 1,
            PixelHeight = 1,
            NaturalSize = new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1)),
        }), new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1))));
        document.Objects.Add(new AnchoredObject("box1", new ParagraphAnchor("p7"), new TextBoxContent(new BlockContainer([new ParagraphBlock("bp1", "Nel riquadro", DefaultStyleNames.FrameContents)])),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3))));
        return document;
    }

    private static TableCell Heading(string text) => new(new BlockContainer([new ParagraphBlock("th" + text, text, DefaultStyleNames.TableHeading)]));

    private static TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock("tc" + text, text, DefaultStyleNames.TableContents)]));
}
