using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Settings;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round204MoveDisplaySettingsTests
{
    [Fact]
    public void Changing_the_move_colour_notifies_the_open_documents()
    {
        var settings = new SettingsService([new FolioChangeSettings()], [new InMemorySettingsStore(SettingScope.User)], new TextCatalog(UiLanguage.English), NullLogger<SettingsService>.Instance);
        var moves = new SettingsMoveDisplayOptions(settings);
        var raised = 0;
        moves.Changed += (_, _) => raised++;

        settings.Set(FolioChangeSettings.MoveColor, "blue");
        settings.Set(FolioChangeSettings.MovedTo, "bold");

        Assert.Equal(2, raised);
        Assert.Equal(0xFF2563EBu, moves.Current.Color);
        Assert.Equal(PulseOffice.Documents.Layout.Flow.MovedToMark.Bold, moves.Current.To);
    }
}
