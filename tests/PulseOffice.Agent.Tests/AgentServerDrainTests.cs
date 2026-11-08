using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent.Protocol;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Transport;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class AgentServerDrainTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulse-agent-drain-" + Guid.NewGuid().ToString("N"));
    private readonly HttpClient _http = new();

    public void Dispose()
    {
        _http.Dispose();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Stopping_waits_for_a_request_already_being_served_and_its_client_gets_the_answer()
    {
        var ct = TestContext.Current.CancellationToken;
        var endpoint = new HeldEndpoint();
        var (server, code) = Server(endpoint, TimeProvider.System);
        var request = SendAsync(server, code, ct);
        await endpoint.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        var stopping = server.StopAsync();
        await Task.Delay(200, ct);
        Assert.False(stopping.IsCompleted);

        endpoint.Release.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), ct);
        var response = await request.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"done\":true}", await response.Content.ReadAsStringAsync(ct));
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task Stopping_gives_up_on_a_stuck_request_once_the_drain_timeout_has_passed()
    {
        var ct = TestContext.Current.CancellationToken;
        var endpoint = new HeldEndpoint();
        var (server, code) = Server(endpoint, new ElapsedTimeProvider());
        _ = SendAsync(server, code, ct);
        await endpoint.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.False(server.IsRunning);
        Assert.False(endpoint.Release.Task.IsCompleted);
        Assert.True(endpoint.Token.IsCancellationRequested);
    }

    private (AgentHttpServer Server, string Code) Server(IMcpEndpoint endpoint, TimeProvider time)
    {
        var code = new FileAgentAccessCode(Path.Combine(_folder, "code"), new FakePrivateFiles());
        var server = new AgentHttpServer(endpoint, code, new AgentSessions(TimeProvider.System, new AgentLimits()),
            NullLogger<AgentHttpServer>.Instance, time);
        server.Start(FreePort());
        Assert.True(server.IsRunning);
        return (server, code.Current);
    }

    private Task<HttpResponseMessage> SendAsync(AgentHttpServer server, string code, CancellationToken ct)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, server.Address)
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}", Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", code);
        return _http.SendAsync(message, ct);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class HeldEndpoint : IMcpEndpoint
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken Token { get; private set; }

        public async Task<McpReply> HandleAsync(string body, string? sessionId, CancellationToken ct)
        {
            Token = ct;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return new McpReply(200, "{\"done\":true}", null);
        }
    }

    private sealed class ElapsedTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            base.CreateTimer(callback, state, dueTime == Timeout.InfiniteTimeSpan ? dueTime : TimeSpan.FromMilliseconds(50), period);
    }
}
