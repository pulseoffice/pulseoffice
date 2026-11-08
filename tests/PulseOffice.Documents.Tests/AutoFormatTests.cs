using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class AutoFormatTests
{
    private readonly Fixture _f = new();

    private DocumentAutoFormat Formatter() =>
        new(new ParagraphAutoFormat([new NumberRule(), new BulletRule(), new BorderRule()]), _f.Editor, new ListEditing(_f.Stories, _f.Resolver), _f.Stories);

    [Theory]
    [InlineData("- voce", AutoFormatKind.Bullets, 2)]
    [InlineData("* voce", AutoFormatKind.Bullets, 2)]
    [InlineData("12) voce", AutoFormatKind.Numbering, 4)]
    [InlineData("------", AutoFormatKind.Border, 6)]
    public void IRiconoscimentiSonoQuelliDiWriter(string text, AutoFormatKind kind, int prefix)
    {
        var match = new ParagraphAutoFormat([new NumberRule(), new BulletRule(), new BorderRule()]).Recognise(text);

        Assert.Equal(kind, match?.Kind);
        Assert.Equal(prefix, match?.PrefixLength);
    }

    [Theory]
    [InlineData("-voce")]
    [InlineData("--")]
    [InlineData("1.5 metri")]
    [InlineData("-- a")]
    public void IlTestoNormaleNonSiTocca(string text) =>
        Assert.Null(new ParagraphAutoFormat([new NumberRule(), new BulletRule(), new BorderRule()]).Recognise(text));

    [Fact]
    public void ApplicareATuttoIlDocumentoEUnSoloAnnullamento()
    {
        _f.Document.Body.Replace([
            new ParagraphBlock(_f.Document.NextId("p"), "- uno"),
            new ParagraphBlock(_f.Document.NextId("p"), "2. due"),
            new ParagraphBlock(_f.Document.NextId("p"), "***"),
            new ParagraphBlock(_f.Document.NextId("p"), "testo"),
        ]);

        Assert.Equal(3, Formatter().FormatAll(_f.Document, _f.History, lists: true, borders: true));

        Assert.Equal(["uno", "due", "", "testo"], _f.Document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.Equal(DefaultStyleNames.List1, _f.Paragraph(0).Content.ListStyleName);
        Assert.Equal(2, _f.Paragraph(1).Content.ListRestart);
        Assert.Equal(4.5, _f.Paragraph(2).Content.Borders?.Bottom?.Width);

        _f.History.Undo();

        Assert.Equal("- uno", _f.Paragraph(0).Text);
        Assert.Null(_f.Paragraph(0).Content.ListStyleName);
    }
}
