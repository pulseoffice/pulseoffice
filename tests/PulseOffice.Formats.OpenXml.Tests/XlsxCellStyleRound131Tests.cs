using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxCellStyleRound131Tests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void A_built_in_child_that_resets_its_parents_alignment_keeps_it_reset()
    {
        var workbook = Workbook.CreateDefault();
        var heading = workbook.NamedStyles.Find("Heading 1")!;
        workbook.NamedStyles.Define("Heading 1", workbook.Styles.Get(heading.StyleId), heading.Parent, builtIn: true,
            explicitProperties: heading.Explicit | CellStyleProperties.Horizontal);

        var reread = RoundTrip(workbook);
        var style = reread.NamedStyles.StyleOf(reread.NamedStyles.Find("Heading 1")!);

        Assert.Equal(CellHorizontalAlignment.General, style.Horizontal);
        Assert.True(style.Font.Bold);
    }

    [Fact]
    public void An_unlocked_parent_does_not_unlock_a_child_that_locks_itself()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Heading", CellStyle.Default with { Locked = false }, builtIn: true);
        var heading = workbook.NamedStyles.Find("Heading 1")!;
        workbook.NamedStyles.Define("Heading 1", workbook.Styles.Get(heading.StyleId), heading.Parent, builtIn: true,
            explicitProperties: heading.Explicit | CellStyleProperties.Locked);

        var reread = RoundTrip(workbook);

        Assert.True(reread.NamedStyles.StyleOf(reread.NamedStyles.Find("Heading 1")!).Locked);
        Assert.False(reread.NamedStyles.StyleOf(reread.NamedStyles.Find("Heading")!).Locked);
    }
}
