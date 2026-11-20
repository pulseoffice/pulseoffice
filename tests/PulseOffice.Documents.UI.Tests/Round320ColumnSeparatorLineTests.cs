using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Pages;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round320ColumnSeparatorLineTests
{
    private readonly FolioColumnLayouts _layouts = new();

    private readonly SectionColumns _columns = new(2, new Twip(720))
    {
        Separator = true,
        SeparatorLine = new ColumnSeparatorLine(BorderLineStyle.Dashed, 1.5, 0xFF2563EB) { HeightPercent = 60, Alignment = ColumnSeparatorAlignment.Middle },
    };

    [Fact]
    public void The_editor_shows_the_stored_separator_line()
    {
        var line = _layouts.Layout(_columns, 16).Line;

        Assert.Equal(new ColumnLine(BorderLineStyle.Dashed, 1.5, "#2563EB", 60, ColumnLinePosition.Middle), line);
    }

    [Fact]
    public void An_edited_separator_line_reaches_the_model()
    {
        var shown = _layouts.Layout(_columns, 16) with { Line = new ColumnLine(BorderLineStyle.Double, 2, "#DC2626", 80, ColumnLinePosition.Bottom) };

        var written = _layouts.Columns(shown, true, _columns).SeparatorLine;

        Assert.Equal(new ColumnSeparatorLine(BorderLineStyle.Double, 2, 0xFFDC2626) { HeightPercent = 80, Alignment = ColumnSeparatorAlignment.Bottom }, written);
    }

    [Fact]
    public void An_unedited_separator_line_is_kept_as_stored()
    {
        Assert.Same(_columns.SeparatorLine, _layouts.Columns(_layouts.Layout(_columns, 16), true, _columns).SeparatorLine);
    }
}
