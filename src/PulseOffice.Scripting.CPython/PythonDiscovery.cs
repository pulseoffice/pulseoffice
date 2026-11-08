using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using PulseOffice.Platform;

namespace PulseOffice.Scripting.CPython;

public interface IPythonDiscovery
{
    IReadOnlyList<PythonRuntimeDescriptor> DiscoverSystemInstallations();

    PythonRuntimeDescriptor? FindBest();

    PythonDiscoveryResult Inspect(string executablePath);
}

public sealed class PythonVersionRange
{
    public static readonly Version MinimumVersion = new(3, 10);
    public static readonly Version MaximumVersion = new(3, 14);
    public static readonly Version PreferredVersion = new(3, 13);

    public bool IsSupported(Version version)
    {
        var feature = new Version(version.Major, version.Minor);
        return feature >= MinimumVersion && feature <= MaximumVersion;
    }
}

public sealed class PythonDiscovery : IPythonDiscovery
{
    private readonly PythonVersionRange _range = new();
    private readonly IReadOnlyList<PythonCandidateSource> _sources;
    private readonly PythonInterpreterProbe _probe = new();
    private readonly PythonDescriptorReader _reader;

    public PythonDiscovery()
        : this(new RuntimeHostSystem())
    {
    }

    public PythonDiscovery(IHostSystem host)
    {
        var path = new ExecutableSearchPath(host);
        var names = new PythonExecutableNames();
        _sources = [new PathSource(path, names), new WindowsInstallSource(host), new MacInstallSource(names, host), new LinuxInstallSource(host)];
        _reader = new PythonDescriptorReader(_range,
            new LibraryLocator[] { new WindowsLibraryLocator(host), new MacLibraryLocator(host), new LinuxLibraryLocator() }
                .First(locator => locator.Applies));
    }

    public IReadOnlyList<PythonRuntimeDescriptor> DiscoverSystemInstallations()
    {
        var found = new List<PythonRuntimeDescriptor>();

        foreach (var executable in _sources.Where(source => source.Applies).SelectMany(source => source.Candidates()))
            if (Inspect(executable).Runtime is { } runtime && found.All(existing => existing.Home != runtime.Home))
                found.Add(runtime);

        return [.. found.OrderByDescending(runtime => runtime.Version)];
    }

    public PythonRuntimeDescriptor? FindBest() =>
        DiscoverSystemInstallations()
            .Where(runtime => runtime.MatchesCurrentProcess)
            .FirstOrDefault(runtime => _range.IsSupported(runtime.Version));

    public PythonDiscoveryResult Inspect(string executablePath)
    {
        if (!File.Exists(executablePath))
            return new PythonDiscoveryResult(null, $"Executable not found: {executablePath}");

        var probe = _probe.Run(executablePath);
        return probe.Output is { } output ? _reader.Read(executablePath, output) : new PythonDiscoveryResult(null, probe.Error);
    }
}

internal sealed class ExecutableSearchPath(IHostSystem host)
{
    public string? Find(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        var executable = host.ExecutableName(name);
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, executable))
            .FirstOrDefault(File.Exists);
    }
}

internal abstract class PythonCandidateSource
{
    public abstract bool Applies { get; }

    public abstract IEnumerable<string> Candidates();

    protected IEnumerable<string> Directories(string root, string pattern) =>
        Directory.Exists(root) ? Directory.EnumerateDirectories(root, pattern) : [];
}

internal sealed class PythonExecutableNames
{
    public IReadOnlyList<string> All { get; } =
    [
        "python3",
        .. Enumerable.Range(PythonVersionRange.MinimumVersion.Minor, PythonVersionRange.MaximumVersion.Minor - PythonVersionRange.MinimumVersion.Minor + 1)
            .Reverse()
            .Select(minor => $"python3.{minor}"),
        "python",
    ];
}

internal sealed class PathSource(ExecutableSearchPath path, PythonExecutableNames names) : PythonCandidateSource
{
    public override bool Applies => true;

    public override IEnumerable<string> Candidates() =>
        names.All.Select(path.Find).OfType<string>();
}

