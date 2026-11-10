using Xunit;
using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Settings;
using PulseOffice.Shell.Desktop;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
[UnsupportedOSPlatform("windows")]
public sealed class AppImageDesktopIntegrationTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("pulse-desktop-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private sealed class Location(string path) : IAppImageLocation
    {
        public string? Current { get; private set; } = path;

        public void MovedTo(string moved) => Current = moved;
    }

    private sealed class Environment(IReadOnlyDictionary<string, string> values) : IEnvironmentVariables
    {
        public string? Get(string name) => values.GetValueOrDefault(name);
    }

    private sealed class NoProcesses : IExternalProcess
    {
        public Task<ProcessOutcome?> RunAsync(string executable, IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment = null, string? input = null, CancellationToken ct = default) =>
            Task.FromResult<ProcessOutcome?>(null);
    }

    private (AppImageDesktopIntegration Integration, Location Location, string Image) Build()
    {
        var downloads = Directory.CreateDirectory(Path.Combine(_home, "Downloads")).FullName;
        var image = Path.Combine(downloads, "PulseOffice-0.3.0-linux-x86_64.AppImage");
        File.WriteAllText(image, "#!/bin/sh\necho launched \"$0\" \"$@\"\n");
        File.SetUnixFileMode(image, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var appDir = Directory.CreateDirectory(Path.Combine(_home, "appdir")).FullName;
        File.WriteAllBytes(Path.Combine(appDir, "io.pulseoffice.PulseOffice.png"), [1, 2, 3]);
        var location = new Location(image);
        var environment = new Environment(new Dictionary<string, string> { ["HOME"] = _home, ["APPDIR"] = appDir });
        return (new AppImageDesktopIntegration(location, environment, new NoProcesses(), new AppImageLauncherScript()), location, image);
    }

    private string Share(params string[] parts) => Path.Combine([_home, ".local", "share", .. parts]);

    [Fact]
    public void Integrating_writes_menu_entry_launcher_icon_and_registered_path()
    {
        var (integration, _, image) = Build();
        Assert.False(integration.IsIntegrated);

        integration.Integrate();

        Assert.True(integration.IsIntegrated);
        Assert.Equal(image, integration.RegisteredImage);
        var entry = File.ReadAllText(Share("applications", "io.pulseoffice.PulseOffice.desktop"));
        Assert.Contains($"Exec=\"{Share("PulseOffice", "launch")}\" %F", entry);
        Assert.Contains("application/vnd.oasis.opendocument.text;", entry);
        Assert.True(File.Exists(Share("icons", "hicolor", "256x256", "apps", "io.pulseoffice.PulseOffice.png")));
        Assert.True((File.GetUnixFileMode(Share("PulseOffice", "launch")) & UnixFileMode.UserExecute) != 0);
    }

    [Fact]
    public void Moving_to_applications_follows_the_running_image_and_the_registration()
    {
        var (integration, location, image) = Build();
        integration.Integrate();

        var moved = integration.MoveToApplicationsFolder();

        Assert.Equal(Path.Combine(_home, "Applications", Path.GetFileName(image)), moved);
        Assert.False(File.Exists(image));
        Assert.Equal(moved, location.Current);
        Assert.Equal(moved, integration.RegisteredImage);
        Assert.True(integration.IsInApplicationsFolder);
    }

    [Fact]
    public void Removing_deletes_every_file()
    {
        var (integration, _, _) = Build();
        integration.Integrate();
        integration.Remove();

        Assert.False(integration.IsIntegrated);
        Assert.False(File.Exists(Share("PulseOffice", "appimage-path")));
        Assert.False(File.Exists(Share("icons", "hicolor", "256x256", "apps", "io.pulseoffice.PulseOffice.png")));
    }

    private string Launch()
    {
        var start = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Share("PulseOffice", "launch"));
        start.ArgumentList.Add("report.odt");
        start.Environment["HOME"] = _home;
        start.Environment["PATH"] = "/usr/bin:/bin";
        start.Environment.Remove("XDG_DATA_HOME");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    [Fact]
    public void The_launcher_starts_the_registered_image_with_the_arguments()
    {
        var (integration, _, image) = Build();
        integration.Integrate();
        Assert.Equal($"launched {image} report.odt", Launch());
    }

    [Fact]
    public void The_launcher_finds_an_image_moved_to_applications()
    {
        var (integration, _, image) = Build();
        integration.Integrate();
        var applications = Directory.CreateDirectory(Path.Combine(_home, "Applications")).FullName;
        var moved = Path.Combine(applications, Path.GetFileName(image));
        File.Move(image, moved);

        Assert.Equal($"launched {moved} report.odt", Launch());
    }

    [Fact]
    public void The_launcher_gives_up_quietly_when_nothing_is_found_and_no_dialog_tool_exists()
    {
        var (integration, _, image) = Build();
        integration.Integrate();
        File.Delete(image);

        Assert.Equal(string.Empty, Launch());
    }

    [Fact]
    public void The_launcher_never_runs_a_downloaded_image_that_is_not_executable()
    {
        var (integration, _, image) = Build();
        integration.Integrate();
        File.Delete(image);
        var planted = Path.Combine(_home, "Downloads", "PulseOffice-9.9.9-linux-x86_64.AppImage");
        File.WriteAllText(planted, "#!/bin/sh\necho planted\n");

        Assert.Equal(string.Empty, Launch());
        Assert.Equal(0, (int)(File.GetUnixFileMode(planted) & UnixFileMode.UserExecute));
    }

    [Fact]
    public void The_launcher_never_runs_a_registered_path_replaced_by_a_file_that_is_not_executable()
    {
        var (integration, _, image) = Build();
        integration.Integrate();
        File.Delete(image);
        File.WriteAllText(image, "#!/bin/sh\necho planted\n");

        Assert.Equal(string.Empty, Launch());
    }

    [Fact]
    public void The_menu_entry_escapes_the_launcher_path_as_the_desktop_entry_specification_requires()
    {
        var data = Directory.CreateDirectory(Path.Combine(_home, "da\"ta $x `y` \\z 100%")).FullName;
        var (_, location, _) = Build();
        var integration = new AppImageDesktopIntegration(location,
            new Environment(new Dictionary<string, string> { ["HOME"] = _home, ["XDG_DATA_HOME"] = data }), new NoProcesses(), new AppImageLauncherScript());

        integration.Integrate();

        var entry = File.ReadAllLines(Path.Combine(data, "applications", "io.pulseoffice.PulseOffice.desktop"));
        var execQuoted = string.Concat(Path.Combine(data, "PulseOffice", "launch")
            .Select(character => character is '"' or '`' or '$' or '\\' ? "\\" + character : character.ToString()));
        var expected = "Exec=\"" + execQuoted.Replace("%", "%%", StringComparison.Ordinal).Replace("\\", "\\\\", StringComparison.Ordinal) + "\" %F";
        Assert.Contains("\\\\\\\\z", expected);
        Assert.Contains(expected, entry);
    }

    [Fact]
    public void A_data_folder_with_a_line_break_is_refused()
    {
        var (_, location, _) = Build();
        var integration = new AppImageDesktopIntegration(location,
            new Environment(new Dictionary<string, string> { ["HOME"] = _home, ["XDG_DATA_HOME"] = Path.Combine(_home, "a\nExec=evil") }), new NoProcesses(), new AppImageLauncherScript());

        Assert.Throws<InvalidOperationException>(integration.Integrate);
    }
}

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DesktopIntegrationCoordinatorTests(HeadlessDesktopSession headless) : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("pulse-desktop-prefs-").FullName;

    private UiThreadSession Session => headless.Session;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private sealed class FakeIntegration : IDesktopIntegration
    {
        public bool IsAvailable { get; init; } = true;

        public bool IsIntegrated { get; set; }

        public string? CurrentImage { get; set; } = "/home/u/Downloads/PulseOffice.AppImage";

        public string? RegisteredImage { get; set; }

        public string ApplicationsFolder => "/home/u/Applications";

        public bool IsInApplicationsFolder => CurrentImage?.StartsWith(ApplicationsFolder, StringComparison.Ordinal) == true;

        public int Registrations { get; private set; }

        public void Integrate() => IsIntegrated = true;

        public void Remove() => IsIntegrated = false;

        public void RegisterCurrentLocation()
        {
            Registrations++;
            RegisteredImage = CurrentImage;
        }

        public string MoveToApplicationsFolder() => CurrentImage = ApplicationsFolder + "/PulseOffice.AppImage";
    }

    private sealed class CountingDialogs : IDialogService
    {
        public int Shown { get; private set; }

        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            Shown++;
            return Task.FromResult<TResult?>(default);
        }

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private sealed class Paths(string home) : IUserDataPaths
    {
        public string ConfigurationDirectory => home;

        public string CacheDirectory => home;

        public string SettingsFile => Path.Combine(home, "settings.json");

        public string RecoveryDirectory => home;

        public string Configuration(string name) => Path.Combine(home, name);
    }

    private (DesktopIntegrationCoordinator Coordinator, CountingDialogs Dialogs, JsonDesktopIntegrationPreferencesStore Store) Build(FakeIntegration integration)
    {
        var dialogs = new CountingDialogs();
        var store = new JsonDesktopIntegrationPreferencesStore(new Paths(_home));
        var coordinator = new DesktopIntegrationCoordinator(integration, store, dialogs, new NotificationCenter(TimeProvider.System),
            new TextCatalog(UiLanguage.Italian), NullLogger<DesktopIntegrationCoordinator>.Instance);
        return (coordinator, dialogs, store);
    }

    [Fact]
    public Task Asks_to_integrate_once_and_never_after_a_refusal_for_good() => Session.Dispatch(async () =>
    {
        var integration = new FakeIntegration();
        var (coordinator, dialogs, store) = Build(integration);
        await coordinator.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogs.Shown);

        store.Save(new DesktopIntegrationPreferences(Declined: true, QuietRelocation: false));
        await coordinator.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogs.Shown);
    }, CancellationToken.None);

    [Fact]
    public Task A_moved_image_is_asked_about_unless_quiet_or_already_in_applications() => Session.Dispatch(async () =>
    {
        var integration = new FakeIntegration { IsIntegrated = true, RegisteredImage = "/home/u/old/PulseOffice.AppImage" };
        var (coordinator, dialogs, store) = Build(integration);

        await coordinator.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogs.Shown);
        Assert.Equal(1, integration.Registrations);

        integration.RegisteredImage = "/elsewhere";
        store.Save(new DesktopIntegrationPreferences(Declined: false, QuietRelocation: true));
        await coordinator.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogs.Shown);
        Assert.Equal(2, integration.Registrations);

        store.Save(new DesktopIntegrationPreferences(false, false));
        integration.CurrentImage = "/home/u/Applications/PulseOffice.AppImage";
        integration.RegisteredImage = "/elsewhere";
        await coordinator.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogs.Shown);
        Assert.Equal(3, integration.Registrations);
    }, CancellationToken.None);

    [Fact]
    public Task Nothing_happens_outside_an_appimage() => Session.Dispatch(async () =>
    {
        var (coordinator, dialogs, _) = Build(new FakeIntegration { IsAvailable = false });
        await coordinator.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, dialogs.Shown);
        Assert.False(coordinator.IsAvailable);
    }, CancellationToken.None);
}
