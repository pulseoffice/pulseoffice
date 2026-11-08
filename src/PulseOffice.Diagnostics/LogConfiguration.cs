using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace PulseOffice.Diagnostics;

public sealed record LogOptions
{
    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Information;

    public long FileSizeLimitBytes { get; init; } = 32L * 1024 * 1024;

    public int RetainedFileCount { get; init; } = 14;

    public TimeSpan? RetainedFileTimeLimit { get; init; } = TimeSpan.FromDays(30);

    public bool Asynchronous { get; init; } = true;

    public bool WriteToConsole { get; init; }

    public string? Directory { get; init; }
}

public static class LogConfiguration
{
    private const string Template =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    public static ILoggerFactory Initialize(LogOptions? options = null)
    {
        options ??= new LogOptions();

        var directory = options.Directory ?? LogPaths.LogDirectory;
        System.IO.Directory.CreateDirectory(directory);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(options.MinimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Version", typeof(LogConfiguration).Assembly.GetName().Version?.ToString() ?? "0.0.0")
            .Enrich.WithProperty("Os", System.Runtime.InteropServices.RuntimeInformation.OSDescription);

        void ConfigureFile(Serilog.Configuration.LoggerSinkConfiguration sink) =>
            sink.File(
                path: Path.Combine(directory, "pulseoffice-.log"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                retainedFileCountLimit: options.RetainedFileCount,
                retainedFileTimeLimit: options.RetainedFileTimeLimit,
                outputTemplate: Template,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(2));

        if (options.Asynchronous)
            configuration.WriteTo.Async(ConfigureFile);
        else
            ConfigureFile(configuration.WriteTo);

        if (options.WriteToConsole)
            configuration.WriteTo.Console(outputTemplate: Template);

        Log.Logger = configuration.CreateLogger();

        Log.Information("Logging started in {Directory}", directory);
        return new LoggerFactory().AddSerilog(Log.Logger, dispose: true);
    }

    public static void CaptureUnhandledExceptions()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                WriteCrash(ex, "AppDomain.UnhandledException", terminating: e.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrash(e.Exception, "TaskScheduler.UnobservedTaskException", terminating: false);
            e.SetObserved();
        };
    }

    public static void WriteCrash(Exception exception, string origin, bool terminating)
    {
        Log.Fatal(exception, "Unhandled exception from {Origin} (terminating: {Terminating})", origin, terminating);

        try
        {
            System.IO.Directory.CreateDirectory(LogPaths.CrashDirectory);
            var file = Path.Combine(LogPaths.CrashDirectory,
                $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.txt");

            File.WriteAllText(file, $"""
                PulseOffice — crash report
                Time:     {DateTimeOffset.Now:O}
                Origin:   {origin}
                System:   {System.Runtime.InteropServices.RuntimeInformation.OSDescription}
                Runtime:  {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}

                {exception}
                """);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not write the crash report");
        }
    }

    public static void Shutdown() => Log.CloseAndFlush();

    public static IReadOnlyList<FileInfo> ExistingLogFiles()
    {
        var directory = new DirectoryInfo(LogPaths.LogDirectory);
        if (!directory.Exists) return [];
        return [.. directory.GetFiles("pulseoffice-*.log").OrderByDescending(f => f.LastWriteTimeUtc)];
    }
}
