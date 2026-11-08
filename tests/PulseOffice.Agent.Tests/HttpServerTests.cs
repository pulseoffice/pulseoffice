using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent.Protocol;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Transport;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class HttpServerTests : IAsyncDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulse-agent-http-" + Guid.NewGuid().ToString("N"));
    private readonly Harness _harness = new();
    private readonly AgentSessions _sessions = new(TimeProvider.System, new AgentLimits());
    private readonly FileAgentAccessCode _code;
    private readonly AgentHttpServer _server;
    private readonly HttpClient _http = new();

    public HttpServerTests()
    {
        _code = new FileAgentAccessCode(Path.Combine(_folder, "access-code"), new PulseOffice.Platform.PrivateFilesFactory().Create());
        var endpoint = new McpEndpoint(new JsonRpcCodec(),
            [new PingMethod(), new ToolsListMethod(_harness.Catalog, _harness.Invoker), new ToolsCallMethod(_harness.Catalog, _harness.Invoker)],
            _sessions, new McpProtocolVersions(), new AgentRequestGate(new AgentLimits()), NullLogger<McpEndpoint>.Instance);
        _server = new AgentHttpServer(endpoint, _code, _sessions, NullLogger<AgentHttpServer>.Instance, TimeProvider.System);
        _server.Start(FreePort());
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _server.DisposeAsync();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Un_client_si_collega_elenca_gli_strumenti_e_li_chiama()
    {
        Assert.True(_server.IsRunning);
        Assert.Equal("127.0.0.1", _server.Address!.Host);
        _harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        var (initialize, session) = await PostAsync(Request(1, "initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "test-client", ["version"] = "1" },
        }), null);
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("test-client", Assert.Single(_sessions.Active).Client);

        var (initialized, _) = await PostAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", session);
        Assert.Equal(HttpStatusCode.Accepted, initialized.StatusCode);

        var (listed, _) = await PostAsync(Request(2, "tools/list", new JsonObject()), session);
        var tools = (await JsonAsync(listed))["result"]!["tools"]!.AsArray();
        Assert.Contains(tools, tool => tool!["name"]!.GetValue<string>() == "ledger_sheet_Write");
        Assert.All(tools, tool => Assert.Equal("object", tool!["inputSchema"]!["type"]!.GetValue<string>()));

        var (called, _) = await PostAsync(Request(3, "tools/call", new JsonObject
        {
            ["name"] = "ledger_sheet_Write",
            ["arguments"] = new JsonObject { ["reference"] = "C3", ["value"] = 42 },
        }), session);
        Assert.False((await JsonAsync(called))["result"]!["isError"]!.GetValue<bool>());
        Assert.Equal("42", _harness.Sheet.Read("C3"));

        var (picture, _) = await PostAsync(Request(4, "tools/call", new JsonObject { ["name"] = "render_page", ["arguments"] = new JsonObject() }), session);
        var content = (await JsonAsync(picture))["result"]!["content"]!.AsArray();
        Assert.Equal("image", content[0]!["type"]!.GetValue<string>());
        Assert.Equal(_harness.Workspace.Picture, Convert.FromBase64String(content[0]!["data"]!.GetValue<string>()));

        var (unknown, _) = await PostAsync(Request(5, "tools/call", new JsonObject { ["name"] = "nessuno" }), session);
        Assert.Equal(JsonRpcCodes.InvalidParams, (await JsonAsync(unknown))["error"]!["code"]!.GetValue<int>());

        using var end = new HttpRequestMessage(HttpMethod.Delete, _server.Address) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _code.Current) } };
        end.Headers.Add("Mcp-Session-Id", session);
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(end, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Empty(_sessions.Active);
    }

    [Fact]
    public async Task Senza_codice_o_con_un_codice_sbagliato_il_server_risponde_401()
    {
        using var anonymous = new HttpRequestMessage(HttpMethod.Post, _server.Address) { Content = Json(Request(1, "ping", new JsonObject())) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(anonymous, TestContext.Current.CancellationToken)).StatusCode);

        using var wrong = new HttpRequestMessage(HttpMethod.Post, _server.Address) { Content = Json(Request(1, "ping", new JsonObject())) };
        wrong.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sbagliato");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(wrong, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Una_pagina_web_di_un_altro_sito_non_puo_parlare_al_server()
    {
        using var foreign = new HttpRequestMessage(HttpMethod.Post, _server.Address) { Content = Json(Request(1, "ping", new JsonObject())) };
        foreign.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _code.Current);
        foreign.Headers.Add("Origin", "https://example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(foreign, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Una_richiesta_con_un_host_estraneo_e_rifiutata_contro_il_dns_rebinding()
    {
        using var socket = new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback, _server.Address!.Port, TestContext.Current.CancellationToken);
        var stream = socket.GetStream();
        var body = Request(1, "ping", new JsonObject());
        var raw = $"POST /mcp HTTP/1.1\r\nHost: attacker.example:{_server.Address.Port}\r\nAuthorization: Bearer {_code.Current}\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(raw), TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        var status = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(" 200 ", status);
    }

    [Fact]
    public async Task Un_corpo_a_blocchi_oltre_il_tetto_e_rifiutato_senza_leggerlo_tutto()
    {
        var oversized = new byte[4 * 1024 * 1024 + 1024];
        Array.Fill(oversized, (byte)' ');
        using var content = new StreamContent(new UnseekableStream(oversized));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, _server.Address) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _code.Current);
        request.Headers.TransferEncodingChunked = true;
        using var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private sealed class UnseekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }

    [Fact]
    public async Task Get_non_e_supportato_e_dopo_lo_stop_il_server_non_risponde()
    {
        using var get = new HttpRequestMessage(HttpMethod.Get, _server.Address);
        get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _code.Current);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _http.SendAsync(get, TestContext.Current.CancellationToken)).StatusCode);

        var address = _server.Address!;
        await _server.StopAsync();
        Assert.False(_server.IsRunning);
        await Assert.ThrowsAsync<HttpRequestException>(() => _http.PostAsync(address, Json("{}"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Il_controllore_accende_e_spegne_il_server_secondo_le_impostazioni()
    {
        await _server.StopAsync();
        var preferences = new FakePreferences { Enabled = false, Port = FreePort() };
        var controller = new AgentServerController(preferences, _server, NullLogger<AgentServerController>.Instance);
        controller.Start();
        await controller.Settled;
        Assert.False(_server.IsRunning);

        preferences.Enabled = true;
        preferences.Raise();
        await controller.Settled;
        Assert.True(_server.IsRunning);
        Assert.Equal(preferences.Port, _server.Address!.Port);

        preferences.Enabled = false;
        preferences.Raise();
        await controller.Settled;
        Assert.False(_server.IsRunning);
    }

    private async Task<(HttpResponseMessage Response, string? Session)> PostAsync(string body, string? session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _server.Address) { Content = Json(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _code.Current);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (session is not null) request.Headers.Add("Mcp-Session-Id", session);
        var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);
        return (response, response.Headers.TryGetValues("Mcp-Session-Id", out var values) ? values.First() : session);
    }

    private static async Task<JsonNode> JsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string Request(int id, string method, JsonObject parameters) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters }.ToJsonString();

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
