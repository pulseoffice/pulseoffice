using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Document.Fonts;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using CellVerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign;
using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class DocxRoundTripTests
{
    private readonly DirectFormatting _formatting = new();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument NewDocument()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PageStyle(DefaultStyleNames.Standard));
        return document;
    }

    private TextDocument RoundTrip(TextDocument document, DocxFlavour? flavour = null)
    {
        using var stream = new MemoryStream();
        var written = _composer.Writer(flavour ?? DocxFlavour.Document).Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        Assert.True(_composer.Reader(flavour ?? DocxFlavour.Document).CanRead(stream));
        var read = _composer.Reader(flavour ?? DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private ParagraphBlock Paragraph(TextDocument document, params TextRun[] runs) =>
        new(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns(runs));

    [Fact]
    public void IlTestoELaFormattazioneDellePorzioniSopravvivono()
    {
        var document = NewDocument();
        var bold = _formatting.Set(new TextRun("Grassetto"), new CharacterProperties { Bold = true, FontSize = 14, Color = 0xFFC0392B, Font = "Liberation Sans" });
        var plain = new TextRun(" normale\tcon tab ");
        var italic = _formatting.Set(new TextRun("corsivo"), new CharacterProperties
        {
            Italic = true, DoubleUnderline = true, Strikethrough = true, SmallCaps = true, Baseline = TextBaseline.Superscript,
            Highlight = 0xFFFFFF00, Language = "it-IT", Shadow = true, Outline = true,
        });
        document.Body.Add(Paragraph(document, bold, plain, italic));

        var read = RoundTrip(document);
        var paragraph = Assert.IsType<ParagraphBlock>(read.Body[0]);
        Assert.Equal("Grassetto normale\tcon tab corsivo", paragraph.Text);
        Assert.Equal(3, paragraph.Content.Runs.Count);
        Assert.Equal(_formatting.Run(bold), _formatting.Run(paragraph.Content.Runs[0]));
        Assert.Equal(_formatting.Run(italic), _formatting.Run(paragraph.Content.Runs[2]));
    }

    [Fact]
    public void LeProprietaDelParagrafoSopravvivono()
    {
        var document = NewDocument();
        var properties = new ParagraphProperties
        {
            Align = TextAlign.Justify,
            SpaceBefore = 6,
            SpaceAfter = 12,
            MarginLeft = 36,
            MarginRight = 18,
            Indent = -18,
            KeepWithNext = true,
            KeepTogether = true,
            PageBreakBefore = false,
            LineHeight = ParagraphLineHeight.Proportional(1.5),
            ContextualSpacing = true,
            Background = 0xFFEEEEEE,
            Borders = BoxBorders.Uniform(new BorderLine(1, 0xFF0000FF), 4) with { Left = null, Top = new BorderLine(1.5, 0xFF00FF00) { Style = BorderLineStyle.Dashed } },
            Hyphenation = ParagraphHyphenation.Off,
            TabStops = [new TabStop(72) { Alignment = TabAlignment.Right, Leader = TabLeader.Dots }],
        };
        var paragraph = new ParagraphBlock(document.NextId("p"), _formatting.Set(new TextParagraph("x") { StyleName = DefaultStyleNames.TextBody }, properties).WithRuns([new TextRun("x")]));
        document.Body.Add(paragraph);

        var back = RoundTrip(document);
        var read = Assert.IsType<ParagraphBlock>(back.Body[0]);
        Assert.Equal(DefaultStyleNames.TextBody, read.StyleName);
        Assert.Equal(_formatting.Paragraph(paragraph.Content), _formatting.Paragraph(read.Content) with { TabStops = paragraph.Content.TabStops });
        Assert.False(back.Styles.TabsRelativeToIndent);
        Assert.Equal([.. paragraph.Content.TabStops!.Select(tab => tab with { Position = tab.Position + 36 })], read.Content.TabStops);
    }

    [Fact]
    public void GliStiliConEreditaritaSopravvivono()
    {
        var document = NewDocument();
        document.Styles.Paragraph.Replace(new ParagraphStyle("Mio titolo")
        {
            ParentName = DefaultStyleNames.HeadingLevel(1),
            NextStyleName = DefaultStyleNames.TextBody,
            Paragraph = new ParagraphProperties { SpaceAfter = 20, Align = TextAlign.Center },
            Character = new CharacterProperties { Italic = true, Color = 0xFF112233 },
            OutlineLevel = 2,
        });
        document.Styles.Character.Replace(new CharacterStyle("Rosso") { Character = new CharacterProperties { Color = 0xFFFF0000 } });
        document.Styles.DefaultCharacter = document.Styles.DefaultCharacter with { FontSize = 11, Font = "Carlito" };
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("T") { StyleName = "Mio titolo" }.WithRuns([new TextRun("T") { StyleName = "Rosso" }])));

        var read = RoundTrip(document);
        var style = read.Styles.Paragraph.Find("Mio titolo");
        Assert.NotNull(style);
        Assert.Equal(DefaultStyleNames.HeadingLevel(1), style.ParentName);
        Assert.Equal(DefaultStyleNames.TextBody, style.NextStyleName);
        Assert.Equal(2, style.OutlineLevel);
        Assert.Equal(TextAlign.Center, style.Paragraph.Align);
        Assert.Equal(20, style.Paragraph.SpaceAfter);
        Assert.Equal(0xFF112233u, style.Character.Color);
        Assert.Equal(0xFFFF0000u, read.Styles.Character.Find("Rosso")!.Character.Color);
        Assert.Equal(11, read.Styles.DefaultCharacter.FontSize);
        Assert.Equal("Carlito", read.Styles.DefaultCharacter.Font);
        var paragraph = Assert.IsType<ParagraphBlock>(read.Body[0]);
        Assert.Equal("Mio titolo", paragraph.StyleName);
        Assert.Equal("Rosso", paragraph.Content.Runs[0].StyleName);
    }

    [Fact]
    public void GliElenchiSopravvivono()
    {
        var document = NewDocument();
        var levels = Enumerable.Range(0, ListStyle.LevelCount)
            .Select(level => new ListLevel(level == 0 ? NumberFormat.Arabic : NumberFormat.LowerRoman) { Suffix = ")", Indent = 18 * level, Hanging = 18 })
            .ToArray();
        document.Styles.List.Replace(new ListStyle("Numerato", levels));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("uno") { ListStyleName = "Numerato", Numbered = true, StyleName = DefaultStyleNames.Standard }.WithRuns([new TextRun("uno")])));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("due") { ListStyleName = "Numerato", Numbered = true, BulletLevel = 1, StyleName = DefaultStyleNames.Standard }.WithRuns([new TextRun("due")])));

        var read = RoundTrip(document);
        var list = read.Styles.List.Find("Numerato");
        Assert.NotNull(list);
        Assert.Equal(NumberFormat.LowerRoman, list.Level(1).Format);
        Assert.Equal(")", list.Level(0).Suffix);
        var second = Assert.IsType<ParagraphBlock>(read.Body[1]);
        Assert.Equal("Numerato", second.Content.ListStyleName);
        Assert.Equal(1, second.Content.BulletLevel);
        Assert.True(second.Content.Numbered);
    }

    [Fact]
    public void LeTabelleConUnioniSopravvivono()
    {
        var document = NewDocument();
        TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock(document.NextId("p"), text, DefaultStyleNames.Standard)]));
        TableCell Covered() => new(new BlockContainer()) { Covered = true };
        var origin = Cell("A");
        origin.ColumnSpan = 2;
        origin.RowSpan = 2;
        origin.Background = 0xFFDDEEFF;
        origin.VerticalAlign = CellVerticalAlign.Middle;
        var rows = new[]
        {
            new TableRow([origin, Covered(), Cell("C")]),
            new TableRow([Covered(), Covered(), Cell("F")]) { Height = Twip.FromCentimeters(1), MinimumHeight = false, AllowBreakAcrossPages = false },
            new TableRow([Cell("G"), Cell("H"), Cell("I")]),
        };
        document.Body.Add(new TableBlock(document.NextId("t"), rows, [new Twip(1000), new Twip(2000), new Twip(3000)]) { HeaderRows = 1 });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.Standard));

        var table = Assert.IsType<TableBlock>(RoundTrip(document).Body[0]);
        Assert.Equal([1000, 2000, 3000], table.Columns.Select(column => column.Value));
        Assert.Equal(1, table.HeaderRows);
        var read = table.Cell(0, 0);
        Assert.Equal(2, read.ColumnSpan);
        Assert.Equal(2, read.RowSpan);
        Assert.Equal(0xFFDDEEFFu, read.Background);
        Assert.Equal(CellVerticalAlign.Middle, read.VerticalAlign);
        Assert.True(table.Cell(1, 0).Covered);
        Assert.True(table.Cell(1, 1).Covered);
        Assert.Equal("F", ((ParagraphBlock)table.Cell(1, 2).Content[0]).Text);
        Assert.Equal(Twip.FromCentimeters(1), table.Rows[1].Height);
        Assert.False(table.Rows[1].MinimumHeight);
        Assert.False(table.Rows[1].AllowBreakAcrossPages);
        Assert.Equal("I", ((ParagraphBlock)table.Cell(2, 2).Content[0]).Text);
    }

    [Fact]
    public void PaginaIntestazioneESezioniSopravvivono()
    {
        var document = NewDocument();
        var standard = document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        standard.Margins = new Margins(Twip.FromCentimeters(3), Twip.FromCentimeters(2), Twip.FromCentimeters(2.5), Twip.FromCentimeters(2));
        standard.Header.Enabled = true;
        standard.Header.Default.Replace([new ParagraphBlock(document.NextId("p"), "Intestazione", DefaultStyleNames.Header)]);
        standard.FirstPageDifferent = true;
        standard.Header.First = new BlockContainer([new ParagraphBlock(document.NextId("p"), "Prima", DefaultStyleNames.Header)]);
        var landscape = new PageStyle(DefaultStyleNames.Landscape) { Orientation = PageOrientation.Landscape, NumberFormat = NumberFormat.UpperRoman };
        landscape.Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { Separator = true };
        document.Styles.Page.Replace(landscape);
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "Primo", DefaultStyleNames.Standard));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("Secondo") { StyleName = DefaultStyleNames.Standard, PageStyleName = DefaultStyleNames.Landscape, PageNumberRestart = 5 }.WithRuns([new TextRun("Secondo")])));

        var read = RoundTrip(document);
        var first = read.Styles.Page.Find(read.InitialPageStyle)!;
        Assert.Equal(standard.Margins, first.Margins);
        Assert.Equal(standard.Width, first.Width);
        Assert.True(first.Header.Enabled);
        Assert.True(first.FirstPageDifferent);
        Assert.Equal("Intestazione", ((ParagraphBlock)first.Header.Default[0]).Text);
        Assert.Equal("Prima", ((ParagraphBlock)first.Header.First![0]).Text);
        var second = Assert.IsType<ParagraphBlock>(read.Body[1]);
        Assert.NotNull(second.Content.PageStyleName);
        Assert.Equal(5, second.Content.PageNumberRestart);
        var converted = read.Styles.Page.Find(second.Content.PageStyleName)!;
        Assert.Equal(PageOrientation.Landscape, converted.Orientation);
        Assert.Equal(landscape.Width, converted.Width);
        Assert.Equal(NumberFormat.UpperRoman, converted.NumberFormat);
        Assert.Equal(2, converted.Columns.Count);
        Assert.True(converted.Columns.Separator);
    }

    [Fact]
    public void NoteCommentiSegnalibriCampiECollegamentiSopravvivono()
    {
        var document = NewDocument();
        var note = new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock(document.NextId("p"), "Nota", DefaultStyleNames.Footnote)]));
        document.Notes.Add(note);
        var endnote = new Note("n2", NoteKind.Endnote, new BlockContainer([new ParagraphBlock(document.NextId("p"), "Finale", DefaultStyleNames.Endnote)]));
        document.Notes.Add(endnote);
        var date = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        document.Comments.Add(new Comment("c1", "Ada", date, new BlockContainer([new ParagraphBlock(document.NextId("p"), "Da rivedere", DefaultStyleNames.Standard)])) { Initials = "AL" });
        document.Comments.Add(new Comment("c2", "Bruno", date, new BlockContainer([new ParagraphBlock(document.NextId("p"), "Fatto", DefaultStyleNames.Standard)])) { ParentId = "c1", Resolved = true });
        var paragraph = new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns(
        [
            new TextRun("Vedi "),
            new TextRun("il sito") { Hyperlink = "https://example.org/" },
            new TextRun(" pagina "),
            new TextRun("3") { Field = TextFieldKind.PageNumber },
            new TextRun(" fine"),
        ]))
        {
            Marks =
            [
                new BookmarkMark(0, "inizio", MarkEdge.Start),
                new CommentMark(0, "c1", MarkEdge.Start),
                new BookmarkMark(4, "inizio", MarkEdge.End),
                new CommentMark(4, "c1", MarkEdge.End),
                new NoteMark(11, "n1"),
                new NoteMark(25, "n2"),
            ],
        };
        document.Body.Add(paragraph);
        document.Variables["Cliente"] = "ACME";
        document.Properties.Title = "Relazione";

        var read = RoundTrip(document);
        var body = Assert.IsType<ParagraphBlock>(read.Body[0]);
        Assert.Equal(paragraph.Text, body.Text);
        Assert.Contains(body.Content.Runs, run => run.Hyperlink == "https://example.org/" && run.Text == "il sito");
        Assert.Contains(body.Content.Runs, run => run.Field == TextFieldKind.PageNumber && run.Text == "3");
        Assert.Contains(body.Marks, mark => mark is BookmarkMark { Name: "inizio", Edge: MarkEdge.Start, Offset: 0 });
        Assert.Contains(body.Marks, mark => mark is BookmarkMark { Name: "inizio", Edge: MarkEdge.End, Offset: 4 });
        var notes = body.Marks.OfType<NoteMark>().ToList();
        Assert.Equal([11, 25], notes.Select(mark => mark.Offset));
        Assert.Equal("Nota", ((ParagraphBlock)read.Notes.Find(notes[0].NoteId)!.Content[0]).Text);
        Assert.Equal(NoteKind.Endnote, read.Notes.Find(notes[1].NoteId)!.Kind);
        var commentMarks = body.Marks.OfType<CommentMark>().ToList();
        Assert.Equal(2, commentMarks.Count);
        var root = read.Comments.Find(commentMarks[0].CommentId)!;
        Assert.Equal("Ada", root.Author);
        Assert.Equal("AL", root.Initials);
        Assert.Equal(date, root.Date);
        var reply = Assert.Single(read.Comments.RepliesTo(root.Id));
        Assert.Equal("Bruno", reply.Author);
        Assert.True(reply.Resolved);
        Assert.Equal("ACME", read.Variables["Cliente"]);
        Assert.Equal("Relazione", read.Properties.Title);
    }

    [Fact]
    public void ModelliEDocumentiConMacroSiScrivonoESiRileggono()
    {
        foreach (var flavour in new[] { DocxFlavour.Template, DocxFlavour.MacroEnabled })
        {
            var document = NewDocument();
            document.Body.Add(new ParagraphBlock(document.NextId("p"), "Modello", DefaultStyleNames.Standard));
            Assert.Equal("Modello", ((ParagraphBlock)RoundTrip(document, flavour).Body[0]).Text);
        }
    }

    [Fact]
    public void LePerditeSonoDichiarate()
    {
        var document = NewDocument();
        document.Body.Add(new SectionBlock(document.NextId("s"), "Sezione", new BlockContainer([new ParagraphBlock(document.NextId("p"), "x", DefaultStyleNames.Standard)])) { Protected = true });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty).WithRuns([new TextRun("o") { Overline = true }])));
        var codes = _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Select(loss => loss.Code).ToList();
        Assert.DoesNotContain("DOCX003", codes);
        Assert.Contains("DOCX007", codes);
        var read = RoundTrip(document);
        var section = Assert.IsType<SectionBlock>(read.Body[0]);
        Assert.Equal("Sezione", section.Name);
        Assert.True(section.Protected);
        Assert.Equal("x", ((ParagraphBlock)section.Content[0]).Text);
        var table = NewDocument();
        table.Body.Add(new SectionBlock(table.NextId("s"), "Tabella", new BlockContainer([new TableBlock(table.NextId("t"), [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock(table.NextId("p"), "c", DefaultStyleNames.Standard)]))])], [PulseOffice.Foundation.Units.Twip.FromPoints(100)])])));
        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(table).Losses, loss => loss.Code == "DOCX003");
    }

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    [Fact]
    public void ImmaginiCaselleDiTestoFormeEGraficiSopravvivono()
    {
        var document = NewDocument();
        var paragraph = new ParagraphBlock(document.NextId("p"), "Testo con immagine", DefaultStyleNames.Standard);
        document.Body.Add(paragraph);
        var image = new AnchoredObject("obj1", new AsCharacterAnchor(paragraph.Id),
            new EmbeddedContent(new RasterImageObject { Id = "img", Data = Png, MediaType = "image/png", Description = "Logo" }),
            new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1))) { Name = "Logo" };
        var box = new AnchoredObject("obj2", new CharacterAnchor(paragraph.Id),
            new TextBoxContent(new BlockContainer([new ParagraphBlock(document.NextId("p"), "Nella casella", DefaultStyleNames.Standard)])),
            new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(3)))
        {
            Placement = new ObjectPlacement(Twip.FromCentimeters(1), Twip.FromCentimeters(2)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = WrapMode.Left,
            WrapSpacing = new Margins(Twip.FromPoints(5), Twip.FromPoints(6), Twip.FromPoints(7), Twip.FromPoints(8)),
            ZOrder = 2,
        };
        var shape = new AnchoredObject("obj3", new ParagraphAnchor(paragraph.Id),
            new EmbeddedContent(new ShapeObject { Id = "sh", Shape = ShapeKind.Ellipse, Text = "Ciao" }),
            new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(3))) { Wrap = WrapMode.Through, InBackground = true };
        var chart = new AnchoredObject("obj4", new PageAnchor(1),
            new EmbeddedContent(new ChartDefinition
            {
                Id = "ch", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
                InlineData = new ChartData(["A", "B"], [new ChartSeries("Serie", [1, 2])]),
            }),
            new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(5))) { Wrap = WrapMode.TopAndBottom };
        document.Objects.Add(image);
        document.Objects.Add(box);
        document.Objects.Add(shape);
        document.Objects.Add(chart);
        paragraph.Marks = [new ObjectMark(5, "obj1"), new ObjectMark(10, "obj2")];

        var read = RoundTrip(document);
        Assert.Equal(4, read.Objects.All.Count);
        var body = (ParagraphBlock)read.Body[0];
        Assert.Equal("Testo con immagine", body.Text);
        var marks = body.Marks.OfType<ObjectMark>().ToList();
        Assert.Equal([5, 10], marks.Select(mark => mark.Offset));
        var readImage = read.Objects.Find(marks[0].ObjectId)!;
        Assert.IsType<AsCharacterAnchor>(readImage.Anchor);
        var raster = Assert.IsType<RasterImageObject>(((EmbeddedContent)readImage.Content).Value);
        Assert.Equal(Png, raster.Data.ToArray());
        Assert.Equal("Logo", raster.Description);
        Assert.Equal(image.Size, readImage.Size);
        var readBox = read.Objects.Find(marks[1].ObjectId)!;
        Assert.IsType<CharacterAnchor>(readBox.Anchor);
        Assert.Equal(box.Placement, readBox.Placement);
        Assert.Equal(WrapMode.Left, readBox.Wrap);
        Assert.Equal(box.WrapSpacing, readBox.WrapSpacing);
        Assert.Equal(2, readBox.ZOrder);
        Assert.Equal("Nella casella", ((ParagraphBlock)((TextBoxContent)readBox.Content).Content[0]).Text);
        var readShape = read.Objects.All.Single(item => item.Content is EmbeddedContent { Value: ShapeObject });
        Assert.IsType<ParagraphAnchor>(readShape.Anchor);
        Assert.True(readShape.InBackground);
        Assert.Equal(WrapMode.Through, readShape.Wrap);
        var ellipse = (ShapeObject)((EmbeddedContent)readShape.Content).Value;
        Assert.Equal(ShapeKind.Ellipse, ellipse.Shape);
        Assert.Null(ellipse.Text);
        Assert.Equal("Ciao", ((ParagraphBlock)readShape.Content.Story![0]).Text);
        var readChart = read.Objects.All.Single(item => item.Content is EmbeddedContent { Value: ChartDefinition });
        Assert.IsType<PageAnchor>(readChart.Anchor);
        Assert.Equal(ChartType.Column, ((ChartDefinition)((EmbeddedContent)readChart.Content).Value).Type);
    }

    [Fact]
    public void LeRevisioniSopravvivonoSenzaEssereAccettate()
    {
        var document = NewDocument();
        var date = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        document.Changes.IsRecording = true;
        document.Changes.Add(new Insertion("ins", "Ada", date));
        document.Changes.Add(new Deletion("del", "Bruno", date, new BlockContainer([new ParagraphBlock("x", "vecchio ", DefaultStyleNames.Standard)])));
        var previous = new TextRun("Testo");
        document.Changes.Add(new FormatChange("fmt", "Carla", date, [previous]));
        var bold = _formatting.Set(new TextRun("Testo"), new CharacterProperties { Bold = true });
        var paragraph = new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns(
            [bold, new TextRun(" nuovo e fine")]))
        {
            Marks =
            [
                new ChangeMark(0, "fmt", MarkEdge.Start),
                new ChangeMark(5, "fmt", MarkEdge.End),
                new ChangeMark(6, "ins", MarkEdge.Start),
                new ChangeMark(11, "ins", MarkEdge.End),
                new ChangeMark(12, "del", MarkEdge.Point),
            ],
        };
        document.Body.Add(paragraph);
        var centred = new ParagraphBlock(document.NextId("p"),
            _formatting.Set(new TextParagraph("Centrato") { StyleName = DefaultStyleNames.Standard }, new ParagraphProperties { Align = TextAlign.Center }).WithRuns([new TextRun("Centrato")]));
        document.Changes.Add(new ParagraphFormatChange("par", "Dario", date, new TextParagraph("Centrato") { StyleName = DefaultStyleNames.Standard }));
        centred.Marks = [new ChangeMark(0, "par", MarkEdge.Point)];
        document.Body.Add(centred);

        var read = RoundTrip(document);
        Assert.True(read.Changes.IsRecording);
        var body = (ParagraphBlock)read.Body[0];
        Assert.Equal("Testo nuovo e fine", body.Text);
        var changes = body.Marks.OfType<ChangeMark>().ToList();
        var insertion = Assert.IsType<Insertion>(read.Changes.Find(changes.Single(mark => read.Changes.Find(mark.ChangeId) is Insertion && mark.Edge == MarkEdge.Start).ChangeId));
        Assert.Equal("Ada", insertion.Author);
        Assert.Equal(date, insertion.Date);
        Assert.Contains(changes, mark => mark.ChangeId == insertion.Id && mark.Edge == MarkEdge.Start && mark.Offset == 6);
        Assert.Contains(changes, mark => mark.ChangeId == insertion.Id && mark.Edge == MarkEdge.End && mark.Offset == 11);
        var deletion = read.Changes.All.OfType<Deletion>().Single();
        Assert.Equal("vecchio ", ((ParagraphBlock)deletion.Removed[0]).Text);
        Assert.Contains(changes, mark => mark.ChangeId == deletion.Id && mark.Edge == MarkEdge.Point && mark.Offset == 12);
        var format = read.Changes.All.OfType<FormatChange>().Single();
        Assert.False(format.PreviousRuns[0].Bold);
        Assert.Equal("Carla", format.Author);
        var paragraphChange = read.Changes.All.OfType<ParagraphFormatChange>().Single();
        Assert.Equal(TextAlign.Left, paragraphChange.Previous.Align);
        Assert.Equal(TextAlign.Center, ((ParagraphBlock)read.Body[1]).Content.Align);
    }

    [Fact]
    public void ICaratteriIncorporatiSiOffuscanoESiRileggono()
    {
        var document = NewDocument();
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "x", DefaultStyleNames.Standard));
        var regular = new PulseOffice.Typography.Tests.SyntheticFont().Build();
        document.Fonts = new DocumentFonts(false, [new EmbeddedFontFile("Prova Sans", 400, false, "a.ttf", regular)]);
        var read = RoundTrip(document);
        var file = Assert.Single(read.Fonts.Files);
        Assert.Equal("Prova Sans", file.Family);
        Assert.Equal(regular, file.Data);
    }

    [Fact]
    public void LeSezioniConColonneDiventanoSezioniContinue()
    {
        var document = NewDocument();
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "Prima", DefaultStyleNames.Standard));
        document.Body.Add(new SectionBlock(document.NextId("s"), "Colonne", new BlockContainer([new ParagraphBlock(document.NextId("p"), "Dentro", DefaultStyleNames.Standard)]))
        {
            Columns = new SectionColumns(3, Twip.FromCentimeters(0.5)),
        });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "Dopo", DefaultStyleNames.Standard));

        var read = RoundTrip(document);
        Assert.Equal(3, read.Body.Count);
        var section = Assert.IsType<SectionBlock>(read.Body[1]);
        Assert.Equal(3, section.Columns.Count);
        Assert.Equal(Twip.FromCentimeters(0.5), section.Columns.Gap);
        Assert.Equal("Dentro", ((ParagraphBlock)section.Content[0]).Text);
        Assert.Equal("Dopo", ((ParagraphBlock)read.Body[2]).Text);
        Assert.Equal(1, read.Styles.Page.Find(read.InitialPageStyle)!.Columns.Count);
    }

    [Fact]
    public void VociDIndiceERichiamiPersonalizzatiSopravvivono()
    {
        var document = NewDocument();
        document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock(document.NextId("p"), "Nota", DefaultStyleNames.Footnote)])) { Citation = "*" });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "Parola", DefaultStyleNames.Standard)
        {
            Marks = [new IndexEntryMark(0, "Voce \"citata\"", MarkEdge.Point), new NoteMark(6, "n1")],
        });
        var read = RoundTrip(document);
        var body = (ParagraphBlock)read.Body[0];
        Assert.Contains(body.Marks, mark => mark is IndexEntryMark { Entry: "Voce \"citata\"", Offset: 0 });
        Assert.Equal("*", read.Notes.All.Single().Citation);
    }

    [Fact]
    public void LImmagineSostitutivaDiUnOggettoFoglioSopravviveAlGiro()
    {
        var document = NewDocument();
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
    public void FormuleFileIncorporatiEControlliSopravvivonoInLinea()
    {
        var document = NewDocument();
        var paragraph = new ParagraphBlock(document.NextId("p"), "Prima dopo", DefaultStyleNames.Standard);
        document.Body.Add(paragraph);
        var size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(1));
        AnchoredObject Inline(string id, EmbeddedObject value) => new(id, new AsCharacterAnchor(paragraph.Id), new EmbeddedContent(value), size);
        document.Objects.Add(Inline("f", new FormulaObject { Id = "f", Expression = "frac {a} {b} + sqrt {x^2}" }));
        document.Objects.Add(Inline("o", new PackagedObject { Id = "o", FileName = "dati.csv", MediaType = "text/csv", Data = "a;b"u8.ToArray() }));
        document.Objects.Add(Inline("c1", new FormControlObject { Id = "c1", ControlKind = FormControlKind.CheckBox, Properties = new FormControlProperties { Name = "ok", Label = "Accetto", Value = new FormControlValue { Checked = CheckState.Checked } } }));
        document.Objects.Add(Inline("c2", new FormControlObject { Id = "c2", ControlKind = FormControlKind.ListBox, Properties = new FormControlProperties { Name = "scelta", Items = ["Uno", "Due"], Value = new FormControlValue { SelectedIndices = [1], Text = "Due" } } }));
        document.Objects.Add(Inline("c3", new FormControlObject { Id = "c3", ControlKind = FormControlKind.TextBox, Properties = new FormControlProperties { Name = "nome", Value = new FormControlValue { Text = "Ada" } } }));
        document.Objects.Add(Inline("c4", new FormControlObject { Id = "c4", ControlKind = FormControlKind.DateField, Properties = new FormControlProperties { Name = "data", Value = new FormControlValue { Text = "2026-10-01" } } }));
        paragraph.Marks = [new ObjectMark(5, "f"), new ObjectMark(5, "o"), new ObjectMark(5, "c1"), new ObjectMark(5, "c2"), new ObjectMark(6, "c3"), new ObjectMark(10, "c4")];

        var read = RoundTrip(document);
        var body = (ParagraphBlock)read.Body[0];
        Assert.Equal("Prima dopo", body.Text);
        var contents = body.Marks.OfType<ObjectMark>().Select(mark => ((EmbeddedContent)read.Objects.Find(mark.ObjectId)!.Content).Value).ToList();
        Assert.Equal(6, contents.Count);
        var formula = Assert.IsType<FormulaObject>(contents[0]);
        var parser = new PulseOffice.Formulas.FormulaParser();
        var linear = new PulseOffice.Formulas.LinearFormulaWriter();
        Assert.Equal(linear.Write(parser.Parse("frac {a} {b} + sqrt {x^2}") with { Source = string.Empty }), linear.Write(parser.Parse(formula.Expression) with { Source = string.Empty }));
        var ole = Assert.IsType<PackagedObject>(contents[1]);
        Assert.Equal("dati.csv", ole.FileName);
        Assert.Equal("text/csv", ole.MediaType);
        Assert.Equal("a;b"u8.ToArray(), ole.Data.ToArray());
        var check = Assert.IsType<FormControlObject>(contents[2]);
        Assert.Equal(CheckState.Checked, check.Properties.Value.Checked);
        Assert.Equal("Accetto", check.Properties.Label);
        var list = Assert.IsType<FormControlObject>(contents[3]);
        Assert.Equal(["Uno", "Due"], list.Properties.Items);
        Assert.Equal([1], list.Properties.Value.SelectedIndices);
        Assert.Equal("Ada", Assert.IsType<FormControlObject>(contents[4]).Properties.Value.Text);
        var date = Assert.IsType<FormControlObject>(contents[5]);
        Assert.Equal(FormControlKind.DateField, date.ControlKind);
        Assert.Equal("2026-10-01", date.Properties.Value.Text);
    }

    [Fact]
    public void TracciatiFontworkVettorialiEGruppiSopravvivono()
    {
        var document = NewDocument();
        var paragraph = new ParagraphBlock(document.NextId("p"), "x", DefaultStyleNames.Standard);
        document.Body.Add(paragraph);
        var size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(4));
        AnchoredObject Floating(string id, EmbeddedObject value) => new(id, new CharacterAnchor(paragraph.Id), new EmbeddedContent(value), size);
        var path = new FreeformPath([new PathFigure([new PathNode(new ShapePoint(0, 0)), new PathNode(new ShapePoint(1, 0)), new PathNode(new ShapePoint(0.5, 1))]) { Closed = true }]);
        document.Objects.Add(Floating("free", new FreeformObject { Id = "free", Path = path }));
        document.Objects.Add(Floating("word", new FontworkObject { Id = "word", Text = "Benvenuti", Warp = FontworkWarp.ArchUp, FontFamily = "Georgia" }));
        document.Objects.Add(Floating("vec", new VectorGraphicObject
        {
            Id = "vec",
            Asset = new VectorAsset { Id = "vec", Name = "vec", SourceKind = VectorSourceKind.InlineSvg, SvgContent = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><path d="M0 0 L10 0 L10 10 Z" fill="#FF0000"/><path d="M0 10 L5 5" fill="none" stroke="#0000FF" stroke-width="1"/></svg>""" },
        }));
        document.Objects.Add(Floating("grp", new ShapeGroupObject
        {
            Id = "grp",
            Children =
            [
                new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.5, 0.5),
                new GroupChild(new ShapeObject { Id = "b", Shape = ShapeKind.Ellipse }, 0.5, 0.5, 0.5, 0.5),
            ],
        }));
        paragraph.Marks = [new ObjectMark(0, "free"), new ObjectMark(0, "word"), new ObjectMark(1, "vec"), new ObjectMark(1, "grp")];

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX002");
        var read = RoundTrip(document);
        var contents = ((ParagraphBlock)read.Body[0]).Marks.OfType<ObjectMark>().Select(mark => ((EmbeddedContent)read.Objects.Find(mark.ObjectId)!.Content).Value).ToList();
        var free = Assert.IsType<FreeformObject>(contents[0]);
        Assert.InRange(free.Path.Figures[0].Nodes.Count, 3, 4);
        var word = Assert.IsType<FontworkObject>(contents[1]);
        Assert.Equal("Benvenuti", word.Text);
        Assert.Equal(FontworkWarp.ArchUp, word.Warp);
        Assert.Equal("Georgia", word.FontFamily);
        var vector = Assert.IsType<VectorGraphicObject>(contents[2]);
        Assert.Contains("#FF0000", vector.Asset.SvgContent);
        Assert.Contains("#0000FF", vector.Asset.SvgContent);
        var group = Assert.IsType<ShapeGroupObject>(contents[3]);
        Assert.Equal(2, group.Children.Count);
        Assert.Equal(ShapeKind.Ellipse, ((ShapeObject)group.Children[1].Content).Shape);
        Assert.Equal(0.5, group.Children[1].X, 3);
        Assert.Equal(0.5, group.Children[1].Width, 3);
    }

    [Fact]
    public void IlDuotonoDiUnImmagineSopravviveInDocxESiDichiaraPersoInOdt()
    {
        var document = NewDocument();
        var paragraph = new ParagraphBlock(document.NextId("p"), "Foto", DefaultStyleNames.Standard);
        document.Body.Add(paragraph);
        document.Objects.Add(new AnchoredObject("obj1", new AsCharacterAnchor(paragraph.Id),
            new EmbeddedContent(new RasterImageObject { Id = "img", Data = Png, MediaType = "image/png", Duotone = new ImageDuotone("#000080", "#FFFFFF") }),
            new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1))));
        paragraph.Marks = [new ObjectMark(2, "obj1")];

        var read = RoundTrip(document);
        var raster = read.Objects.All.Select(item => item.Content).OfType<EmbeddedContent>().Select(content => content.Value).OfType<RasterImageObject>().Single();

        Assert.Equal(new ImageDuotone("#000080", "#FFFFFF"), raster.Duotone);
        Assert.Contains("ODT008", new PulseOffice.Formats.Odf.Odt.OdtWriter().CheckFidelity(document).Losses.Select(loss => loss.Code));
    }
}
