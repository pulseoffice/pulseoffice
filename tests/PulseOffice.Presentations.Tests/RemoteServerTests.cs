using System.Net;
using System.Net.Http;
using PulseOffice.Presentations.Remote;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class RemoteServerTests
{
    private sealed class Target : IRemoteTarget
    {
        public int Position { get; private set; }

        public int Count => 3;

        public string Title => $"Diapositiva {Position + 1}";

        public string Notes => "Note";

        public void Next() => Position = Math.Min(2, Position + 1);

        public void Previous() => Position = Math.Max(0, Position - 1);

        public void First() => Position = 0;

        public void Last() => Position = 2;
    }

    private sealed class Page : IRemotePage
    {
        public string Login(RemoteLoginNotice notice, TimeSpan retryAfter) => "login " + notice;

        public string Controller(string pin) => "controller " + pin;
    }

    [Fact]
    public async Task SenzaIlPinNonSiComandaNulla()
    {
        var target = new Target();
        using var server = new RemoteServer(new RemoteSession { Target = target }, action => { action(); return Task.CompletedTask; }, new Page(), new RemoteAttemptThrottle(TimeProvider.System));
        var endpoint = server.Start(IPAddress.Loopback);
        using var http = new HttpClient();

        var refused = await http.PostAsync($"http://127.0.0.1:{endpoint.Port}/go/next?pin=9999{(endpoint.Pin == "9999" ? "0" : "")}", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(0, target.Position);

        var accepted = await http.PostAsync($"http://127.0.0.1:{endpoint.Port}/go/next?pin={endpoint.Pin}", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(1, target.Position);
        Assert.Contains("\"position\":2", await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        await http.PostAsync($"http://127.0.0.1:{endpoint.Port}/go/last?pin={endpoint.Pin}", null, TestContext.Current.CancellationToken);
        Assert.Equal(2, target.Position);
        Assert.Contains("controller", await http.GetStringAsync(endpoint.Url.Replace(endpoint.Address, "127.0.0.1", StringComparison.Ordinal), TestContext.Current.CancellationToken));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task IPinSbagliatiDaUnIndirizzoNonBloccanoIlPinGiustoNeGliAltri()
    {
        var target = new Target();
        using var server = new RemoteServer(new RemoteSession { Target = target }, action => { action(); return Task.CompletedTask; }, new Page(), new RemoteAttemptThrottle(TimeProvider.System));
        var endpoint = server.Start(IPAddress.Loopback);
        using var http = new HttpClient();
        var wrong = endpoint.Pin == "0000" ? "0001" : "0000";

        var first = await http.GetStringAsync($"http://127.0.0.1:{endpoint.Port}/?pin={wrong}", TestContext.Current.CancellationToken);
        Assert.Contains("WrongPin", first);
        await http.GetAsync($"http://127.0.0.1:{endpoint.Port}/?pin={wrong}", TestContext.Current.CancellationToken);
        var throttled = await http.GetAsync($"http://127.0.0.1:{endpoint.Port}/?pin={wrong}", TestContext.Current.CancellationToken);
        await http.GetAsync($"http://127.0.0.1:{endpoint.Port}/?pin={wrong}", TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)429, (await http.GetAsync($"http://127.0.0.1:{endpoint.Port}/?pin={wrong}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.NotNull(throttled);
    }

    [Fact]
    public void IlRitardoCresceSoloPerLIndirizzoCheSbagliaESiAzzeraColPinGiusto()
    {
        var clock = new Clock();
        var throttle = new RemoteAttemptThrottle(clock);
        var attacker = IPAddress.Parse("192.168.1.66");
        var speaker = IPAddress.Parse("192.168.1.10");

        for (var i = 0; i < 50; i++)
        {
            clock.Now += throttle.Wait(attacker);
            throttle.Failed(attacker);
        }

        Assert.True(throttle.Wait(attacker) >= TimeSpan.FromMinutes(1));
        Assert.True(throttle.Wait(attacker) <= TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.Zero, throttle.Wait(speaker));
        Assert.Equal(TimeSpan.Zero, throttle.Wait(IPAddress.Parse("192.168.1.66").MapToIPv6()) - throttle.Wait(attacker));

        throttle.Succeeded(attacker);
        Assert.Equal(TimeSpan.Zero, throttle.Wait(attacker));
    }
}
