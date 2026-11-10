using System.Net;
using System.Text.Json.Nodes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Tools.McpClient;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class Round139AgentTests
{
    [Fact]
    public async Task Export_refuses_a_file_that_appeared_after_the_check()
    {
        var harness = new Harness();
        var document = harness.Workspace.Add(ModuleKind.Documents, "Letter");
        harness.Files.AppearingAfterFirstCheck.Add("/tmp/late.pdf");

        var result = await harness.CallAsync("export_document", new JsonObject { ["document"] = document.Id, ["path"] = "/tmp/late.pdf" });

        Assert.True(result.IsError);
        Assert.Empty(harness.Workspace.Exported);
        Assert.Empty(harness.Confirmation.Asked);
    }

    [Fact]
    public async Task Save_as_refuses_a_file_that_appeared_after_the_check()
    {
        var harness = new Harness();
        var document = harness.Workspace.Add(ModuleKind.Documents, "Letter");
        harness.Files.AppearingAfterFirstCheck.Add("/tmp/late.odt");

        var result = await harness.CallAsync("save_document_as", new JsonObject { ["document"] = document.Id, ["path"] = "/tmp/late.odt" });

        Assert.True(result.IsError);
        Assert.Null(harness.Workspace.Documents[0].Path);
    }

    [Fact]
    public async Task Bridge_answers_a_refused_request_instead_of_stopping()
    {
        var server = new ScriptedServer(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var output = await RunBridgeAsync(server, """{"jsonrpc":"2.0","id":7,"method":"tools/list"}""", """{"jsonrpc":"2.0","id":8,"method":"ping"}""");

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal(7, JsonNode.Parse(lines[0])!["id"]!.GetValue<int>());
        Assert.Contains("401", JsonNode.Parse(lines[0])!["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bridge_answers_a_malformed_line_with_a_parse_error()
    {
        var server = new ScriptedServer(_ => Json("""{"jsonrpc":"2.0","id":2,"result":{}}"""));
        var output = await RunBridgeAsync(server, "{not json", """{"jsonrpc":"2.0","id":2,"method":"ping"}""");

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(-32700, JsonNode.Parse(lines[0])!["error"]!["code"]!.GetValue<int>());
        Assert.Equal(2, JsonNode.Parse(lines[1])!["id"]!.GetValue<int>());
    }

    [Fact]
    public async Task Bridge_opens_a_new_session_when_the_old_one_expired()
    {
        var sessions = 0;
        var server = new ScriptedServer(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var session = request.Headers.TryGetValues("Mcp-Session-Id", out var values) ? values.First() : null;
            if (body.Contains("\"initialize\"", StringComparison.Ordinal))
            {
                var reply = Json("""{"jsonrpc":"2.0","id":1,"result":{}}""");
                reply.Headers.Add("Mcp-Session-Id", $"s{++sessions}");
                return reply;
            }

            if (body.Contains("notifications/", StringComparison.Ordinal)) return new HttpResponseMessage(HttpStatusCode.Accepted);
            return session == "s1"
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"jsonrpc":"2.0","id":3,"error":{"code":-32600,"message":"Unknown or expired session"}}""") }
                : Json("""{"jsonrpc":"2.0","id":3,"result":{"tools":[]}}""");
        });

        var output = await RunBridgeAsync(server,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","id":3,"method":"tools/list"}""");

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, sessions);
        Assert.NotNull(JsonNode.Parse(lines[1])!["result"]);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static async Task<string> RunBridgeAsync(ScriptedServer server, params string[] lines)
    {
        using var http = new HttpClient(server);
        var client = new McpHttpClient(http, new Uri("http://127.0.0.1:1/mcp"), "code");
        var output = new StringWriter();
        await new StdioBridge(client, new StringReader(string.Join("\n", lines)), output, TextWriter.Null).RunAsync(CancellationToken.None);
        return output.ToString();
    }

    private sealed class ScriptedServer(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Method == HttpMethod.Delete ? new HttpResponseMessage(HttpStatusCode.OK) : reply(request));
    }
}
