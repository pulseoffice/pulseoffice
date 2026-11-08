using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

internal sealed class ReferenceSample
{
    private ParagraphBlock Paragraph(string id, string? style, params TextRun[] runs) =>
        new(id, new TextParagraph(string.Empty) { StyleName = style ?? DefaultStyleNames.TextBody }.WithRuns(runs));

    private SectionBlock Index(string id, IndexDefinition definition, params ParagraphBlock[] paragraphs) =>
        new(id, id, new BlockContainer(paragraphs)) { Index = definition, Protected = true };

    private TextRun Field(string shown, TextFieldKind kind, string? argument = null, FieldDetail? detail = null) =>
        new(shown) { Field = kind, FieldArgument = argument, FieldDetail = detail };

    public TextDocument Build()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Properties.Title = "Relazione";
        document.Properties.Custom["Cliente"] = "Rossi";
        document.Variables["lingua"] = "it";
        document.Bibliography.Store(new BibliographySource("Eco1977", "book", new Dictionary<string, string>
        {
            ["author"] = "Umberto Eco",
            ["title"] = "Come si fa una tesi di laurea",
            ["year"] = "1977",
            ["publisher"] = "Bompiani",
        }));

        var heading = Paragraph("heading", DefaultStyleNames.HeadingLevel(1), new TextRun("Capitolo importante"));
        heading.Marks = [new BookmarkMark(0, "cap", MarkEdge.Start), new BookmarkMark(8, "cap", MarkEdge.End)];
        var properties = Paragraph("properties", null,
            new TextRun("Titolo: "), Field("Relazione", TextFieldKind.Title),
            new TextRun(" cliente: "), Field("Rossi", TextFieldKind.CustomProperty, "Cliente"),
            new TextRun(" parole: "), Field("12", TextFieldKind.WordCount),
            new TextRun(" tabelle: "), Field("0", TextFieldKind.TableCount));
        var variables = Paragraph("variables", null,
            Field("5", TextFieldKind.VariableSet, "conto", new FieldDetail { Format = "5" }),
            new TextRun(" letto "), Field("5", TextFieldKind.Variable, "conto"),
            new TextRun(" utente "), Field("it", TextFieldKind.Variable, "lingua"),
            new TextRun(" scelta "), Field("cinque", TextFieldKind.ConditionalText, "conto == 5", new FieldDetail { WhenTrue = "cinque", WhenFalse = "altro" }),
            new TextRun(" fine"));
        var hidden = Paragraph("hidden", null,
            new TextRun("Visibile "), Field("nascosto", TextFieldKind.HiddenText, "conto == 5", new FieldDetail { WhenTrue = "nascosto" }) with { Hidden = true });
        var noted = Paragraph("noted", null, new TextRun("Testo con nota"));
        noted.Marks = [new NoteMark(14, "fn1")];
        document.Notes.Add(new Note("fn1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("fn1p", "La nota.", DefaultStyleNames.Footnote)])));
        var caption = Paragraph("caption", DefaultStyleNames.Caption,
            new TextRun("Figura "), Field("1", TextFieldKind.Sequence, "Figure", new FieldDetail { Format = "refFigure0" }), new TextRun(": il grafico"));
        var references = Paragraph("references", null,
            new TextRun("Vedi "), Field("Capitolo", TextFieldKind.Bookmark, "cap"),
            new TextRun(" a pagina "), Field("1", TextFieldKind.Bookmark, "cap", new FieldDetail { Format = ReferenceFormats.Page }),
            new TextRun(", "), Field("above", TextFieldKind.Bookmark, "cap", new FieldDetail { Format = ReferenceFormats.Direction }),
            new TextRun(", nota "), Field("1", TextFieldKind.NoteReference, "fn1"),
            new TextRun(", "), Field("Figura 1", TextFieldKind.Bookmark, "refFigure0", new FieldDetail { Format = ReferenceFormats.Category }),
            new TextRun("."));
        var fixedDate = Paragraph("fixed", null, new TextRun("Data: "), Field("01/01/2000", TextFieldKind.Date, detail: new FieldDetail { Fixed = true, Value = "2000-01-01" }));
        var citation = Paragraph("citation", null, new TextRun("Come in "), Field("[Eco1977]", TextFieldKind.Citation, "Eco1977"), new TextRun("."));
        caption.Marks = [new IndexEntryMark(12, "grafico", MarkEdge.Point)];
        var contents = Index("toc", new IndexDefinition(IndexKind.Contents) { Title = "Sommario", Levels = 3 },
            Paragraph("tocTitle", DefaultStyleNames.ContentsHeading, new TextRun("Sommario")),
            Paragraph("toc1", DefaultStyleNames.ContentsLevel(1), new TextRun("Capitolo importante") { Hyperlink = "#Capitolo importante|outline" }, new TextRun("\t"), new TextRun("1") { Hyperlink = "#Capitolo importante|outline" }));
        var alphabetical = Index("alpha", new IndexDefinition(IndexKind.Alphabetical) { LetterSeparators = true, Hyperlinks = false },
            Paragraph("alpha1", DefaultStyleNames.IndexLevel(1), new TextRun("grafico, 1")));
        var figures = Index("figures", new IndexDefinition(IndexKind.Illustrations) { Sequence = "Figure", Hyperlinks = false },
            Paragraph("figures1", DefaultStyleNames.IllustrationIndex1, new TextRun("Figura 1: il grafico\t1")));
        var bibliography = Index("biblio", new IndexDefinition(IndexKind.Bibliography) { Title = "Bibliografia", PageNumbers = false, Hyperlinks = false },
            Paragraph("biblioTitle", DefaultStyleNames.BibliographyHeading, new TextRun("Bibliografia")),
            Paragraph("biblio1", DefaultStyleNames.Bibliography1, new TextRun("[Eco1977] Umberto Eco, Come si fa una tesi di laurea, Bompiani, 1977")));
        document.Body.Replace([contents, heading, properties, variables, hidden, noted, caption, references, fixedDate, citation, alphabetical, figures, bibliography]);
        return document;
    }
}
