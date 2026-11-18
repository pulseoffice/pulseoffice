using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor.Facades;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round192HostCallCancellationTests
{
    private sealed class CancellingDispatcher(CancellationTokenSource cap) : IScriptDispatcher
    {
        public T Read<T>(Func<T> read)
        {
            cap.Cancel();
            return read();
        }

        public void Change(Action change)
        {
            cap.Cancel();
            change();
        }

        public void Show(Action show)
        {
            cap.Cancel();
            show();
        }

        public T Write<T>(Func<T> write)
        {
            cap.Cancel();
            return write();
        }
    }

    [Fact]
    public void A_host_call_is_refused_once_the_script_has_passed_its_time_cap()
    {
        var run = new ScriptExecutionPermissions();
        using var cap = new CancellationTokenSource();
        var dispatcher = new CancellableScriptDispatcher(new CancellingDispatcher(cap), run);
        var calls = 0;

        Assert.Equal(1, dispatcher.Read(() => ++calls));

        using var queued = new CancellationTokenSource();
        var cancelledWhileQueued = new CancellableScriptDispatcher(new CancellingDispatcher(queued), run);
        using (run.Enter(ScriptPermissions.Sandboxed, queued.Token))
        {
            Assert.Throws<OperationCanceledException>(() => cancelledWhileQueued.Write(() => ++calls));
            Assert.Throws<OperationCanceledException>(() => cancelledWhileQueued.Change(() => calls++));
        }

        Assert.Equal(1, calls);
        Assert.Equal(CancellationToken.None, run.Cancellation);
    }
}
