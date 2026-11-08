using PulseOffice.Platform;

namespace PulseOffice.Diagnostics;

public static class LogPaths
{
    public const string ApplicationName = "PulseOffice";

    public const string DataHomeVariable = "PULSEOFFICE_DATA_HOME";

    private static readonly IHostSystem Host = new RuntimeHostSystem();

    public static string LogDirectory
    {
        get
        {
            if (Environment.GetEnvironmentVariable(DataHomeVariable) is { Length: > 0 } isolated)
                return Path.Combine(isolated, "logs");

            if (Host.IsMacOS)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Logs", ApplicationName);
            }

            if (Host.IsLinux)
            {
                var state = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
                if (!Path.IsPathFullyQualified(state ?? string.Empty))
                    state = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
                return Path.Combine(state!, ApplicationName, "logs");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationName, "logs");
        }
    }

    public static string CrashDirectory => Path.Combine(LogDirectory, "crash");

    public static string EnsureLogDirectory()
    {
        var path = LogDirectory;
        Directory.CreateDirectory(path);
        return path;
    }
}
