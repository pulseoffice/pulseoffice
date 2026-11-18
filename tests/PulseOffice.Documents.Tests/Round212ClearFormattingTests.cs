using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round212ClearFormattingTests
{
    private readonly DirectFormatting _direct = new();

    [Fact]
    public void Clearing_direct_formatting_keeps_the_field_detail_and_the_ruby_text_of_a_run()
    {
        var detail = new FieldDetail { Format = "YYYY-MM-DD", Fixed = true, Value = "2026-11-18" };
        var run = new TextRun("2026-11-18") { Field = TextFieldKind.Date, FieldDetail = detail, Ruby = "kana", Bold = true, FontSize = 20 };

        var cleared = _direct.Clear(run);

        Assert.Equal(detail, cleared.FieldDetail);
        Assert.Equal("kana", cleared.Ruby);
        Assert.False(cleared.Bold);
        Assert.Null(cleared.FontSize);
    }
}
