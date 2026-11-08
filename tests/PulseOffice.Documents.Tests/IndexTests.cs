using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class IndexTests
{
    private readonly Fixture _f = new();

    private sealed class Pages(Dictionary<string, string> pages) : IFieldLayoutFacts
    {
        public string? PageOf(string paragraphId) => pages.GetValueOrDefault(paragraphId);

        public string? NoteCitation(string noteId) => null;

        public string? ListLabel(string paragraphId) => null;

        public int? PageCount => pages.Count;
    }

    private Pages _pages = new([]);

    private void Build(params (string Text, string Style)[] paragraphs)
    {
        _f.Document.Body.Replace(paragraphs.Select((entry, i) => (Block)new ParagraphBlock("p" + (i + 100), entry.Text, entry.Style)));
        _pages = new Pages(paragraphs.Select((_, i) => ("p" + (i + 100), (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))).ToDictionary(pair => pair.Item1, pair => pair.Item2));
    }

    private SectionBlock Index => _f.IndexEditing.Indexes(_f.Document).Single();

    private IReadOnlyList<string> Lines => [.. _f.Tree.Paragraphs(Index.Content).Select(paragraph => paragraph.Text)];

    [Fact]
    public void IlSommarioRaccoglieLeIntestazioniConLePagineEIlCollegamento()
    {
        Build(("", DefaultStyleNames.TextBody), ("Introduzione", DefaultStyleNames.HeadingLevel(1)), ("testo", DefaultStyleNames.TextBody),
            ("Dettagli", DefaultStyleNames.HeadingLevel(2)), ("Profondo", DefaultStyleNames.HeadingLevel(4)));

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p100", 0), new IndexDefinition(IndexKind.Contents) { Title = "Indice", Levels = 3 }, _pages);

        Assert.Equal(["Indice", "Introduzione\t2", "Dettagli\t4"], Lines);
        Assert.True(Index.Protected);
        var entry = (ParagraphBlock)Index.Content[2];
        Assert.Equal(DefaultStyleNames.ContentsLevel(2), entry.StyleName);
        Assert.Equal("#Dettagli|outline", entry.Content.Runs[0].Hyperlink);
        _f.History.Undo();
        Assert.Empty(_f.IndexEditing.Indexes(_f.Document));
    }

    [Fact]
    public void LAggiornamentoRifaIlSommarioESiAnnulla()
    {
        Build(("", DefaultStyleNames.TextBody), ("Uno", DefaultStyleNames.HeadingLevel(1)));
        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p100", 0), new IndexDefinition(IndexKind.Contents), _pages);
        _f.Document.Body.Add(new ParagraphBlock("p200", "Due", DefaultStyleNames.HeadingLevel(1)));

        _f.IndexEditing.Update(_f.Document, _f.History, new Pages(new() { ["p101"] = "1", ["p200"] = "5" }));

        Assert.Equal(["Uno\t1", "Due\t5"], Lines);
        _f.History.Undo();
        Assert.Equal(["Uno\t2"], Lines);
    }

    [Fact]
    public void LIndiceAnaliticoUnisceLeVociEMetteLeLettere()
    {
        Build(("mela e pera", DefaultStyleNames.TextBody), ("pera", DefaultStyleNames.TextBody), ("banana", DefaultStyleNames.TextBody), ("", DefaultStyleNames.TextBody));
        _f.Paragraph(0).Marks = [new IndexEntryMark(0, "mela", MarkEdge.Point), new IndexEntryMark(7, "pera", MarkEdge.Point)];
        _f.Paragraph(1).Marks = [new IndexEntryMark(0, "Pera", MarkEdge.Point)];
        _f.Paragraph(2).Marks = [new IndexEntryMark(0, "banana", MarkEdge.Point)];

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p103", 0), new IndexDefinition(IndexKind.Alphabetical) { LetterSeparators = true }, _pages);

        Assert.Equal(["B", "banana, 3", "M", "mela, 1", "P", "pera, 1, 2"], Lines);
    }

    [Fact]
    public void LIndiceDelleFigureUsaLeDidascalieNumerate()
    {
        Build(("", DefaultStyleNames.TextBody), ("x", DefaultStyleNames.Caption), ("y", DefaultStyleNames.Caption));
        _f.Paragraph(1).Content = _f.Paragraph(1).Content.WithRuns([new TextRun("Figura "), new TextRun("1") { Field = TextFieldKind.Sequence, FieldArgument = "Figure" }, new TextRun(": il grafico")]);
        _f.Paragraph(2).Content = _f.Paragraph(2).Content.WithRuns([new TextRun("Tabella "), new TextRun("1") { Field = TextFieldKind.Sequence, FieldArgument = "Table" }, new TextRun(": i dati")]);

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p100", 0), new IndexDefinition(IndexKind.Illustrations) { Captions = CaptionDisplay.Text }, _pages);

        Assert.Equal(["il grafico\t2"], Lines);
    }

    [Fact]
    public void LaBibliografiaElencaLeFontiCitate()
    {
        Build(("Come in ", DefaultStyleNames.TextBody), ("", DefaultStyleNames.TextBody));
        _f.Document.Bibliography.Store(new BibliographySource("Eco1977", "book", new Dictionary<string, string> { ["author"] = "Umberto Eco", ["title"] = "Tesi", ["year"] = "1977" }));
        _f.Paragraph(0).Content = _f.Paragraph(0).Content.WithRuns([new TextRun("Come in "), new TextRun("[Eco1977]") { Field = TextFieldKind.Citation, FieldArgument = "Eco1977" }]);

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p101", 0), new IndexDefinition(IndexKind.Bibliography), _pages);

        Assert.Equal(["[Eco1977] Umberto Eco, Tesi, 1977"], Lines);
    }

    [Fact]
    public void IlSommarioNonContieneSeStesso()
    {
        Build(("", DefaultStyleNames.TextBody), ("Capo", DefaultStyleNames.HeadingLevel(1)));
        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p100", 0), new IndexDefinition(IndexKind.Contents) { Title = "Sommario" }, _pages);
        Index.Content.Add(new ParagraphBlock("intruso", "Finto", DefaultStyleNames.HeadingLevel(1)));
        _f.IndexEditing.Update(_f.Document, _f.History, _pages);
        Assert.Equal(["Sommario", "Capo\t2"], Lines);
    }

    [Fact]
    public void LIndiceSiTogliePerIntero()
    {
        Build(("", DefaultStyleNames.TextBody));
        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p100", 0), new IndexDefinition(IndexKind.Contents), _pages);
        _f.IndexEditing.Remove(_f.Document, _f.History, Index.Id);
        Assert.Empty(_f.IndexEditing.Indexes(_f.Document));
        Assert.Single(_f.Document.Body.Blocks);
    }
}
