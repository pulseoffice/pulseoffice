using Avalonia.Threading;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class UiThreadSessionTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public async Task A_failure_after_an_await_inside_an_async_body_reaches_the_test()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Session.Dispatch(async () =>
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            throw new InvalidOperationException("after await");
        }, CancellationToken.None));

        Assert.Equal("after await", failure.Message);
    }

    [Fact]
    public async Task The_result_of_an_async_body_is_the_awaited_value()
    {
        var value = await Session.Dispatch(async () =>
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            return 42;
        }, CancellationToken.None);

        Assert.Equal(42, value);
    }
}
