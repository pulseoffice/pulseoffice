using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class HyperlinkAppearanceTests
{
    private sealed class Palette : ITextTheme
    {
        public uint Resolve(ColorRole role) => role switch
        {
            ColorRole.Hyperlink => 0xFF0563C1,
            ColorRole.Accent2 => 0xFFED7D31,
            _ => 0xFF1A1A1A,
        };

        public string MajorFont => "Inter";

        public string MinorFont => "Inter";
    }

    private readonly IHyperlinkAppearance _links = new HyperlinkAppearance();
    private readonly ITextTheme _theme = new Palette();

    [Fact]
    public void Il_testo_collegato_di_una_forma_prende_il_colore_del_tema_e_la_sottolineatura()
    {
        var linked = new TextRun("Sito") { Hyperlink = "https://example.org", Color = 0xFF1A1A1A };

        Assert.Equal(0xFF0563C1u, _links.Ink(linked, _theme));
        Assert.True(_links.Underlined(linked));
        Assert.Equal(0xFF1A1A1Au, _links.Ink(new TextRun("piano"), _theme));
        Assert.Equal(0xFFED7D31u, _links.Ink(new TextRun("piano") { ColorRole = ColorRole.Accent2 }, _theme));
        Assert.False(_links.Underlined(new TextRun("piano")));
    }

    [Fact]
    public void Un_campo_con_indirizzo_non_si_disegna_come_collegamento()
    {
        var field = new TextRun("1") { Hyperlink = "#Slide2", Field = TextFieldKind.SlideNumber };

        Assert.Equal(0xFF1A1A1Au, _links.Ink(field, _theme));
        Assert.False(_links.Underlined(field));
    }

    [Fact]
    public void Il_testo_collegato_delle_forme_di_Ledger_e_Folio_si_colora_e_si_sottolinea()
    {
        var linked = new ShapeTextRun("Sito") { Link = "https://example.org", ColorHex = "#000000" };

        Assert.Equal("#0563C1", _links.ColorHex(linked));
        Assert.True(_links.Underlined(linked));
        Assert.Equal("#FF0000", _links.ColorHex(new ShapeTextRun("piano") { ColorHex = "#FF0000" }));
        Assert.Null(_links.ColorHex(new ShapeTextRun("piano")));
        Assert.False(_links.Underlined(new ShapeTextRun("piano")));
    }
}
