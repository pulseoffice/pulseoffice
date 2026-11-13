using Avalonia;
using PulseOffice.Diagnostics;
using PulseOffice.Platform;
using Serilog;
using Serilog.Events;

namespace PulseOffice.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var debugBuild = typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
            .OfType<System.Reflection.AssemblyConfigurationAttribute>()
            .Any(attribute => attribute.Configuration == "Debug");
        var loggerFactory = LogConfiguration.Initialize(debugBuild
            ? new LogOptions { MinimumLevel = LogEventLevel.Debug, WriteToConsole = true }
            : new LogOptions());
        LogConfiguration.CaptureUnhandledExceptions();

        try
        {
            Log.Information("Starting PulseOffice");
            if (!DisplayIsReady()) return;
            BuildAvaloniaApp(loggerFactory, new PulseOffice.UI.Controls.Fonts.FontFamilyMappingTable()).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogConfiguration.WriteCrash(ex, "Program.Main", terminating: true);
            throw;
        }
        finally
        {
            Log.Information("Shutting down PulseOffice");
            LogConfiguration.Shutdown();
        }
    }

    private static bool DisplayIsReady()
    {
        IHostSystem host = new RuntimeHostSystem();
        IActiveDisplayProbe probe = host.IsMacOS
            ? new PulseOffice.Platform.MacOS.MacActiveDisplayProbe()
            : new AlwaysActiveDisplayProbe();
        var gate = new DisplayReadinessGate(probe, new SystemDisplayWaitClock());
        if (!probe.HasActiveDisplay()) Log.Warning("No active display (screen off or locked); waiting for one before starting the UI");
        var outcome = gate.WaitForActiveDisplay(DisplayWaitTimeout);
        if (outcome == DisplayReadinessOutcome.ReadyAfterWaiting) Log.Information("Active display available, starting the UI");
        if (outcome != DisplayReadinessOutcome.TimedOut) return true;
        Log.Error("No active display after {Timeout}; PulseOffice cannot start its renderer and exits", DisplayWaitTimeout);
        Environment.ExitCode = 2;
        return false;
    }

    private static readonly TimeSpan DisplayWaitTimeout = TimeSpan.FromMinutes(10);

    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(null, new PulseOffice.UI.Controls.Fonts.FontFamilyMappingTable());

    private static AppBuilder BuildAvaloniaApp(
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory, PulseOffice.UI.Controls.Fonts.FontFamilyMappingTable fontMappings) =>
        AppBuilder.Configure(() => new App { LoggerFactory = loggerFactory, FontMappings = fontMappings })
            .UsePlatformDetect()
            .WithInterFont()
            .With(new Avalonia.Media.FontManagerOptions { FontFamilyMappings = fontMappings })
            .With(new MacOSPlatformOptions { DisableDefaultApplicationMenuItems = true })
            .With(new X11PlatformOptions { EnableDrawnDecorations = true })
            .AfterSetup(builder => new DeveloperTools.ConfiguredDeveloperTools().Attach(builder.Instance!))
            .LogToTrace();
}
