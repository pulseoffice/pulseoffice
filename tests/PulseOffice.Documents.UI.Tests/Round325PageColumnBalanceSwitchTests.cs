using PulseOffice.Documents.Editing.Borders;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Pages;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round325PageColumnBalanceSwitchTests
{
    private readonly FolioPageTabs _tabs =
        new(new FolioPageServices(null!, null!, null!, null!, null!, new BoxBorderFrames(), null!, null!, null!, new TextCatalog(UiLanguage.English), new FolioColumnLayouts()));

    [Theory]
    [InlineData(2, true, true)]
    [InlineData(2, false, false)]
    [InlineData(1, true, false)]
    public void The_columns_tab_switch_marks_page_columns_to_balance_before_a_section(int count, bool switched, bool marked)
    {
        var style = new PageStyle("Default");
        var values = new Dictionary<string, object>
        {
            ["size"] = 0,
            ["orientation"] = 0,
            ["columnLayout"] = new ColumnLayout(count, 1, false),
            ["balanceBeforeSection"] = switched,
        };

        _tabs.Write(style, [], values);

        Assert.Equal(marked, style.Columns.BalancedBeforeSection);
    }
}
