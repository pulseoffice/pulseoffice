using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class QuitCoordinatorTests
{
    private sealed class ThrowingGuard : IUnsavedChangesGuard
    {
        public bool HasUnsaved => throw new InvalidOperationException("pending edit refused");

        public Task<bool> ConfirmCloseAsync(IReadOnlyList<OpenDocument> documents, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> ConfirmCloseAllAsync(CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class CleanGuard : IUnsavedChangesGuard
    {
        public bool HasUnsaved => false;

        public Task<bool> ConfirmCloseAsync(IReadOnlyList<OpenDocument> documents, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<bool> ConfirmCloseAllAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class CountingTarget : IQuitTarget
    {
        public int Quits { get; private set; }

        public void Quit() => Quits++;
    }

    [Fact]
    public void Un_errore_nel_controllo_delle_modifiche_ferma_l_uscita_invece_di_lasciarla_passare()
    {
        var quit = new QuitCoordinator(new ThrowingGuard(), NullLogger<QuitCoordinator>.Instance);
        var target = new CountingTarget();
        var request = new CancelEventArgs();

        Assert.True(quit.Intercept(request, target));
        Assert.True(request.Cancel);
        Assert.Equal(0, target.Quits);
    }

    [Fact]
    public void Senza_modifiche_l_uscita_passa_senza_domande()
    {
        var quit = new QuitCoordinator(new CleanGuard(), NullLogger<QuitCoordinator>.Instance);
        var request = new CancelEventArgs();

        Assert.False(quit.Intercept(request, new CountingTarget()));
        Assert.False(request.Cancel);
    }
}