internal sealed class WindowsInstallSource(IHostSystem host) : PythonCandidateSource
{
    public override bool Applies => host.IsWindows;

    public override IEnumerable<string> Candidates()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        foreach (var root in new[] { Path.Combine(local, "Programs", "Python"), programFiles })
            foreach (var directory in Directories(root, "Python3*"))
                yield return Path.Combine(directory, "python.exe");

        var storeAlias = Path.Combine(local, "Microsoft", "WindowsApps", "python3.exe");
        if (File.Exists(storeAlias)) yield return storeAlias;
    }
}

internal sealed class MacInstallSource(PythonExecutableNames names, IHostSystem host) : PythonCandidateSource
{
    public override bool Applies => host.IsMacOS;

    public override IEnumerable<string> Candidates()
    {
        foreach (var directory in Directories("/Library/Frameworks/Python.framework/Versions", "*"))
            yield return Path.Combine(directory, "bin", "python3");

        foreach (var system in new[] { "/usr/bin/python3", "/Library/Developer/CommandLineTools/usr/bin/python3" })
            if (File.Exists(system)) yield return system;

        foreach (var prefix in new[] { "/opt/homebrew", "/usr/local" })
            foreach (var directory in Directories(Path.Combine(prefix, "opt"), "python@3*"))
                foreach (var name in names.All)
                    yield return Path.Combine(directory, "bin", name);
    }
}

internal sealed class LinuxInstallSource(IHostSystem host) : PythonCandidateSource
{
    public override bool Applies => !host.IsWindows && !host.IsMacOS;

    public override IEnumerable<string> Candidates() =>
        new[] { "/usr/bin", "/usr/local/bin", "/opt/python/bin" }
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "python3.*"))
            .Where(file => !Path.GetFileName(file).Contains('-'));
}

internal sealed record ProbeResult(string? Output, string Error);

internal sealed class PythonInterpreterProbe
{
    private const string Script = """
        import sys, sysconfig, platform, json
        print(json.dumps({
          "version": "%d.%d.%d" % sys.version_info[:3],
          "prefix": sys.base_prefix,
          "libdir": sysconfig.get_config_var("LIBDIR") or "",
          "ldlibrary": sysconfig.get_config_var("LDLIBRARY") or "",
          "instsoname": sysconfig.get_config_var("INSTSONAME") or "",
          "machine": platform.machine(),
          "freethreaded": bool(sysconfig.get_config_var("Py_GIL_DISABLED")),
          "venv": sys.prefix if sys.prefix != sys.base_prefix else ""
        }))
        """;

    public ProbeResult Run(string executablePath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executablePath, ["-c", Script])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null) return new ProbeResult(null, "The interpreter could not be started");

            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                process.Kill(entireProcessTree: true);
                return new ProbeResult(null, "The interpreter did not answer within 10 seconds");
            }

            Task.WaitAll([output, errors], TimeSpan.FromSeconds(2));
            return process.ExitCode == 0 && output.IsCompletedSuccessfully
                ? new ProbeResult(output.Result, string.Empty)
                : new ProbeResult(null, $"The interpreter exited with code {process.ExitCode}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new ProbeResult(null, $"The probe failed: {ex.Message}");
        }
    }
}

internal sealed class PythonDescriptorReader(PythonVersionRange range, LibraryLocator locator)
{
    private readonly IReadOnlyDictionary<string, Architecture> _machines = new Dictionary<string, Architecture>
    {
        ["x86_64"] = Architecture.X64,
        ["amd64"] = Architecture.X64,
        ["arm64"] = Architecture.Arm64,
        ["aarch64"] = Architecture.Arm64,
        ["i386"] = Architecture.X86,
        ["i686"] = Architecture.X86,
        ["x86"] = Architecture.X86,
        ["armv7l"] = Architecture.Arm,
    };

    public PythonDiscoveryResult Read(string executablePath, string json)
    {
        var line = json.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault(text => text.StartsWith('{'));
        if (line is null) return new PythonDiscoveryResult(null, "The interpreter gave no description");
        try
        {
            return Describe(executablePath, line);
        }
        catch (Exception failure) when (failure is System.Text.Json.JsonException or InvalidOperationException)
        {
            return new PythonDiscoveryResult(null, $"Unreadable interpreter description: {failure.Message}");
        }
    }

