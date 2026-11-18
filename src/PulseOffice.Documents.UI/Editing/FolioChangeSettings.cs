using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;

namespace PulseOffice.Documents.UI.Editing;

public sealed class FolioChangeSettings : ISettingsProvider
{
    public const string MoveColor = "folio.changes.moveColor";
    public const string MovedFrom = "folio.changes.movedFrom";
    public const string MovedTo = "folio.changes.movedTo";

    public IEnumerable<SettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = MoveColor,
            Kind = SettingKind.Choice,
            DefaultValue = "green",
            Category = "documents/changes",
            Module = ModuleKind.Documents,
            Choices = [new("green"), new("byAuthor"), new("blue"), new("violet"), new("red")],
            Keywords = ["move", "track changes", "colour", "color", "revision"],
        },
        new()
        {
            Key = MovedFrom,
            Kind = SettingKind.Choice,
            DefaultValue = "doubleStrikethrough",
            Category = "documents/changes",
            Module = ModuleKind.Documents,
            Choices = [new("doubleStrikethrough"), new("strikethrough")],
            Keywords = ["move", "track changes", "strikethrough", "revision"],
        },
        new()
        {
            Key = MovedTo,
            Kind = SettingKind.Choice,
            DefaultValue = "doubleUnderline",
            Category = "documents/changes",
            Module = ModuleKind.Documents,
            Choices = [new("doubleUnderline"), new("underline"), new("bold")],
            Keywords = ["move", "track changes", "underline", "revision"],
        },
    ];
}

public sealed class SettingsMoveDisplayOptions : IMoveDisplayOptions
{
    private readonly ISettingsService _settings;

    public SettingsMoveDisplayOptions(ISettingsService settings)
    {
        _settings = settings;
        _settings.Changed += OnSettingChanged;
    }

    public event EventHandler? Changed;

    private readonly IReadOnlyDictionary<string, uint?> _colors = new Dictionary<string, uint?>(StringComparer.Ordinal)
    {
        ["green"] = 0xFF008A00,
        ["byAuthor"] = null,
        ["blue"] = 0xFF2563EB,
        ["violet"] = 0xFF7C3AED,
        ["red"] = 0xFFDC2626,
    };

    public MoveDisplay Current => new(
        _colors.TryGetValue(Text(FolioChangeSettings.MoveColor), out var color) ? color : 0xFF008A00,
        Text(FolioChangeSettings.MovedFrom) == "strikethrough" ? MovedFromMark.Strikethrough : MovedFromMark.DoubleStrikethrough,
        Text(FolioChangeSettings.MovedTo) switch
        {
            "underline" => MovedToMark.Underline,
            "bold" => MovedToMark.Bold,
            _ => MovedToMark.DoubleUnderline,
        });

    private string Text(string key) => _settings.Get<string>(key) ?? string.Empty;

    private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        if (e.Key is FolioChangeSettings.MoveColor or FolioChangeSettings.MovedFrom or FolioChangeSettings.MovedTo) Changed?.Invoke(this, EventArgs.Empty);
    }
}
