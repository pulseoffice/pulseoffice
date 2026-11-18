using System.ComponentModel;
using System.Diagnostics;

namespace PulseOffice.Shell;

public interface IRecoveryOwnership
{
    RecoveryOwner Current { get; }

    bool IsAlive(RecoveryOwner owner);
}

public sealed class ProcessRecoveryOwnership : IRecoveryOwnership
{
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(2);

    public RecoveryOwner Current
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return new RecoveryOwner(process.Id, StartOf(process) ?? DateTimeOffset.MinValue);
        }
    }

    public bool IsAlive(RecoveryOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        try
        {
            using var process = Process.GetProcessById(owner.ProcessId);
            return !process.HasExited
                && StartOf(process) is { } started
                && (started - owner.Started).Duration() <= StartTolerance;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    private DateTimeOffset? StartOf(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}
