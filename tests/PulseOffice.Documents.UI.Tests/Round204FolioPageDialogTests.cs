using PulseOffice.Documents.Editing.Borders;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Pages;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round204FolioPageDialogTests
{
    private FolioPageTabs Tabs() =>
        new(new FolioPageServices(null!, null!, null!, null!, null!, new BoxBorderFrames(), null!, null!, null!, new TextCatalog(UiLanguage.English), new FolioColumnLayouts()));

    [Fact]
    public void The_page_dialog_sets_the_gutter_and_its_position()
    {
        var style = new PageStyle("Default");
        var values = new Dictionary<string, object>
        {
            ["size"] = 0,
            ["orientation"] = 0,
            ["margins"] = new PageMargins(2, 2, 2, 2),
            ["gutter"] = 1.5,
            ["gutterPosition"] = 1,
        };

        Tabs().Write(style, [], values);

        Assert.Equal(1.5, Math.Round(style.Gutter.ToCentimeters(), 2));
        Assert.True(style.GutterAtTop);
    }

    [Fact]
    public void Editing_the_margins_alone_keeps_the_gutter()
    {
        var style = new PageStyle("Default") { Gutter = PulseOffice.Foundation.Units.Twip.FromCentimeters(1) };
        var values = new Dictionary<string, object> { ["size"] = 0, ["orientation"] = 0, ["margins"] = new PageMargins(2, 2, 2, 2) };

        Tabs().Write(style, [], values);

        Assert.Equal(1, Math.Round(style.Gutter.ToCentimeters(), 2));
        Assert.False(style.GutterAtTop);
    }

    [Fact]
    public void The_columns_editor_widths_become_uneven_columns()
    {
        var style = new PageStyle("Default");
        var layout = new ColumnLayout(2, 1, false) { Widths = [new ColumnWidth(4, 1), new ColumnWidth(12, 0)] };
        var values = new Dictionary<string, object> { ["size"] = 0, ["orientation"] = 0, ["columnLayout"] = layout };

        Tabs().Write(style, [], values);

        Assert.True(style.Columns.Uneven);
        Assert.Equal(3, style.Columns.Spans[1].Width.Value / (double)style.Columns.Spans[0].Width.Value, 2);
    }

    [Fact]
    public void Unedited_uneven_columns_keep_their_stored_widths()
    {
        var layouts = new FolioColumnLayouts();
        var columns = new PulseOffice.Documents.Blocks.SectionColumns(2, new PulseOffice.Foundation.Units.Twip(720))
        {
            Spans = [new(new PulseOffice.Foundation.Units.Twip(2000), new PulseOffice.Foundation.Units.Twip(720)), new(new PulseOffice.Foundation.Units.Twip(6000), PulseOffice.Foundation.Units.Twip.Zero)],
        };

        var shown = layouts.Layout(columns, 16);
        var written = layouts.Columns(shown, true, columns);

        Assert.Equal(columns, written);
        Assert.Equal(3, shown.Widths[1].Width / shown.Widths[0].Width, 3);
    }
}
