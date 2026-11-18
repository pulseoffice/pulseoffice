using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerLinkSettings : ISettingsProvider
{
    public const string FollowOnClick = "ledger.links.followOnClick";

    public IEnumerable<SettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = FollowOnClick,
            Kind = SettingKind.Boolean,
            DefaultValue = true,
            Category = "documents/spreadsheets",
            Module = ModuleKind.Sheets,
            Keywords = ["hyperlink", "link", "click", "spreadsheet"],
        },
    ];
}

public sealed class SettingsCellLinkClicking(ISettingsService settings) : ICellLinkClicking
{
    public bool FollowsOnClick => settings.Get<bool>(LedgerLinkSettings.FollowOnClick);
}
