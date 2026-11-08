using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PulseOffice.Testing;

public sealed partial class TestProcessWatchdog : IDisposable
{
    private readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private readonly TimeSpan ProcessLifetimeLimit = TimeSpan.FromMinutes(
        typeof(TestProcessWatchdog).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "PulseOffice.TestProcessLifetimeMinutes") is { Value: { } minutes }
            ? double.Parse(minutes, System.Globalization.CultureInfo.InvariantCulture)
            : 20);

    private readonly CancellationTokenSource stop = new();
    private readonly Stopwatch lifetime = Stopwatch.StartNew();
    private readonly int originalParent;

    public TestProcessWatchdog()
    {
        originalParent = CanObserveParent ? ParentProcessId() : 0;
        new Thread(Watch) { IsBackground = true, Name = "Test process watchdog" }.Start();
    }

    private static bool CanObserveParent => OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    private void Watch()
    {
        while (!stop.Token.WaitHandle.WaitOne(PollInterval))
        {
            if (CanObserveParent && ParentProcessId() != originalParent)
                Terminate($"its runner (process {originalParent}) is gone");
            if (lifetime.Elapsed > ProcessLifetimeLimit)
                Terminate($"it has been running for more than {ProcessLifetimeLimit.TotalMinutes} minutes");
        }
    }

    private static void Terminate(string reason)
    {
        Console.Error.WriteLine($"Test process {Environment.ProcessId} terminated by the watchdog: {reason}");
        Console.Error.Flush();
        Process.GetCurrentProcess().Kill();
    }

    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();
    }

    [LibraryImport("libc", EntryPoint = "getppid")]
    private static partial int ParentProcessId();
}
