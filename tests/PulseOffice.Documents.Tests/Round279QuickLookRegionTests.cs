using PulseOffice.Document.Tables;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round279QuickLookRegionTests
{
    [Fact]
    public void A_quick_look_sets_total_row_and_column_formats()
    {
        var fixture = new Fixture();
        var formatting = new TableFormatting(new TableGrid(fixture.Tree), new StyleEditing(new StyleReferences(fixture.Stories, fixture.Tree), new PulseOffice.Documents.Clipboard.FragmentRemapper(fixture.Stories, fixture.Tree)), fixture.Tree);
        var look = new TableStylePresets().Find("boxListGreen")!;

        var name = formatting.EnsurePreset(fixture.Document, fixture.History, look, look.Name);

        var style = fixture.Document.Styles.Table.Find(name)!.Table;
        Assert.True(style.TotalRow!.Bold);
        Assert.Equal(BorderLineStyle.Double, style.TotalRow.Borders!.Top!.Style);
        Assert.Equal(look.BorderColor, style.TotalRow.Borders.Top.Color);
        Assert.True(style.FirstColumn!.Bold);
        Assert.True(style.LastColumn!.Bold);
    }
}
