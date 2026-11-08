using System.Text;
using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.Windows;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class PlatformRound23Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"platform-round23-{Guid.NewGuid():N}");

    [Fact]
    public async Task SecretToolNamesStartingWithADashComeAfterTheEndOfOptions()
    {
        var process = new RecordingProcess();
        var vault = new SecretToolVault(process, new FixedResolver());

        await vault.WriteAsync("-service", "--label", "secret", TestContext.Current.CancellationToken);
        await vault.ReadAsync("-service", "-a", TestContext.Current.CancellationToken);
        await vault.RemoveAsync("-service", "-a", TestContext.Current.CancellationToken);

        Assert.Equal(3, process.Calls.Count);
        foreach (var arguments in process.Calls)
        {
            var separator = arguments.IndexOf("--");
            Assert.True(separator > 0);
            Assert.All(arguments.Take(separator).Skip(1), argument => Assert.StartsWith("--label=", argument, StringComparison.Ordinal));
            Assert.Equal("service", arguments[separator + 1]);
            Assert.Equal("-service", arguments[separator + 2]);
        }
        Assert.Equal("secret", process.Inputs[0]);
        Assert.Equal("/usr/bin/secret-tool", process.Executables[0]);
    }

    [Fact]
    public async Task SecretToolThatCannotStartReportsFailureInsteadOfThrowing()
    {
        var vault = new SecretToolVault(new ThrowingProcess(), new FixedResolver());

        Assert.False(await vault.WriteAsync("PulseOffice", "key", "secret", TestContext.Current.CancellationToken));
        Assert.Null(await vault.ReadAsync("PulseOffice", "key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void LinuxFontsFollowXdgDataDirsAndFlatpakHostFolders()
    {
        var environment = new FixedEnvironment(new Dictionary<string, string>
        {
            ["XDG_DATA_DIRS"] = "/app/share:relative/share",
            ["XDG_DATA_HOME"] = Path.Combine(_root, "home-data"),
        });

        var candidates = new SystemFontDirectories(environment, PlatformFamily.Linux).Candidates();

        Assert.Contains("/app/share/fonts", candidates);
        Assert.Contains("/run/host/fonts", candidates);
        Assert.Contains("/run/host/user-fonts", candidates);
        Assert.Contains(Path.Combine(_root, "home-data", "fonts"), candidates);
        Assert.DoesNotContain(candidates, candidate => candidate.StartsWith("relative", StringComparison.Ordinal));
    }

    [Fact]
    public void LinuxFontsWithoutXdgDataDirsUseTheStandardFolders()
    {
        var candidates = new SystemFontDirectories(new FixedEnvironment(new Dictionary<string, string>()), PlatformFamily.Linux).Candidates();

        Assert.Contains("/usr/share/fonts", candidates);
        Assert.Contains("/usr/local/share/fonts", candidates);
    }

    [Fact]
    public void LinuxDictionariesFollowXdgDataDirs()
    {
        var environment = new FixedEnvironment(new Dictionary<string, string>
        {
            ["XDG_DATA_DIRS"] = "/app/share",
        });

        var directories = new SystemDictionaryDirectories(environment, PlatformFamily.Linux).Directories;

        Assert.Contains("/app/share/hunspell", directories);
        Assert.DoesNotContain("/usr/share/hunspell", directories);
    }

    [Fact]
    public void WindowsHtmlFragmentIsNotCutByAStrayEndMarker()
    {
        var formats = new WindowsClipboardFormats();
        var html = "<p>prima <!--EndFragment--> dopo</p>";

        var bytes = formats.EncodeHtml(html);
        var encoded = Encoding.UTF8.GetString(bytes);
        var start = Offset(encoded, "StartFragment:");
        var end = Offset(encoded, "EndFragment:");

        Assert.Equal(html, Encoding.UTF8.GetString(bytes, start, end - start));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static int Offset(string header, string key)
    {
        var at = header.IndexOf(key, StringComparison.Ordinal) + key.Length;
        return int.Parse(header.AsSpan(at, 10), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class RecordingProcess : IExternalProcess
    {
        public List<List<string>> Calls { get; } = [];

        public List<string?> Inputs { get; } = [];

        public List<string> Executables { get; } = [];

        public Task<ProcessOutcome?> RunAsync(string executable, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null, string? input = null, CancellationToken ct = default)
        {
            Executables.Add(executable);
            Calls.Add([.. arguments]);
            Inputs.Add(input);
            return Task.FromResult<ProcessOutcome?>(new ProcessOutcome(0, "value", string.Empty));
        }
    }

    private sealed class ThrowingProcess : IExternalProcess
    {
        public Task<ProcessOutcome?> RunAsync(string executable, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null, string? input = null, CancellationToken ct = default) =>
            throw new System.ComponentModel.Win32Exception(2);
    }

    private sealed class FixedResolver : IExecutableResolver
    {
        public string? Resolve(string executable) => "/usr/bin/" + executable;
    }

    private sealed class FixedEnvironment(IReadOnlyDictionary<string, string> values) : IEnvironmentVariables
    {
        public string? Get(string name) => values.GetValueOrDefault(name);
    }
}
