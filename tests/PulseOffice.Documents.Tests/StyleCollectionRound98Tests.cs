using System.Diagnostics;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class StyleCollectionRound98Tests
{
    [Fact]
    public void Many_styles_chained_deeply_resolve_in_near_linear_time()
    {
        const int count = 20_000;
        var chainLength = 0;
        new PulseOffice.Testing.GrowthCheck().Linear(size =>
        {
            var sheet = new StyleSheet();
            for (var index = 0; index < size; index++)
                sheet.Paragraph.Add(new ParagraphStyle("S" + index) { ParentName = index == 0 ? null : "S" + (index - 1) });
            return () => chainLength = new StyleChain().RootFirst(sheet.Paragraph, "S" + (size - 1)).Count;
        }, count);

        Assert.Equal(count, chainLength);
    }

    [Fact]
    public void Circular_parents_resolve_once_each()
    {
        var sheet = new StyleSheet();
        sheet.Paragraph.Add(new ParagraphStyle("A") { ParentName = "B" });
        sheet.Paragraph.Add(new ParagraphStyle("B") { ParentName = "A" });

        Assert.Equal(["A", "B"], new StyleChain().RootFirst(sheet.Paragraph, "B").Select(style => style.Name));
    }

    [Fact]
    public void Removing_and_replacing_keep_the_lookup_in_step()
    {
        var collection = new StyleCollection<CharacterStyle>();
        collection.Add(new CharacterStyle("A"));
        Assert.True(collection.Remove("A"));
        Assert.Null(collection.Find("A"));
        Assert.False(collection.Remove("A"));

        var first = new CharacterStyle("B");
        collection.ReplaceAll([first, new CharacterStyle("B")]);
        Assert.Same(first, collection.Find("B"));

        var copy = new StyleCollection<CharacterStyle>();
        copy.CopyFrom(collection);
        Assert.NotNull(copy.Find("B"));
        Assert.NotSame(first, copy.Find("B"));
    }
}
