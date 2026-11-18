using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round215ListSuppressionTests
{
    private readonly Fixture _f = new();

    public Round215ListSuppressionTests()
    {
        _f.Document.Styles.Paragraph.Add(new ParagraphStyle("Listed") { ParentName = DefaultStyleNames.Standard, ListStyleName = DefaultStyleNames.List1 });
        _f.Type("one");
        _f.First.Content = _f.First.Content with { StyleName = "Listed" };
    }

    [Fact]
    public void Removing_the_list_of_a_styled_list_paragraph_switches_the_style_list_off()
    {
        Assert.Equal(DefaultStyleNames.List1, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.First.Content).ListStyleName);

        _f.Editor.SetList(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), null, 0);

        Assert.True(_f.First.Content.ListSuppressed);
        Assert.Null(_f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.First.Content).ListStyleName);
    }

    [Fact]
    public void Applying_a_list_again_clears_the_switch()
    {
        _f.Editor.SetList(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), null, 0);
        _f.Editor.SetList(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), DefaultStyleNames.Numbering123, 0);

        Assert.False(_f.First.Content.ListSuppressed);
        Assert.Equal(DefaultStyleNames.Numbering123, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.First.Content).ListStyleName);
    }

    [Fact]
    public void A_paragraph_without_a_style_list_is_not_marked_when_its_list_is_removed()
    {
        _f.First.Content = _f.First.Content with { StyleName = DefaultStyleNames.Standard };
        _f.Editor.SetList(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), DefaultStyleNames.Numbering123, 0);
        _f.Editor.SetList(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), null, 0);

        Assert.False(_f.First.Content.ListSuppressed);
    }
}
