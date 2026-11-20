using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round289PageFieldShapeTests
{
    [Fact]
    public void A_page_field_with_its_own_numerals_shows_them()
    {
        TextRun[] runs = [new("1") { Field = TextFieldKind.PageNumber, FieldDetail = new FieldDetail { Value = SequenceFormats.UpperRoman } }];

        var resolved = new PageFieldText(new SequenceNumerals()).Resolve(new TextParagraph("1") { Runs = runs }, "4", null);

        Assert.Equal("IV", resolved.Text);
    }
}
