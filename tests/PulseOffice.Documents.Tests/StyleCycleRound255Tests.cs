using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class StyleCycleRound255Tests
{
    [Fact]
    public void Inheritance_cycles_are_cut_and_chains_kept()
    {
        var sheet = new StyleSheet();
        sheet.Paragraph.Add(new ParagraphStyle("A") { ParentName = "B" });
        sheet.Paragraph.Add(new ParagraphStyle("B") { ParentName = "C" });
        sheet.Paragraph.Add(new ParagraphStyle("C") { ParentName = "A" });
        sheet.Paragraph.Add(new ParagraphStyle("D") { ParentName = "D" });
        sheet.Paragraph.Add(new ParagraphStyle("E") { ParentName = "A" });

        sheet.BreakInheritanceCycles();

        Assert.Null(sheet.Paragraph.Find("D")!.ParentName);
        Assert.Equal(1, new[] { "A", "B", "C" }.Count(name => sheet.Paragraph.Find(name)!.ParentName is null));
        Assert.Equal("A", sheet.Paragraph.Find("E")!.ParentName);
        foreach (var style in sheet.Paragraph.All)
        {
            var seen = new HashSet<string>();
            for (var current = style; current is not null; current = sheet.Paragraph.Find(current.ParentName))
                Assert.True(seen.Add(current.Name));
        }
    }
}
