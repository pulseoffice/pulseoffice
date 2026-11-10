using System.Runtime.Versioning;
using PulseOffice.Platform;

namespace PulseOffice.Platform.Linux;

[UnsupportedOSPlatform("windows")]
public sealed class AppImageDesktopIntegration(
    IAppImageLocation location,
    IEnvironmentVariables environment,
    IExternalProcess processes,
    IAppImageLauncherScript launcher) : IDesktopIntegration
{
    private const string ApplicationId = "io.pulseoffice.PulseOffice";

    private static readonly string[] MediaTypes =
    [
        "application/vnd.oasis.opendocument.text",
        "application/vnd.oasis.opendocument.spreadsheet",
        "application/vnd.oasis.opendocument.presentation",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/rtf",
        "text/csv",
    ];

    private const UnixFileMode Executable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private string Home => environment.Get("HOME") is { Length: > 0 } home ? home : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private string DataHome => environment.Get("XDG_DATA_HOME") is { Length: > 0 } data && Path.IsPathFullyQualified(data)
        ? data
        : Path.Combine(Home, ".local", "share");

    private string OwnFolder => Path.Combine(DataHome, "PulseOffice");

    private string PathFile => Path.Combine(OwnFolder, "appimage-path");

    private string LauncherFile => Path.Combine(OwnFolder, "launch");

    private string DesktopFolder => Path.Combine(DataHome, "applications");

    private string DesktopFile => Path.Combine(DesktopFolder, ApplicationId + ".desktop");

    private string IconFile => Path.Combine(DataHome, "icons", "hicolor", "256x256", "apps", ApplicationId + ".png");

    public bool IsAvailable => location.Current is not null;

    public bool IsIntegrated => File.Exists(DesktopFile) && File.Exists(LauncherFile);

    public string? CurrentImage => location.Current;

    public string? RegisteredImage => File.Exists(PathFile) ? File.ReadAllText(PathFile).Trim() : null;

    public string ApplicationsFolder => Path.Combine(Home, "Applications");

    public bool IsInApplicationsFolder =>
        CurrentImage is { } image && string.Equals(Path.GetDirectoryName(image), ApplicationsFolder, StringComparison.Ordinal);

    public void Integrate()
    {
        var image = CurrentImage ?? throw new InvalidOperationException("PulseOffice is not running from an AppImage.");
        var entry = DesktopEntry();
        Directory.CreateDirectory(OwnFolder);
        Directory.CreateDirectory(DesktopFolder);
        WriteAtomically(PathFile, image + "\n");
        WriteAtomically(LauncherFile, launcher.Text(PathFile));
        File.SetUnixFileMode(LauncherFile, Executable);
        WriteAtomically(DesktopFile, entry);
        if (environment.Get("APPDIR") is { Length: > 0 } appDir && File.Exists(Path.Combine(appDir, ApplicationId + ".png")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IconFile)!);
            File.Copy(Path.Combine(appDir, ApplicationId + ".png"), IconFile, overwrite: true);
        }
        Refresh(registerTypes: true);
    }

    public void Remove()
    {
        foreach (var file in new[] { DesktopFile, LauncherFile, PathFile, IconFile })
            if (File.Exists(file)) File.Delete(file);
        Refresh(registerTypes: false);
    }

    public void RegisterCurrentLocation()
    {
        var image = CurrentImage ?? throw new InvalidOperationException("PulseOffice is not running from an AppImage.");
        Directory.CreateDirectory(OwnFolder);
        WriteAtomically(PathFile, image + "\n");
    }

    public string MoveToApplicationsFolder()
    {
        var image = CurrentImage ?? throw new InvalidOperationException("PulseOffice is not running from an AppImage.");
        Directory.CreateDirectory(ApplicationsFolder);
        var target = Path.Combine(ApplicationsFolder, Path.GetFileName(image));
        if (!string.Equals(image, target, StringComparison.Ordinal))
        {
            File.Move(image, target, overwrite: true);
            File.SetUnixFileMode(target, Executable);
            location.MovedTo(target);
        }
        if (IsIntegrated) RegisterCurrentLocation();
        return target;
    }

    private string DesktopEntry() =>
        "[Desktop Entry]\n" +
        "Type=Application\n" +
        "Name=PulseOffice\n" +
        "GenericName=Office Suite\n" +
        "Comment=Documents, spreadsheets and presentations\n" +
        $"Exec=\"{ExecArgument(LauncherFile)}\" %F\n" +
        $"Icon={ApplicationId}\n" +
        "Terminal=false\n" +
        "Categories=Office;\n" +
        $"MimeType={string.Join(';', MediaTypes)};\n" +
        "StartupWMClass=PulseOffice\n";

    private static string ExecArgument(string path)
    {
        if (path.Any(char.IsControl))
            throw new InvalidOperationException("The menu entry cannot point to a path that contains control characters.");
        var quoted = new System.Text.StringBuilder(path.Length + 8);
        foreach (var character in path)
        {
            if (character is '"' or '`' or '$' or '\\') quoted.Append('\\');
            quoted.Append(character);
        }
        return quoted.ToString()
            .Replace("%", "%%", StringComparison.Ordinal)
            .Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private void Refresh(bool registerTypes)
    {
        Run("update-desktop-database", [DesktopFolder]);
        Run("gtk-update-icon-cache", ["-q", "-t", Path.Combine(DataHome, "icons", "hicolor")]);
        if (registerTypes) Run("xdg-mime", ["default", ApplicationId + ".desktop", .. MediaTypes]);
    }

    private void Run(string executable, IReadOnlyList<string> arguments)
    {
        try
        {
            processes.RunAsync(executable, arguments).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }
}
