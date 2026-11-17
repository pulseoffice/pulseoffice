using System.Runtime.ExceptionServices;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class SmallStackThread
{
    public const int StackBytes = 512 * 1024;

    public const int DeeperThanAnyRecursionFits = 10_000;

    public T Run<T>(Func<T> work)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }, StackBytes);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result;
    }
}
