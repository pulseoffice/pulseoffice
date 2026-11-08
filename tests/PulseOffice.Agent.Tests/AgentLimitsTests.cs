using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent.Protocol;
using PulseOffice.Agent.Security;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class AgentLimitsTests
{
    private const string Initialize = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"clientInfo":{"name":"x"}}}""";
    private const string Block = """{"jsonrpc":"2.0","id":2,"method":"block"}""";

    private readonly ManualTime _time = new();
    private readonly BlockingMethod _block = new();
    private readonly AgentSessions _sessions;
    private readonly McpEndpoint _endpoint;

    public AgentLimitsTests()
    {
        var limits = new AgentLimits(MaximumSessions: 2, MaximumRequestsPerSession: 1, MaximumRequests: 2, IdleExpiry: TimeSpan.FromMinutes(10));
        _sessions = new AgentSessions(_time, limits);
        _endpoint = new McpEndpoint(new JsonRpcCodec(), [new PingMethod(), _block], _sessions, new McpProtocolVersions(),
            new AgentRequestGate(limits), NullLogger<McpEndpoint>.Instance);
    }

    private static int Code(McpReply reply) => JsonNode.Parse(reply.Body!)!["error"]!["code"]!.GetValue<int>();

    [Fact]
    public async Task Sessions_beyond_the_limit_are_refused_until_one_expires()
    {
        Assert.NotNull((await _endpoint.HandleAsync(Initialize, null, CancellationToken.None)).SessionId);
        Assert.NotNull((await _endpoint.HandleAsync(Initialize, null, CancellationToken.None)).SessionId);

        var refused = await _endpoint.HandleAsync(Initialize, null, CancellationToken.None);
        Assert.Equal(503, refused.Status);
        Assert.Null(refused.SessionId);
        Assert.Equal(JsonRpcCodes.TooManySessions, Code(refused));

        _time.Advance(TimeSpan.FromMinutes(11));
        Assert.NotNull((await _endpoint.HandleAsync(Initialize, null, CancellationToken.None)).SessionId);
        Assert.Single(_sessions.Active);
    }

    [Fact]
    public async Task A_session_cannot_run_more_requests_at_once_than_allowed()
    {
        var session = _sessions.TryCreate("x", "v")!.Id;
        var running = _endpoint.HandleAsync(Block, session, CancellationToken.None);

        var refused = await _endpoint.HandleAsync(Block, session, CancellationToken.None);
        Assert.Equal(429, refused.Status);
        Assert.Equal(JsonRpcCodes.TooManyRequests, Code(refused));

        _block.Release();
        Assert.Equal(200, (await running).Status);
        Assert.Equal(200, (await _endpoint.HandleAsync("""{"jsonrpc":"2.0","id":3,"method":"ping"}""", session, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task All_sessions_together_cannot_exceed_the_overall_limit()
    {
        var first = _sessions.TryCreate("a", "v")!.Id;
        var second = _sessions.TryCreate("b", "v")!.Id;
        var running = new[] { _endpoint.HandleAsync(Block, first, CancellationToken.None), _endpoint.HandleAsync(Block, second, CancellationToken.None) };
        _sessions.End(second);
        var third = _sessions.TryCreate("c", "v")!.Id;

        var refused = await _endpoint.HandleAsync(Block, third, CancellationToken.None);
        Assert.Equal(JsonRpcCodes.TooManyRequests, Code(refused));

        _block.Release();
        await Task.WhenAll(running);
    }

    [Fact]
    public async Task A_failing_request_gives_its_place_back()
    {
        var session = _sessions.TryCreate("x", "v")!.Id;
        _block.Fail();

        Assert.Equal(JsonRpcCodes.InternalError, Code(await _endpoint.HandleAsync(Block, session, CancellationToken.None)));
        Assert.Equal(JsonRpcCodes.InternalError, Code(await _endpoint.HandleAsync(Block, session, CancellationToken.None)));
    }

    private sealed class BlockingMethod : IMcpMethod
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _fails;

        public string Name => "block";

        public void Release() => _gate.TrySetResult();

        public void Fail() => _fails = true;

        public async Task<JsonNode> InvokeAsync(McpCall call, CancellationToken ct)
        {
            if (_fails) throw new InvalidOperationException("failed");
            await _gate.Task.ConfigureAwait(false);
            return new JsonObject();
        }
    }
}
