using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckPictureBulletDialogTests
{
    private readonly DeckTextFormats _formats = new();
    private readonly ListBulletPicture _picture = new(new byte[] { 1, 2, 3 }, "image/png", 12, 6);

    [Fact]
    public void The_dialog_shows_the_paragraph_picture()
    {
        var paragraph = new TextParagraph("voce") { Bullet = true, BulletSymbol = new BulletSymbol("•") { Picture = _picture } };

        Assert.Same(_picture, _formats.List(paragraph).Picture);
    }

    [Fact]
    public void A_chosen_picture_is_applied_and_removing_it_returns_to_the_character()
    {
        var plain = new TextParagraph("voce") { Bullet = true };

        var pictured = _formats.Apply(plain, new ListLevelFormat(ListNumbering.Bullet) { Picture = _picture }, 0);
        Assert.Same(_picture, pictured.BulletSymbol!.Picture);

        var back = _formats.Apply(pictured, new ListLevelFormat(ListNumbering.Bullet) { Bullet = "–" }, 0);
        Assert.Null(back.BulletSymbol!.Picture);
        Assert.Equal("–", back.BulletSymbol.Character);
    }

    [Fact]
    public void An_unchanged_picture_keeps_the_symbol_and_its_size()
    {
        var symbol = new BulletSymbol("•") { RelativeSize = 0.5, Picture = _picture with { ScalesWithFont = true } };
        var paragraph = new TextParagraph("voce") { Bullet = true, BulletSymbol = symbol };

        var applied = _formats.Apply(paragraph, _formats.List(paragraph), 0);

        Assert.Same(symbol, applied.BulletSymbol);
    }
}
