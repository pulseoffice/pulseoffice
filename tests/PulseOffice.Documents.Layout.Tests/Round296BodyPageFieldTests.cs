using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round296BodyPageFieldTests
{
    [Fact]
    public void A_body_page_field_with_its_own_numerals_ignores_the_roman_page_style()
    {
        var f = new LayoutFixture(width: 200, height: 48);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.NumberFormat = NumberFormat.LowerRoman;
        f.Add("riga");
        f.Add("riga");
        f.Add("riga");
        f.Add("riga");
        TextRun[] runs =
        [
            new("#") { Field = TextFieldKind.PageNumber, FieldDetail = new FieldDetail { Value = SequenceFormats.Arabic } },
            new("/"),
            new("#") { Field = TextFieldKind.PageNumber, FieldDetail = new FieldDetail { Value = SequenceFormats.UpperLetter } },
        ];
        f.Document.Body.Add(new ParagraphBlock(f.Document.NextId("h"), new TextParagraph(string.Empty).WithRuns(runs)));

        var layout = f.Layout();
        var last = layout.Pages[^1];

        Assert.Equal(2, last.Number);
        Assert.Equal("2/B", string.Concat(last.Lines[^1].Line.Runs.Where(run => run.Kind == GlyphRunKind.Text).Select(run => run.Text)).TrimEnd());
    }
}
