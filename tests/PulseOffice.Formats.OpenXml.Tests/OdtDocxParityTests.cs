using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.Odf.Tests.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OdtDocxParityTests
{
    private readonly TextDocumentDescriber _describer = new();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    internal static TextDocument Rich()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([]);
        document.Properties.Title = "Relazione";
        document.Properties.Author = "Ada";
        document.Language = "it-IT";

        var heading = new ParagraphBlock("p1", new TextParagraph("Titolo") { StyleName = DefaultStyleNames.HeadingLevel(1), Runs = [new TextRun("Titolo")] })
        {
            Marks = [new BookmarkMark(0, "inizio", MarkEdge.Start), new BookmarkMark(6, "inizio", MarkEdge.End)],
        };
        var runs = new TextRun[]
        {
            new("Ciao ") { Bold = true },
            new("bel") { Italic = true, Underline = true, Color = 0xFFFF0000, Hyperlink = "https://example.org" },
            new(" mondo") { Strikethrough = true, Baseline = TextBaseline.Superscript, FontSize = 9, Font = "Liberation Sans", Highlight = 0xFFFFFF00 },
        };
        var body = new ParagraphBlock("p2", new TextParagraph("Ciao bel mondo") { StyleName = DefaultStyleNames.TextBody, MarginLeft = 12, Indent = -6 }.WithRuns(runs)
            with { Explicit = ParagraphOverrides.Align, Align = TextAlign.Center })
        {
            Marks = [new CommentMark(0, "c1", MarkEdge.Start), new NoteMark(4, "ftn1"), new CommentMark(9, "c1", MarkEdge.End), new ObjectMark(10, "img1")],
        };
        var list1 = new ParagraphBlock("p3", new TextParagraph("Primo") { Runs = [new TextRun("Primo")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true });
        var list2 = new ParagraphBlock("p4", new TextParagraph("Secondo") { Runs = [new TextRun("Secondo")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, BulletLevel = 1 });
        var fields = new ParagraphBlock("p5", new TextParagraph("Pagina 1 di 3") { Runs = [new TextRun("Pagina "), new TextRun("1") { Field = TextFieldKind.PageNumber }, new TextRun(" di "), new TextRun("3") { Field = TextFieldKind.PageCount }] });
        var table = new TableBlock("tbl1",
        [
            new TableRow([Cell("A"), Cell("B")]),
            new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("tc3", "Unita", DefaultStyleNames.TableContents)])) { ColumnSpan = 2, Background = 0xFFCCFFCC }, new TableCell(new BlockContainer()) { Covered = true }]),
        ], [Twip.FromCentimeters(5), Twip.FromCentimeters(7)])
        {
            HeaderRows = 1,
        };
        var tail = new ParagraphBlock("p7", new TextParagraph("Fine\tqui")
        {
            Runs = [new TextRun("Fine\tqui") { StyleName = DefaultStyleNames.Emphasis }],
            SpaceBefore = 6,
            SpaceAfter = 12,
            KeepWithNext = true,
            KeepTogether = true,
            PageBreakBefore = true,
            Explicit = ParagraphOverrides.SpaceBefore | ParagraphOverrides.SpaceAfter,
            TabStops = [new TabStop(100) { Alignment = TabAlignment.Right, Leader = TabLeader.Dots }],
            LineHeight = ParagraphLineHeight.Proportional(1.5),
        })
        {
            Marks = [new NoteMark(4, "end1")],
        };

        var effects = new ParagraphBlock("p8", new TextParagraph("Largo stretto RILIEVO incavo nascosto").WithRuns(
        [
            new TextRun("Largo ") { LetterSpacing = 2, Kerning = false },
            new TextRun("stretto ") { ScaleWidth = 80, LetterSpacing = -0.5 },
            new TextRun("rilievo ") { Case = TextCase.Upper, Relief = FontRelief.Embossed },
            new TextRun("incavo ") { Relief = FontRelief.Engraved, Kerning = true },
            new TextRun("nascosto") { Hidden = true },
        ]));

        document.Body.Replace([heading, body, list1, list2, fields, table, tail, effects]);
        document.Notes.Add(new Note("ftn1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np1", "Nota a piè", DefaultStyleNames.Footnote)])));
        document.Notes.Add(new Note("end1", NoteKind.Endnote, new BlockContainer([new ParagraphBlock("np2", "Nota finale", DefaultStyleNames.Endnote)])));
        document.Notes.ContinuousEndnotes = true;
        document.Comments.Add(new Comment("c1", "Bea", new DateTimeOffset(2026, 3, 4, 10, 30, 0, TimeSpan.Zero), new BlockContainer([new ParagraphBlock("cp1", "Da rivedere")])) { Initials = "BB" });
        document.Objects.Add(new AnchoredObject("img1", new AsCharacterAnchor("p2"), new EmbeddedContent(new RasterImageObject
        {
            Id = "raster1",
            Data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="),
            MediaType = "image/png",
            PixelWidth = 1,
            PixelHeight = 1,
            NaturalSize = new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1)),
        }), new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1))));
        return document;
    }

    private static TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock("tc" + text, text, DefaultStyleNames.TableContents)]));

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        var written = writer.Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    internal string Content(TextDocument document)
    {
        var description = _describer.Describe(document);
        description = description[description.IndexOf("body:", StringComparison.Ordinal)..];
        var ids = Identifiers(document).Distinct().Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => "#" + pair.index);
        if (ids.Count > 0)
        {
            var pattern = new System.Text.RegularExpressions.Regex(@"(?<![\w#])(" + string.Join("|", ids.Keys.OrderByDescending(id => id.Length).Select(System.Text.RegularExpressions.Regex.Escape)) + @")(?!\w)");
            description = pattern.Replace(description, match => ids[match.Value]);
        }

        description = System.Text.RegularExpressions.Regex.Replace(description, @"(?m)^(\s+(image|packaged)) \S+", "$1 #content");
        return description.Replace(" style= outline=", " style=" + DefaultStyleNames.Standard + " outline=", StringComparison.Ordinal);
    }

    private IEnumerable<string> Identifiers(TextDocument document)
    {
        foreach (var id in Containers(document.Body)) yield return id;
        foreach (var note in document.Notes.All)
        {
            yield return note.Id;
            foreach (var id in Containers(note.Content)) yield return id;
        }
        foreach (var comment in document.Comments.All)
        {
            yield return comment.Id;
            foreach (var id in Containers(comment.Content)) yield return id;
        }
        foreach (var item in document.Objects.All.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            yield return item.Id;
            if (item.Content is TextBoxContent box)
                foreach (var id in Containers(box.Content)) yield return id;
        }
    }

    private IEnumerable<string> Containers(BlockContainer container)
    {
        foreach (var block in container.Blocks)
        {
            yield return block.Id;
            var inner = block switch
            {
                TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Containers(cell.Content)),
                SectionBlock section => Containers(section.Content),
                _ => [],
            };
            foreach (var id in inner) yield return id;
        }
    }

    [Fact]
    public void UnDocumentoRiccoDaLoStessoModelloInOdtEInDocx()
    {
        var odt = Through(Rich(), new OdtWriter(), new OdtReader());
        var docx = Through(Rich(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));
        Assert.Equal(Content(odt), Content(docx));
    }

    [Fact]
    public void DocxDichiaraSoloPerditeConosciute()
    {
        var losses = _composer.Writer(DocxFlavour.Document).CheckFidelity(Rich()).Losses;
        Assert.Empty(losses);
        Assert.Empty(new OdtWriter().CheckFidelity(Rich()).Losses);
    }
}
