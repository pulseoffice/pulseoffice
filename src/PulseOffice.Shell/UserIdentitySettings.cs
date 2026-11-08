using PulseOffice.Foundation.Identity;
using PulseOffice.Settings;

namespace PulseOffice.Shell;

public static class UserIdentitySettingKeys
{
    public const string Name = "user.name";
    public const string Initials = "user.initials";
}

public sealed class UserIdentitySettings : ISettingsProvider
{
    public IEnumerable<SettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = UserIdentitySettingKeys.Name,
            Kind = SettingKind.Text,
            DefaultValue = string.Empty,
            Category = "general",
            Keywords = ["author", "comments", "changes"],
        },
        new()
        {
            Key = UserIdentitySettingKeys.Initials,
            Kind = SettingKind.Text,
            DefaultValue = string.Empty,
            Category = "general",
        },
    ];
}

public sealed class SettingsUserIdentity(ISettingsService settings, SystemUserIdentity system) : IUserIdentity
{
    private readonly InitialsOfName _initials = new();

    public string Name => Setting(UserIdentitySettingKeys.Name) ?? system.Name;

    public string Initials => Setting(UserIdentitySettingKeys.Initials) ?? (Setting(UserIdentitySettingKeys.Name) is { } name ? _initials.Of(name) : system.Initials);

    private string? Setting(string key) => settings.Get<string>(key)?.Trim() is { Length: > 0 } value ? value : null;
}
