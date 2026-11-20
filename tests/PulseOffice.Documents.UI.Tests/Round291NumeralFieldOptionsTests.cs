using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round291NumeralFieldOptionsTests
{
    private readonly NumeralFieldOptions _options = new(new SequenceNumerals());

    private readonly FieldOptionsContext _context = new(null!, null!, new DateTime(2026, 11, 20));

    [Fact]
    public void A_sequence_field_takes_its_name_and_numerals_from_the_dialog()
    {
        var current = new TextRun("1") { Field = TextFieldKind.Sequence };
        var values = new Dictionary<string, object> { ["name"] = "Table", ["numerals"] = 2 };

        var composed = _options.Compose(new FolioFieldChoice("sequence", TextFieldKind.Sequence), current, values, _context);

        Assert.Equal("Table", composed!.FieldArgument);
        Assert.Equal(SequenceFormats.UpperRoman, composed.FieldDetail!.Value);
    }

    [Fact]
    public void Page_field_numerals_back_to_arabic_drop_the_detail()
    {
        var current = new TextRun("i") { Field = TextFieldKind.PageNumber, FieldDetail = new FieldDetail { Value = SequenceFormats.LowerRoman } };

        var composed = _options.Compose(new FolioFieldChoice("pageNumber", TextFieldKind.PageNumber), current, new Dictionary<string, object> { ["numerals"] = 0 }, _context);

        Assert.Null(composed!.FieldDetail);
    }

    [Fact]
    public void A_chapter_field_keeps_the_chosen_level()
    {
        var current = new TextRun("x") { Field = TextFieldKind.Chapter, FieldArgument = "number" };

        var composed = _options.Compose(new FolioFieldChoice("chapterNumber", TextFieldKind.Chapter, "number"), current, new Dictionary<string, object> { ["level"] = 2 }, _context);

        Assert.Equal(("number", "3"), (composed!.FieldArgument, composed.FieldDetail!.Value));
    }
}
