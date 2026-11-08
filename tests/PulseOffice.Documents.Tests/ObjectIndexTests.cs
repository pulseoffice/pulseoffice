using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ObjectIndexTests
{
    private readonly Fixture _f = new();

    private sealed class Pages : IFieldLayoutFacts
    {
        public string? PageOf(string paragraphId) => paragraphId == "p101" ? "1" : "2";

        public string? NoteCitation(string noteId) => null;

        public string? ListLabel(string paragraphId) => null;

        public int? PageCount => 2;
    }

    private void Place(string id, string paragraphId, EmbeddedObject value, string name) =>
        _f.Document.Objects.Add(new AnchoredObject(id, new ParagraphAnchor(paragraphId), new EmbeddedContent(value), new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2))) { Name = name });

    [Fact]
    public void LIndiceDegliOggettiElencaSoloIGeneriScelti()
    {
        _f.Document.Body.Replace(
        [
            new ParagraphBlock("p100", string.Empty, DefaultStyleNames.TextBody),
            new ParagraphBlock("p101", "uno", DefaultStyleNames.TextBody),
            new ParagraphBlock("p102", "due", DefaultStyleNames.TextBody),
        ]);
        Place("o1", "p101", new FormulaObject { Id = "f1", Expression = "a+b" }, "Formula1");
        Place("o2", "p102", new PackagedObject { Id = "k1", FileName = "x.ods", MediaType = "application/vnd.oasis.opendocument.spreadsheet", Data = new byte[] { 1 } }, "Foglio1");
        Place("o3", "p102", new PackagedObject { Id = "k2", FileName = "x.bin", MediaType = "application/octet-stream", Data = new byte[] { 1 } }, "Altro1");

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p100", 0),
            new IndexDefinition(IndexKind.Objects) { ObjectSources = IndexObjectSources.Formulas | IndexObjectSources.Spreadsheets, Hyperlinks = false }, new Pages());

        var index = _f.IndexEditing.Indexes(_f.Document).Single();
        Assert.Equal(["Formula1\t1", "Foglio1\t2"], _f.Tree.Paragraphs(index.Content).Select(paragraph => paragraph.Text));
        Assert.All(_f.Tree.Paragraphs(index.Content), paragraph => Assert.Equal(DefaultStyleNames.ObjectIndex1, paragraph.StyleName));
    }
}
