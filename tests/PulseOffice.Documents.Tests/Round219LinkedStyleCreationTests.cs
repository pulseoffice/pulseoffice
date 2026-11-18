using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round219LinkedStyleCreationTests
{
    private readonly Fixture _f = new();

    public Round219LinkedStyleCreationTests()
    {
        _f.Document.Styles.Paragraph.Add(new ParagraphStyle("Quote Block") { ParentName = DefaultStyleNames.Standard, Character = new CharacterProperties { Bold = true } });
        _f.Type("hello world");
    }

    private StyleEditing Styles() => new(new StyleReferences(_f.Stories, _f.Tree), new PulseOffice.Documents.Clipboard.FragmentRemapper(_f.Stories, _f.Tree));

    [Fact]
    public void A_partial_selection_creates_the_char_partner_in_one_undo_step()
    {
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 5)), "Quote Block");

        var partner = _f.Document.Styles.Character.Find("Quote Block Char");
        Assert.NotNull(partner);
        Assert.Equal("Quote Block", partner.LinkedStyleName);
        Assert.True(partner.Character.Bold);
        Assert.Equal("Quote Block Char", _f.Document.Styles.Paragraph.Find("Quote Block")!.LinkedStyleName);
        Assert.Equal("Quote Block Char", _f.First.Content.Runs[0].StyleName);

        _f.History.Undo();
        Assert.False(_f.Document.Styles.Character.Contains("Quote Block Char"));
        Assert.Null(_f.Document.Styles.Paragraph.Find("Quote Block")!.LinkedStyleName);
        Assert.All(_f.First.Content.Runs, run => Assert.Null(run.StyleName));
    }

    [Fact]
    public void The_standard_style_never_gets_a_char_partner()
    {
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 5)), DefaultStyleNames.Standard);

        Assert.False(_f.Document.Styles.Character.Contains(DefaultStyleNames.Standard + " Char"));
    }

    [Fact]
    public void Linking_moves_the_partner_and_clears_the_old_one()
    {
        var styles = Styles();
        styles.Link(_f.Document, _f.History, "Quote Block", new StyleLink.ToNew());
        Assert.Equal("Quote Block", _f.Document.Styles.Character.Find("Quote Block Char")!.LinkedStyleName);

        _f.Document.Styles.Character.Add(new CharacterStyle("Other"));
        styles.Link(_f.Document, _f.History, "Quote Block", new StyleLink.ToExisting("Other"));
        Assert.Equal("Other", _f.Document.Styles.Paragraph.Find("Quote Block")!.LinkedStyleName);
        Assert.Equal("Quote Block", _f.Document.Styles.Character.Find("Other")!.LinkedStyleName);
        Assert.Null(_f.Document.Styles.Character.Find("Quote Block Char")!.LinkedStyleName);

        styles.Link(_f.Document, _f.History, "Quote Block", new StyleLink.None());
        Assert.Null(_f.Document.Styles.Paragraph.Find("Quote Block")!.LinkedStyleName);
        Assert.Null(_f.Document.Styles.Character.Find("Other")!.LinkedStyleName);
    }
}