    private PythonDiscoveryResult Describe(string executablePath, string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        string Text(string name) => root.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

        if (!Version.TryParse(Text("version"), out var version))
            return new PythonDiscoveryResult(null, "Unreadable version");

        if (!range.IsSupported(version))
            return new PythonDiscoveryResult(null,
                $"Version {version} outside the supported range {PythonVersionRange.MinimumVersion}–{PythonVersionRange.MaximumVersion}");

        var home = Text("prefix");
        var library = locator.Find(new LibraryHints(home, version, Text("libdir"), Text("ldlibrary"), Text("instsoname")));
        if (library is null)
            return new PythonDiscoveryResult(null, $"Shared library not found for Python {version} in {home}");

        var venv = Text("venv");

        return new PythonDiscoveryResult(new PythonRuntimeDescriptor
        {
            Version = version,
            Home = home,
            LibraryPath = library,
            ExecutablePath = executablePath,
            Architecture = _machines.GetValueOrDefault(Text("machine").ToLowerInvariant(), RuntimeInformation.ProcessArchitecture),
            Origin = PythonProvisioningMode.SystemInstallation,
            VirtualEnvironmentPath = string.IsNullOrEmpty(venv) ? null : venv,
            IsFreeThreaded = root.TryGetProperty("freethreaded", out var threaded) && threaded.GetBoolean(),
        });
    }
}

internal sealed record LibraryHints(string Home, Version Version, string LibDir, string LdLibrary, string InstSoName)
{
    public string Short => string.Create(CultureInfo.InvariantCulture, $"{Version.Major}.{Version.Minor}");
}

internal abstract class LibraryLocator
{
    public abstract bool Applies { get; }

    public string? Find(LibraryHints hints) =>
        Directories(hints).Where(Directory.Exists)
            .SelectMany(directory => Names(hints).Select(name => Path.Combine(directory, name)))
            .FirstOrDefault(File.Exists);

    protected abstract IEnumerable<string> Names(LibraryHints hints);

    protected virtual IEnumerable<string> Directories(LibraryHints hints)
    {
        if (!string.IsNullOrEmpty(hints.LibDir)) yield return hints.LibDir;
        yield return Path.Combine(hints.Home, "lib");
        yield return hints.Home;
    }

    protected IEnumerable<string> Declared(LibraryHints hints)
    {
        foreach (var raw in new[] { hints.InstSoName, hints.LdLibrary }.Where(raw => !string.IsNullOrEmpty(raw)))
        {
            yield return raw;
            if (Path.GetFileName(raw) is var file && file != raw) yield return file;
        }
    }
}

internal sealed class WindowsLibraryLocator(IHostSystem host) : LibraryLocator
{
    public override bool Applies => host.IsWindows;

    protected override IEnumerable<string> Names(LibraryHints hints) =>
        [$"python{hints.Version.Major}{hints.Version.Minor}.dll"];

    protected override IEnumerable<string> Directories(LibraryHints hints) =>
        [hints.Home, Path.Combine(hints.Home, "DLLs")];
}

internal sealed class MacLibraryLocator(IHostSystem host) : LibraryLocator
{
    public override bool Applies => host.IsMacOS;

    protected override IEnumerable<string> Names(LibraryHints hints) =>
        [.. Declared(hints), $"libpython{hints.Short}.dylib", "Python"];
}

internal sealed class LinuxLibraryLocator : LibraryLocator
{
    public override bool Applies => true;

    protected override IEnumerable<string> Names(LibraryHints hints) =>
        [.. Declared(hints), $"libpython{hints.Short}.so.1.0", $"libpython{hints.Short}.so", $"libpython{hints.Short}m.so.1.0"];

    protected override IEnumerable<string> Directories(LibraryHints hints) =>
        [.. base.Directories(hints), "/usr/lib", "/usr/lib/x86_64-linux-gnu", "/usr/lib/aarch64-linux-gnu"];
}
