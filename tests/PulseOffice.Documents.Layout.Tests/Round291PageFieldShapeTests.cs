using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round291PageFieldShapeTests
{
    [Fact]
    public void A_page_field_with_its_own_numerals_shows_them_on_a_roman_page()
    {
        TextRun[] runs = [new("1") { Field = TextFieldKind.PageNumber, FieldDetail = new FieldDetail { Value = SequenceFormats.UpperLetter } }];

        var resolved = new PageFieldText(new SequenceNumerals()).Resolve(new TextParagraph("1") { Runs = runs }, "iv", null, 4);

        Assert.Equal("D", resolved.Text);
    }
}
