using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ContentControlRoundTripTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ContentControlCatalog _catalog = new(new StoryCatalog(new BlockTree()), new BlockTree());

    internal static TextDocument Sample()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PageStyle(DefaultStyleNames.Standard));
        ParagraphBlock Add(string text, params InlineMark[] marks)
        {
            var paragraph = new ParagraphBlock(document.NextId("p"), text, DefaultStyleNames.Standard) { Marks = marks };
            document.Body.Add(paragraph);
            return paragraph;
        }
        Add("Nome: Ada", Start("c1", 6, new ContentControl { Kind = ContentControlKind.PlainText, Title = "Nome", Tag = "nome", NumericId = 101, ContentLocked = true, MultiLine = true }), End("c1", 9));
        Add("☒ accetto", Start("c2", 0, new ContentControl { Kind = ContentControlKind.CheckBox, Checked = true, CheckedSymbol = new CheckBoxSymbol("✔", "Segoe UI Symbol"), ControlLocked = true }), End("c2", 1));
        Add("Scelta: Due", Start("c3", 8, new ContentControl
        {
            Kind = ContentControlKind.DropDownList,
            Title = "Scelta",
            Items = [new ContentControlItem("Uno", "1"), new ContentControlItem("Due", "2")],
        }), End("c3", 11));
        Add("Città: Roma", Start("c4", 7, new ContentControl { Kind = ContentControlKind.ComboBox, Items = [new ContentControlItem("Roma", "RM"), new ContentControlItem("Milano", "MI")] }), End("c4", 11));
        Add("Data: 01/10/2026", Start("c5", 6, new ContentControl { Kind = ContentControlKind.Date, DateFormat = "dd/MM/yyyy", DateLanguage = "it-IT", Date = new DateTime(2026, 10, 1) }), End("c5", 16));
        Add("Fai clic qui", Start("c6", 0, new ContentControl { Kind = ContentControlKind.RichText, ShowingPlaceholder = true, PlaceholderText = "Fai clic qui", PlaceholderPart = "DefaultPlaceholder_1" }), End("c6", 12));
        Add("Primo blocco", Start("c7", 0, new ContentControl { Kind = ContentControlKind.RichText, Title = "Blocco", Block = true }));
        Add("Secondo blocco", End("c7", 14));
        var picture = Add("", Start("c8", 0, new ContentControl { Kind = ContentControlKind.Picture, Title = "Foto" }), new ObjectMark(0, "img"), End("c8", 0));
        var size = new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(2));
        document.Objects.Add(new AnchoredObject("img", new AsCharacterAnchor(picture.Id), new EmbeddedContent(new RasterImageObject { Id = "img", Data = Png, MediaType = "image/png", NaturalSize = size }), size));
        return document;
    }

    private static ContentControlMark Start(string id, int offset, ContentControl control) => new(offset, id, MarkEdge.Start) { Control = control };

    private static ContentControlMark End(string id, int offset) => new(offset, id, MarkEdge.End);

    private TextDocument ThroughDocx(TextDocument document)
    {
        using var stream = new MemoryStream();
        var written = _composer.Writer(DocxFlavour.Document).Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument ThroughOdt(TextDocument document)
    {
        using var stream = new MemoryStream();
        var written = new OdtWriter().Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private string Covered(TextDocument document, ContentControlSpan span)
    {
        var paragraphs = document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        var first = paragraphs.FindIndex(paragraph => paragraph.Id == span.Start.ParagraphId);
        var last = paragraphs.FindIndex(paragraph => paragraph.Id == span.End.ParagraphId);
        if (first == last) return paragraphs[first].Text[span.Start.Offset..span.End.Offset];
        return string.Join("|", paragraphs.Skip(first).Take(last - first + 1).Select((paragraph, index) =>
            index == 0 ? paragraph.Text[span.Start.Offset..] : index == last - first ? paragraph.Text[..span.End.Offset] : paragraph.Text));
    }

    public static TheoryData<string> Formats() => new() { "docx", "odt" };

    [Theory]
    [MemberData(nameof(Formats))]
    public void IControlliDiContenutoAttraversanoIlFormato(string format)
    {
        var original = Sample();
        var read = format == "docx" ? ThroughDocx(original) : ThroughOdt(original);
        var spans = _catalog.All(read);
        Assert.Equal(8, spans.Count);

        var name = spans[0].Control;
        Assert.Equal(ContentControlKind.PlainText, name.Kind);
        Assert.Equal(("Nome", "nome", 101, true, false, true), (name.Title, name.Tag, name.NumericId, name.ContentLocked, name.ControlLocked, name.MultiLine));
        Assert.Equal("Ada", Covered(read, spans[0]));

        var box = spans[1].Control;
        Assert.Equal(ContentControlKind.CheckBox, box.Kind);
        Assert.True(box.Checked);
        Assert.True(box.ControlLocked);
        Assert.False(box.ContentLocked);
        Assert.Equal(new CheckBoxSymbol("✔", "Segoe UI Symbol"), box.CheckedSymbol);
        Assert.Equal("☐", box.UncheckedSymbol.Character);

        var list = spans[2].Control;
        Assert.Equal(ContentControlKind.DropDownList, list.Kind);
        Assert.Equal([new ContentControlItem("Uno", "1"), new ContentControlItem("Due", "2")], list.Items);
        Assert.Equal("Due", Covered(read, spans[2]));

        Assert.Equal(ContentControlKind.ComboBox, spans[3].Control.Kind);
        Assert.Equal("Milano", spans[3].Control.Items[1].DisplayText);

        var date = spans[4].Control;
        Assert.Equal(ContentControlKind.Date, date.Kind);
        Assert.Equal(("dd/MM/yyyy", "it-IT", new DateTime(2026, 10, 1)), (date.DateFormat, date.DateLanguage, date.Date));
        Assert.Equal("01/10/2026", Covered(read, spans[4]));

        var placeholder = spans[5].Control;
        Assert.True(placeholder.ShowingPlaceholder);
        Assert.Equal("Fai clic qui", placeholder.PlaceholderText);
        Assert.Equal("DefaultPlaceholder_1", placeholder.PlaceholderPart);

        var block = spans.Single(span => span.Control.Title == "Blocco");
        Assert.True(block.Control.Block);
        Assert.Equal("Primo blocco|Secondo blocco", Covered(read, block));

        var picture = spans.Single(span => span.Control.Kind == ContentControlKind.Picture);
        var holder = read.Body.Blocks.OfType<ParagraphBlock>().Single(paragraph => paragraph.Id == picture.Start.ParagraphId);
        var image = holder.Marks.OfType<ObjectMark>().Single();
        var marks = holder.Marks.ToList();
        Assert.True(marks.IndexOf(image) > marks.FindIndex(mark => mark is ContentControlMark { Edge: MarkEdge.Start }));
        Assert.True(marks.IndexOf(image) < marks.FindIndex(mark => mark is ContentControlMark { Edge: MarkEdge.End }));
        Assert.IsType<RasterImageObject>(((EmbeddedContent)read.Objects.Find(image.ObjectId)!.Content).Value);
    }

    [Fact]
    public void IlDocxScriveSdtInLineaEDiParagrafo()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Sample(), stream).Succeeded);
        stream.Position = 0;
        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false);
        var body = package.MainDocumentPart!.Document!.Body!;
        Assert.Single(body.Elements<DocumentFormat.OpenXml.Wordprocessing.SdtBlock>());
        Assert.Equal(7, body.Descendants<DocumentFormat.OpenXml.Wordprocessing.SdtRun>().Count());
        var locked = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.SdtRun>().First().SdtProperties!.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Lock>()!;
        Assert.Equal(DocumentFormat.OpenXml.Wordprocessing.LockingValues.ContentLocked, locked.Val!.Value);
        var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365).Validate(package, TestContext.Current.CancellationToken)
            .Where(error => error.Node is not null && error.Node.Ancestors<DocumentFormat.OpenXml.Wordprocessing.SdtProperties>().Any() || error.Node is DocumentFormat.OpenXml.Wordprocessing.SdtProperties)
            .Select(error => error.Description).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void LOdtScriveIlControlloComeNelRiferimento()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Sample(), stream).Succeeded);
        stream.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(stream);
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());
        var content = System.Xml.Linq.XDocument.Load(reader);
        System.Xml.Linq.XNamespace loext = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";
        var controls = content.Descendants(loext + "content-control").ToList();
        Assert.Equal(9, controls.Count);
        Assert.Contains(controls, element => (string?)element.Attribute(loext + "checkbox") == "true" && (string?)element.Attribute(loext + "checked-state") == "✔" && (string?)element.Attribute(loext + "lock") == "sdtLocked");
        Assert.Contains(controls, element => (string?)element.Attribute(loext + "dropdown") == "true" && element.Elements(loext + "list-item").Count() == 2);
        Assert.Contains(controls, element => (string?)element.Attribute(loext + "date") == "true" && (string?)element.Attribute(loext + "current-date") == "2026-10-01T00:00:00Z");
        Assert.Contains(controls, element => (string?)element.Attribute(loext + "showing-place-holder") == "true");
        Assert.Contains(controls, element => (string?)element.Attribute(loext + "plain-text") == "true" && (string?)element.Attribute(loext + "alias") == "Nome" && (string?)element.Attribute(loext + "tag") == "nome");
    }

    [Fact]
    public void UnControlloACavalloDiParagrafiSiDichiaraESiRicompone()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PageStyle(DefaultStyleNames.Standard));
        document.Body.Add(new ParagraphBlock("a", "uno due", DefaultStyleNames.Standard) { Marks = [Start("c", 4, new ContentControl { Title = "Lungo" })] });
        document.Body.Add(new ParagraphBlock("b", "tre quattro", DefaultStyleNames.Standard) { Marks = [End("c", 3)] });

        Assert.Contains(new OdtWriter().CheckFidelity(document).Losses, loss => loss.Code == "ODT006");
        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX043");
        var sampleLosses = _composer.Writer(DocxFlavour.Document).CheckFidelity(Sample()).Losses;
        Assert.False(sampleLosses.Any(loss => loss.Code is "DOCX043" or "DOCX002"), string.Join(", ", sampleLosses.Select(loss => loss.Code)));
        Assert.Contains(new OdtWriter().CheckFidelity(Sample()).Losses, loss => loss.Code == "ODT006");

        var odt = _catalog.All(ThroughOdt(document));
        Assert.Single(odt);
        Assert.Equal("Lungo", odt[0].Control.Title);
        Assert.False(odt[0].InOneParagraph);

        var docx = _catalog.All(ThroughDocx(document));
        Assert.Equal(2, docx.Count);
        Assert.All(docx, span => Assert.Equal("Lungo", span.Control.Title));
    }

    [Fact]
    public void IlTestoSegnapostoDiUnControlloPienoSiDichiaraInDocx()
    {
        var document = Sample();
        var paragraph = document.Body.Blocks.OfType<ParagraphBlock>().First();
        paragraph.Marks = [.. paragraph.Marks.Select(mark => mark is ContentControlMark { Edge: MarkEdge.Start } start ? start with { Control = start.Control! with { PlaceholderText = "Scrivi il nome" } } : mark)];
        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX044");
        Assert.Equal("Scrivi il nome", _catalog.All(ThroughOdt(document))[0].Control.PlaceholderText);
    }

    public static TheoryData<string, int> ReferenceSuiteFiles() => new() { { "controlli.lo-da-odt.docx", 9 }, { "controlli.lo-da-docx.odt", 8 } };

    [Theory]
    [MemberData(nameof(ReferenceSuiteFiles))]
    public void UnAltraSuiteRiconosceIControlliCheScriviamo(string file, int expected)
    {
        using var stream = File.OpenRead(Path.Combine("Fixtures", "ContentControls", file));
        var read = file.EndsWith(".docx", StringComparison.Ordinal) ? _composer.Reader(DocxFlavour.Document).Read(stream) : new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        var spans = _catalog.All(read.Value!);
        Assert.Equal(expected, spans.Count);
        Assert.Equal(
            [ContentControlKind.PlainText, ContentControlKind.CheckBox, ContentControlKind.DropDownList, ContentControlKind.ComboBox, ContentControlKind.Date],
            spans.Take(5).Select(span => span.Control.Kind));
        Assert.Equal(("Nome", "nome", true), (spans[0].Control.Title, spans[0].Control.Tag, spans[0].Control.ContentLocked));
        Assert.Equal("Ada", Covered(read.Value!, spans[0]));
        Assert.True(spans[1].Control.Checked);
        Assert.True(spans[1].Control.ControlLocked);
        Assert.Equal("\u2714", spans[1].Control.CheckedSymbol.Character);
        Assert.Equal([new ContentControlItem("Uno", "1"), new ContentControlItem("Due", "2")], spans[2].Control.Items);
        Assert.Equal(("dd/MM/yyyy", new DateTime(2026, 10, 1)), (spans[4].Control.DateFormat, spans[4].Control.Date));
        Assert.True(spans[5].Control.ShowingPlaceholder);
        Assert.Contains(spans, span => span.Control is { Kind: ContentControlKind.Picture, Title: "Foto" });
        Assert.Contains(spans, span => span.Control.Title == "Blocco");
    }
}
