using PulseOffice.Platform;

namespace PulseOffice.Settings;

public interface IUserDataPaths
{
    string ConfigurationDirectory { get; }

    string CacheDirectory { get; }

    string SettingsFile { get; }

    string RecoveryDirectory { get; }

    string Configuration(string name);
}

public sealed class UserDataPaths(IHostSystem host) : IUserDataPaths
{
    public UserDataPaths()
        : this(new RuntimeHostSystem())
    {
    }

    public const string ApplicationName = "PulseOffice";

    public const string DataHomeVariable = "PULSEOFFICE_DATA_HOME";

    public string ConfigurationDirectory
    {
        get
        {
            if (DataHome is { } home) return Path.Combine(home, "config");

            if (host.IsMacOS)
                return Path.Combine(Home, "Library", "Application Support", ApplicationName);

            if (host.IsLinux)
            {
                var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? string.Empty;
                if (!Path.IsPathFullyQualified(config)) config = Path.Combine(Home, ".config");
                return Path.Combine(config, ApplicationName);
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationName);
        }
    }

    public string CacheDirectory
    {
        get
        {
            if (DataHome is { } home) return Path.Combine(home, "cache");

            if (host.IsMacOS)
                return Path.Combine(Home, "Library", "Caches", ApplicationName);

            if (host.IsLinux)
            {
                var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? string.Empty;
                if (!Path.IsPathFullyQualified(cache)) cache = Path.Combine(Home, ".cache");
                return Path.Combine(cache, ApplicationName);
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationName, "cache");
        }
    }

    public string SettingsFile => Configuration("settings.json");

    public string RecoveryDirectory => Path.Combine(CacheDirectory, "recovery");

    public string Configuration(string name) => Path.Combine(ConfigurationDirectory, name);

    private string? DataHome =>
        Environment.GetEnvironmentVariable(DataHomeVariable) is { Length: > 0 } home ? home : null;

    private string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
