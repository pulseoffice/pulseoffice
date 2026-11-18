using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round199ListPreviewLanguageTests
{
    private readonly ListSampleRows _rows = new(new ListLabeler());

    [Fact]
    public void Spelled_numbers_follow_the_given_language()
    {
        var rows = _rows.Rows([new ListLevelFormat(ListNumbering.CardinalText) { Suffix = string.Empty }], [0, 0], "it-IT");

        Assert.Equal(["Uno", "Due"], rows.Select(row => row.Label));
    }

    [Fact]
    public void Without_a_language_the_preview_stays_english()
    {
        var rows = _rows.Rows([new ListLevelFormat(ListNumbering.CardinalText) { Suffix = string.Empty }], [0]);

        Assert.Equal("One", rows[0].Label);
    }
}
