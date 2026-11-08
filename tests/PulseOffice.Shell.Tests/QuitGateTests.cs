using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class QuitGateTests
{
    private sealed class PendingGuard : IUnsavedChangesGuard
    {
        public TaskCompletionSource<bool> Answer { get; } = new();
        public int Asked { get; private set; }
        public bool HasUnsaved => true;

        public Task<bool> ConfirmCloseAsync(IReadOnlyList<OpenDocument> documents, CancellationToken ct = default) =>
            ConfirmCloseAllAsync(ct);

        public Task<bool> ConfirmCloseAllAsync(CancellationToken ct = default)
        {
            Asked++;
            return Answer.Task;
        }
    }

    [Fact]
    public async Task UnaSecondaRichiestaDuranteLaDomandaNonApreUnAltraFinestra()
    {
        var guard = new PendingGuard();
        var gate = new QuitGate(guard);

        var first = gate.ConfirmAsync(TestContext.Current.CancellationToken);
        Assert.True(gate.MustConfirm);
        Assert.False(await gate.ConfirmAsync(TestContext.Current.CancellationToken));

        guard.Answer.SetResult(true);
        Assert.True(await first);
        Assert.Equal(1, guard.Asked);
        Assert.False(gate.MustConfirm);
    }
}
