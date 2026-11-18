using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round191ListSeparatorDialogTests
{
    private readonly FolioListForms _forms = new(new ListFormatTabs(null!, null!, null!, new TextCatalog(UiLanguage.English)), new TextCatalog(UiLanguage.English));

    [Fact]
    public void The_dialog_edits_the_level_separator_and_drops_the_gap_list()
    {
        var sheet = Sheet();
        var levels = sheet.List.Find("L")!.Levels;
        var edited = new ListLevelsAnswer([.. levels.Select((level, index) => new ListLevelFormat(ListNumbering.Arabic) { DisplayLevels = level.DisplayLevels, Separator = index == 1 ? "-" : level.Separator })], 1);

        var (_, read, _, _) = _forms.ReadList(sheet, "L", new Dictionary<string, object> { [ListFormatTabs.LevelsKey] = edited, ["apply"] = false });

        Assert.Equal("-", read[1].Separator);
        Assert.Empty(read[1].Separators);
    }

    [Fact]
    public void An_untouched_separator_keeps_the_gap_list()
    {
        var sheet = Sheet();
        var levels = sheet.List.Find("L")!.Levels;
        var edited = new ListLevelsAnswer([.. levels.Select(level => new ListLevelFormat(ListNumbering.Arabic) { DisplayLevels = level.DisplayLevels, Separator = level.Separator })], 1);

        var (_, read, _, _) = _forms.ReadList(sheet, "L", new Dictionary<string, object> { [ListFormatTabs.LevelsKey] = edited });

        Assert.Same(levels[1].Separators, read[1].Separators);
    }

    [Fact]
    public void The_labeler_joins_with_the_level_separator()
    {
        var label = new ListLabeler().Label([new ListLevelFormat(ListNumbering.Arabic), new ListLevelFormat(ListNumbering.Arabic) { DisplayLevels = 2, Separator = "-", Suffix = string.Empty }], 1, [3, 4]);

        Assert.Equal("3-4", label);
    }

    private static StyleSheet Sheet()
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.Arabic), new ListLevel(NumberFormat.Arabic) { DisplayLevels = 2, Separators = ["/"] }]));
        return sheet;
    }
}
