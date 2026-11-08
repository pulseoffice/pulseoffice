using PulseOffice.Settings;

namespace PulseOffice.Agent.Security;

public static class AgentSettingKeys
{
    public const string Enabled = "agent.enabled";
    public const string Port = "agent.port";
    public const string Level = "agent.level";
    public const string Connection = "agent.connection";
}

public sealed class AgentSettings : ISettingsProvider
{
    public const int DefaultPort = 47820;

    public IEnumerable<SettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = AgentSettingKeys.Enabled,
            Kind = SettingKind.Boolean,
            DefaultValue = false,
            Category = "agent",
            Keywords = ["mcp", "ai", "claude"],
        },
        new()
        {
            Key = AgentSettingKeys.Level,
            Kind = SettingKind.Choice,
            DefaultValue = "read",
            Category = "agent",
            Choices = [new SettingChoice("read"), new SettingChoice("edit"), new SettingChoice("files")],
        },
        new()
        {
            Key = AgentSettingKeys.Port,
            Kind = SettingKind.Integer,
            DefaultValue = DefaultPort,
            Minimum = 1024,
            Maximum = 65535,
            Category = "agent",
            IsAdvanced = true,
        },
        new()
        {
            Key = AgentSettingKeys.Connection,
            Kind = SettingKind.Text,
            DefaultValue = string.Empty,
            Category = "agent",
        },
    ];
}

public interface IAgentPreferences
{
    bool Enabled { get; }

    int Port { get; }

    AgentAccessLevel Level { get; }

    event EventHandler? Changed;
}

public sealed class AgentPreferences : IAgentPreferences
{
    private readonly ISettingsService _settings;

    public AgentPreferences(ISettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Changed += (_, args) =>
        {
            if (args.Key is AgentSettingKeys.Enabled or AgentSettingKeys.Port or AgentSettingKeys.Level)
                Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    public bool Enabled => _settings.Get<bool>(AgentSettingKeys.Enabled);

    public int Port => Convert.ToInt32(_settings.Get<object>(AgentSettingKeys.Port), System.Globalization.CultureInfo.InvariantCulture);

    public AgentAccessLevel Level => _settings.Get<string>(AgentSettingKeys.Level) switch
    {
        "files" => AgentAccessLevel.Files,
        "edit" => AgentAccessLevel.Edit,
        _ => AgentAccessLevel.Read,
    };

    public event EventHandler? Changed;
}
