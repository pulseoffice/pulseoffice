using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFieldOptionsTests
{
    private readonly ClockFieldOptions _clock = new();

    private readonly FolioFieldChoice _date = new("date", TextFieldKind.Date);

    [Fact]
    public void Changing_the_format_of_a_fixed_date_keeps_the_date_it_was_fixed_on()
    {
        var current = new TextRun("01/03/2020") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Fixed = true, Value = "2020-03-01" } };
        var context = new FieldOptionsContext(null!, null!, new DateTime(2026, 11, 7));
        var values = new Dictionary<string, object> { ["format"] = 6, ["fixed"] = true };

        var composed = _clock.Compose(_date, current, values, context);

        Assert.Equal("2020-03-01", composed!.FieldDetail!.Value);
        Assert.Equal("yyyy-MM-dd", composed.FieldDetail.Format);
    }

    [Fact]
    public void Fixing_a_live_date_stores_today()
    {
        var current = new TextRun("x") { Field = TextFieldKind.Date };
        var context = new FieldOptionsContext(null!, null!, new DateTime(2026, 11, 7));

        var composed = _clock.Compose(_date, current, new Dictionary<string, object> { ["fixed"] = true }, context);

        Assert.Equal("2026-11-07", composed!.FieldDetail!.Value);
    }
}
