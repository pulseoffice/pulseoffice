using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round296FieldOptionsTests
{
    private readonly FieldOptionsContext _context = new(null!, null!, new DateTime(2026, 11, 20));

    [Fact]
    public void A_page_field_can_force_arabic_numerals()
    {
        var current = new TextRun("i") { Field = TextFieldKind.PageNumber };

        var composed = new NumeralFieldOptions(new SequenceNumerals()).Compose(new FolioFieldChoice("pageNumber", TextFieldKind.PageNumber), current, new Dictionary<string, object> { ["numerals"] = 1 }, _context);

        Assert.Equal(SequenceFormats.Arabic, composed!.FieldDetail!.Value);
    }
}
