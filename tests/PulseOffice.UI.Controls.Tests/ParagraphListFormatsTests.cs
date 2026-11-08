using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ParagraphListFormatsTests
{
    private readonly ParagraphListFormats _formats = new();

    [Fact]
    public void Lo_schema_nativo_resta_se_lo_stile_non_cambia_e_cade_se_cambia()
    {
        var native = new ListNumberFormat { Suffix = string.Empty, DrawingScheme = "circleNumDbPlain" };
        var paragraph = new TextParagraph("Uno") { Numbered = true, NumberFormat = native };
        var level = _formats.List(paragraph);

        Assert.Equal(native, _formats.Apply(paragraph, level with { Start = 1 }, 0).NumberFormat);
        Assert.Equal(new ListNumberFormat { Style = ListNumberStyle.LowerLetter, Suffix = string.Empty, Start = 2 },
            _formats.Apply(paragraph, level with { Numbering = ListNumbering.LowerLetter, Start = 2 }, 0).NumberFormat);
        Assert.Null(_formats.Apply(paragraph, new ListLevelFormat(ListNumbering.Arabic), 0).NumberFormat);
        Assert.Null(_formats.Apply(paragraph, new ListLevelFormat(ListNumbering.Bullet), 0).NumberFormat);
    }

    [Fact]
    public void Il_dialogo_numera_con_tutti_gli_stili()
    {
        var request = _formats.Request(new TextParagraph("Uno") { Numbered = true, NumberFormat = new ListNumberFormat { Style = ListNumberStyle.LowerRoman, Suffix = ")" } });

        Assert.Equal(ListNumbering.LowerRoman, request.Levels[0].Numbering);
        Assert.Equal(")", request.Levels[0].Suffix);
        Assert.True(request.Offers(ListFeatures.Affixes | ListFeatures.Start));
        Assert.Equal("iv)", new ListLabeler().Label(request.Levels, 0, [4]));
    }
}
