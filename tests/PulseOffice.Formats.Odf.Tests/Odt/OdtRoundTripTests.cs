using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtRoundTripTests
{
    private readonly TextDocumentDescriber _describer = new();

    internal static TextDocument Sample()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([]);
        document.Properties.Title = "Relazione";
        document.Properties.Author = "Ada";
        document.Properties.Keywords.Add("prova");
        document.Language = "it-IT";
        document.Variables["Cliente"] = "Rossi";
        document.InitialPageStyle = DefaultStyleNames.FirstPage;

        var sheet = document.Styles;
        sheet.Paragraph.Add(new ParagraphStyle("Corpo speciale")
        {
            ParentName = DefaultStyleNames.TextBody,
            DisplayName = "Speciale",
            Paragraph = new ParagraphProperties
            {
                Align = TextAlign.Justify,
                LineHeight = ParagraphLineHeight.AtLeast(14),
                Hyphenation = new ParagraphHyphenation { MinimumWordLength = 6, MaximumConsecutive = 2 },
                Background = 0xFFEEEEEE,
                Borders = BoxBorders.Uniform(new BorderLine(1, 0xFF336699), 3) with { Bottom = new BorderLine(2, 0xFF000000) { Style = BorderLineStyle.Double }, PaddingLeft = 5 },
                TabStops = [new TabStop(100) { Alignment = TabAlignment.Decimal, DecimalCharacter = ',', Leader = TabLeader.Dots }],
            },
            Character = new CharacterProperties { Font = "Liberation Sans", Color = 0xFF112233, SmallCaps = true },
        });
        var first = sheet.Page.Find(DefaultStyleNames.FirstPage)!;
        first.Header.Enabled = true;
        first.Header.Default.Add(new ParagraphBlock("hp1", "Intestazione", DefaultStyleNames.Header));
        first.FirstPageDifferent = true;
        first.Header.First = new BlockContainer([new ParagraphBlock("hp2", "Solo prima", DefaultStyleNames.Header)]);
        first.Footer.Enabled = true;
        first.Footer.Default.Add(new ParagraphBlock("fp1", new TextParagraph("Pagina 1") { StyleName = DefaultStyleNames.Footer, Runs = [new TextRun("Pagina "), new TextRun("1") { Field = TextFieldKind.PageNumber }] }));
        first.Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { Separator = true };

        var heading = new ParagraphBlock("p1", new TextParagraph("Titolo") { StyleName = DefaultStyleNames.HeadingLevel(1), Runs = [new TextRun("Titolo")] })
        {
            Marks = [new BookmarkMark(0, "inizio", MarkEdge.Start), new BookmarkMark(6, "inizio", MarkEdge.End)],
        };

        var body = new ParagraphBlock("p2", new TextParagraph("Ciao  bel mondo ")
        {
            StyleName = "Corpo speciale",
            MarginLeft = 12.5,
            Indent = -6,
            KeepWithNext = true,
            PageNumberRestart = 3,
            Runs =
            [
                new TextRun("Ciao  ") { Bold = true },
                new TextRun("bel") { Italic = true, Underline = true, Color = 0xFFFF0000, Hyperlink = "https://example.org", StyleName = DefaultStyleNames.Emphasis },
                new TextRun(" mondo ") { Bold = false, Explicit = RunOverrides.Bold, Baseline = TextBaseline.Superscript, Language = "en-US", FontSize = 9, Highlight = 0xFFFFFF00 },
            ],
        }.WithRuns(
            [
                new TextRun("Ciao  ") { Bold = true },
                new TextRun("bel") { Italic = true, Underline = true, Color = 0xFFFF0000, Hyperlink = "https://example.org", StyleName = DefaultStyleNames.Emphasis },
                new TextRun(" mondo ") { Bold = false, Explicit = RunOverrides.Bold, Baseline = TextBaseline.Superscript, Language = "en-US", FontSize = 9, Highlight = 0xFFFFFF00 },
            ]) with { Explicit = ParagraphOverrides.Align, Align = TextAlign.Center })
        {
            Marks =
            [
                new CommentMark(0, "c1", MarkEdge.Start),
                new NoteMark(4, "ftn1"),
                new ChangeMark(6, "ct1", MarkEdge.Start),
                new ChangeMark(9, "ct1", MarkEdge.End),
                new CommentMark(9, "c1", MarkEdge.End),
                new ObjectMark(10, "img1"),
                new IndexEntryMark(11, "mondo", MarkEdge.Point),
                new ChangeMark(15, "ct2", MarkEdge.Point),
            ],
        };

        var list1 = new ParagraphBlock("p3", new TextParagraph("Primo") { Runs = [new TextRun("Primo")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true });
        var list2 = new ParagraphBlock("p4", new TextParagraph("Secondo") { Runs = [new TextRun("Secondo")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, BulletLevel = 1 });
        var fields = new ParagraphBlock("p5", new TextParagraph("Rossi 3") { Runs = [new TextRun("Rossi") { Field = TextFieldKind.Variable, FieldArgument = "Cliente" }, new TextRun(" "), new TextRun("3") { Field = TextFieldKind.PageCount }] })
        {
            Marks = [new CommentMark(0, "c2", MarkEdge.Point)],
        };

        var table = new TableBlock("tbl1",
        [
            new TableRow([Cell("A"), Cell("B")]) { Height = Twip.FromCentimeters(1), MinimumHeight = false },
            new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("tc3", "Unita")])) { ColumnSpan = 2, Background = 0xFFCCFFCC, VerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign.Middle }, new TableCell(new BlockContainer()) { Covered = true }]) { AllowBreakAcrossPages = false },
        ], [Twip.FromCentimeters(5), Twip.FromCentimeters(7)])
        {
            Name = "Dati",
            StyleName = DefaultStyleNames.DefaultTableStyle,
            HeaderRows = 1,
        };

        var section = new SectionBlock("sec1", "Colonne", new BlockContainer([new ParagraphBlock("p6", "Dentro la sezione")]))
        {
            Columns = new SectionColumns(3, Twip.FromCentimeters(0.5)) { Balanced = false },
            Protected = true,
        };

        var tail = new ParagraphBlock("p7", new TextParagraph("Fine\tqui") { Runs = [new TextRun("Fine\tqui")], PageStyleName = DefaultStyleNames.Landscape, PageBreakBefore = true });

        document.Body.Replace([heading, body, list1, list2, fields, table, section, tail]);

        document.Notes.Add(new Note("ftn1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np1", "Nota a piè", DefaultStyleNames.Footnote)])) { Citation = "*" });
        document.Comments.Add(new Comment("c1", "Bea", new DateTimeOffset(2026, 3, 4, 10, 30, 0, TimeSpan.Zero), new BlockContainer([new ParagraphBlock("cp1", "Da rivedere")])) { Initials = "BB" });
        document.Comments.Add(new Comment("c3", "Carlo", new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero), new BlockContainer([new ParagraphBlock("cp3", "Fatto")])) { ParentId = "c1", Resolved = true });
        document.Comments.Add(new Comment("c2", "Bea", new DateTimeOffset(2026, 3, 4, 11, 0, 0, TimeSpan.Zero), new BlockContainer([new ParagraphBlock("cp2", "Punto")])));
        document.Changes.IsRecording = true;
        document.Changes.ProtectionKey = [1, 2, 3, 4];
        document.Changes.Add(new Insertion("ct1", "Ada", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)) { Comment = "aggiunta" });
        document.Changes.Add(new Deletion("ct2", "Ada", new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero), new BlockContainer([new ParagraphBlock("dp1", "tolto")])));

        document.Objects.Add(new AnchoredObject("img1", new AsCharacterAnchor("p2"), new EmbeddedContent(Mapped(new RasterImageObject
        {
            Id = "raster1",
            Data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="),
            MediaType = "image/png",
            PixelWidth = 10,
            PixelHeight = 20,
            Title = "Logo",
            Description = "Il logo",
            Filter = ImageFilter.Grayscale,
            Crop = (0.1, 0.2, 0.05, 0.1),
        })), new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1)))
        {
            Name = "Logo",
            ZOrder = 2,
            WrapSpacing = Margins.Uniform(Twip.FromPoints(2)),
        });
        document.Objects.Add(new AnchoredObject("box1", new ParagraphAnchor("p6"), new TextBoxContent(new BlockContainer([new ParagraphBlock("bp1", "Nel riquadro")])),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)))
        {
            Placement = new ObjectPlacement(Twip.FromCentimeters(1), Twip.FromCentimeters(2)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = WrapMode.TopAndBottom,
            StyleName = DefaultStyleNames.Frame,
        });
        document.Objects.Add(new AnchoredObject("file1", new PageAnchor(2), new EmbeddedContent(new PackagedObject
        {
            Id = "pk1",
            FileName = "dati.csv",
            MediaType = "text/csv",
            Data = "a;b"u8.ToArray(),
        }), new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(3))) { InBackground = true, RotationDegrees = 15 });

        document.Macros.Store(new PulseOffice.Document.Forms.DocumentMacro("Saluta", PulseOffice.Document.Forms.MacroLanguage.JavaScript, "app.Log(1)"));
        document.FormSettings = new PulseOffice.Document.Forms.FormDocumentSettings(OpenInDesignMode: true);
        return document;
    }

    private static RasterImageObject Mapped(RasterImageObject image)
    {
        image.Map.Add(new PulseOffice.Document.Embedding.ImageMapArea { Id = "area1", Shape = PulseOffice.Document.Embedding.ImageMapShape.Rectangle, Points = [(0.1, 0.1), (0.5, 0.5)], Target = "https://example.org", Description = "Sito" });
        return image;
    }

    private static TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock("tc" + text, text, DefaultStyleNames.TableContents)]));

    internal static TextDocument RoundTrip(TextDocument document, ITextDocumentWriter? writer = null)
    {
        using var stream = new MemoryStream();
        var written = (writer ?? new OdtWriter()).Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    [Fact]
    public void LImmagineSostitutivaDiUnOggettoFoglioSopravviveAlGiro()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var paragraph = new ParagraphBlock(document.NextId("p"), "Prima dopo", DefaultStyleNames.Standard);
        document.Body.Add(paragraph);
        var picture = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 42 };
        document.Objects.Add(new AnchoredObject("o", new AsCharacterAnchor(paragraph.Id), new EmbeddedContent(new PackagedObject
        {
            Id = "o", FileName = "Foglio1.ods", MediaType = "application/vnd.oasis.opendocument.spreadsheet", Data = "ods"u8.ToArray(), Replacement = picture,
        }), new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2))));
        paragraph.Marks = [new ObjectMark(5, "o")];

        var read = RoundTrip(document);
        var mark = ((ParagraphBlock)read.Body[0]).Marks.OfType<ObjectMark>().Single();
        var ole = Assert.IsType<PackagedObject>(((EmbeddedContent)read.Objects.Find(mark.ObjectId)!.Content).Value);
        Assert.Equal(picture, ole.Replacement?.ToArray());
        Assert.Equal("ods"u8.ToArray(), ole.Data.ToArray());
    }

    [Fact]
    public void UnDocumentoCompletoTornaUgualeDalGiroInOdt()
    {
        var document = Sample();
        var expected = _describer.Describe(document);
        var actual = _describer.Describe(RoundTrip(document));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IlGiroInFodtEOttConservaIlDocumento()
    {
        Assert.Equal(_describer.Describe(Sample()), _describer.Describe(RoundTrip(Sample(), new OdtWriter(OdtFlavour.Template))));

        var flat = Sample();
        Assert.Contains(new FlatOdtWriter().CheckFidelity(flat).Losses, loss => loss.Code == "ODT004");
        foreach (var macro in flat.Macros.All.ToList()) flat.Macros.Remove(macro.Name);
        Assert.Equal(_describer.Describe(flat), _describer.Describe(RoundTrip(flat, new FlatOdtWriter())));
    }

    [Fact]
    public void IlLettoreRiconosceOdtFodtEOtt()
    {
        foreach (var writer in new ITextDocumentWriter[] { new OdtWriter(), new FlatOdtWriter(), new OdtWriter(OdtFlavour.Template) })
        {
            using var stream = new MemoryStream();
            writer.Write(Sample(), stream);
            stream.Position = 0;
            Assert.True(new OdtReader().CanRead(stream));
        }
    }

    [Fact]
    public void IlRegistroScegliePerEstensioneEPerContenuto()
    {
        var registry = new TextDocumentFormatRegistry([new OdtReader()], [new OdtWriter(), new FlatOdtWriter(), new OdtWriter(OdtFlavour.Template)], new PulseOffice.Formats.Intake.DocumentIntake());
        Assert.Equal("odt", registry.Native.Id);
        Assert.Equal("fodt", registry.WriterFor("a.fodt")!.Format.Id);
        Assert.Equal("ott", registry.WriterFor("a.ott")!.Format.Id);
        using var stream = new MemoryStream();
        new OdtWriter().Write(Sample(), stream);
        stream.Position = 0;
        Assert.True(registry.Read("senza-estensione", stream).Succeeded);
    }

    [Fact]
    public void LePerditeSonoDichiarate()
    {
        var document = Sample();
        document.Changes.Add(new FormatChange("ct9", "Ada", DateTimeOffset.UnixEpoch, []));
        var losses = new OdtWriter().CheckFidelity(document).Losses.Select(loss => loss.Code).ToList();
        Assert.Contains("ODT002", losses);
        Assert.DoesNotContain("ODT001", losses);
    }
}
