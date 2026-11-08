using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class FieldTests
{
    private readonly Fixture _f = new();
    private readonly FieldCondition _conditions = new();

    private sealed class Facts : IFieldLayoutFacts
    {
        public string? PageOf(string paragraphId) => "7";

        public string? NoteCitation(string noteId) => "3";

        public string? ListLabel(string paragraphId) => null;

        public int? PageCount => 12;
    }

    private FieldUpdate Request => new(new Facts(), new DateTime(2026, 10, 10, 9, 30, 0));

    private void Field(int paragraph, int offset, TextFieldKind kind, string shown, string? argument = null, FieldDetail? detail = null)
    {
        _f.Editor.InsertField(_f.Document, _f.History, _f.At(paragraph, offset), kind, shown, argument);
        if (detail is null) return;
        var block = _f.Paragraph(paragraph);
        var position = 0;
        var runs = new List<TextRun>();
        foreach (var run in block.Content.Runs)
        {
            runs.Add(position == offset && run.Field == kind ? run with { FieldDetail = detail } : run);
            position += run.Text.Length;
        }
        block.Content = block.Content.WithRuns(runs);
    }

    private TextRun Run(int paragraph, TextFieldKind kind) => _f.Paragraph(paragraph).Content.Runs.First(run => run.Field == kind);

    [Theory]
    [InlineData("a == 1", true)]
    [InlineData("a EQ 2", false)]
    [InlineData("a > 0 AND b == \"sì\"", true)]
    [InlineData("!(a == 1) || b != \"sì\"", false)]
    [InlineData("ooow:a + 1 GEQ 2", true)]
    [InlineData("missing", false)]
    [InlineData("a LT", false)]
    public void LaCondizioneValutaVariabiliConfrontiELogica(string condition, bool expected)
    {
        var variables = new Dictionary<string, string> { ["a"] = "1", ["b"] = "sì" };
        Assert.Equal(expected, _conditions.Holds(condition, variables));
    }

    [Fact]
    public void LeVariabiliSiImpostanoESiLeggonoInOrdine()
    {
        _f.Type("prima ", "dopo ");
        Field(0, 6, TextFieldKind.VariableSet, "0", "conto", new FieldDetail { Format = "5" });
        Field(1, 5, TextFieldKind.Variable, "?", "conto");

        _f.FieldUpdater.Update(_f.Document, _f.History, Request);

        Assert.Equal("5", Run(0, TextFieldKind.VariableSet).Text);
        Assert.Equal("dopo 5", _f.Paragraph(1).Text);
    }

    [Fact]
    public void IlTestoCondizionaleEQuelloNascostoSeguonoLaCondizione()
    {
        _f.Document.Variables["lingua"] = "it";
        _f.Type("x ", "y ");
        Field(0, 2, TextFieldKind.ConditionalText, "?", "lingua == \"it\"", new FieldDetail { WhenTrue = "ciao", WhenFalse = "hello" });
        Field(1, 2, TextFieldKind.HiddenText, "segreto", "lingua == \"it\"", new FieldDetail { WhenTrue = "segreto" });

        _f.FieldUpdater.Update(_f.Document, _f.History, Request);

        Assert.Equal("x ciao", _f.Paragraph(0).Text);
        Assert.True(Run(1, TextFieldKind.HiddenText).Hidden);

        _f.Document.Variables["lingua"] = "en";
        _f.FieldUpdater.Update(_f.Document, _f.History, Request);
        Assert.Equal("x hello", _f.Paragraph(0).Text);
        Assert.False(Run(1, TextFieldKind.HiddenText).Hidden);
    }

    [Fact]
    public void IRimandiMostranoTestoPaginaEDirezione()
    {
        _f.Type("Vedi ", "Capitolo bello", "e ");
        _f.Editor.AddBookmark(_f.Document, _f.History, DocumentRange.In(_f.Paragraph(1).Id, 0, 8), "cap");
        Field(0, 5, TextFieldKind.Bookmark, "?", "cap");
        Field(2, 2, TextFieldKind.Bookmark, "?", "cap", new FieldDetail { Format = ReferenceFormats.Direction });
        Field(0, 5, TextFieldKind.Bookmark, "?", "cap", new FieldDetail { Format = ReferenceFormats.Page });

        _f.FieldUpdater.Update(_f.Document, _f.History, Request);

        Assert.Equal("Vedi 7Capitolo", _f.Paragraph(0).Text);
        Assert.Equal("e above", _f.Paragraph(2).Text);
    }

    [Fact]
    public void IlRimandoAUnSegnalibroMancanteLoDice()
    {
        _f.Type("x ");
        Field(0, 2, TextFieldKind.Bookmark, "?", "nessuno");
        _f.FieldUpdater.Update(_f.Document, _f.History, Request);
        Assert.Contains("not found", _f.First.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void IConteggiEIlNumeroDiPagineSiAggiornanoEAnnullano()
    {
        _f.Type("uno due tre ");
        Field(0, 12, TextFieldKind.WordCount, "0");
        Field(0, 0, TextFieldKind.PageCount, "0");
        var before = _f.First.Text;

        _f.FieldUpdater.Update(_f.Document, _f.History, Request);

        Assert.Equal("12uno due tre 4", _f.First.Text);
        _f.History.Undo();
        Assert.Equal(before, _f.First.Text);
    }

    [Fact]
    public void IlCampoFissoNonCambia()
    {
        _f.Type("d ");
        Field(0, 2, TextFieldKind.Date, "01/01/2000", detail: new FieldDetail { Fixed = true });
        _f.FieldUpdater.Update(_f.Document, _f.History, Request);
        Assert.Equal("d 01/01/2000", _f.First.Text);
    }

    [Fact]
    public void LAggiornamentoDelCampoSelezionatoTocaSoloQuelParagrafo()
    {
        _f.Document.Properties.Title = "Relazione";
        _f.Type("a ", "b ");
        Field(0, 2, TextFieldKind.Title, "?");
        Field(1, 2, TextFieldKind.Title, "?");
        _f.FieldUpdater.Update(_f.Document, _f.History, Request with { Paragraphs = new HashSet<string> { _f.Paragraph(1).Id } });
        Assert.Equal("a ?", _f.Paragraph(0).Text);
        Assert.Equal("b Relazione", _f.Paragraph(1).Text);
    }

    [Fact]
    public void IlSegnalibroDopoUnCampoSiSpostaConIlTesto()
    {
        _f.Type("x  fine");
        Field(0, 2, TextFieldKind.Title, "?");
        _f.Editor.AddBookmark(_f.Document, _f.History, DocumentRange.At(_f.At(0, 4)), "dopo");
        _f.Document.Properties.Title = "Lungo titolo";
        _f.FieldUpdater.Update(_f.Document, _f.History, Request);
        var mark = _f.First.Marks.OfType<Marks.BookmarkMark>().Single();
        Assert.Equal("x Lungo titolo fine".IndexOf("fine", StringComparison.Ordinal), mark.Offset);
    }
}
