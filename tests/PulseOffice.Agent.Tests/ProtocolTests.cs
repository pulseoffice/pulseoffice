using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent.Protocol;
using PulseOffice.Agent.Security;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class ProtocolTests
{
    private readonly JsonRpcCodec _codec = new();

    [Fact]
    public void Un_testo_non_json_e_un_errore_di_parsing()
    {
        var parsed = _codec.Parse("{not json");
        Assert.False(parsed.Succeeded);
        Assert.Equal(JsonRpcCodes.ParseError, parsed.ErrorCode);
    }

    [Fact]
    public void A_message_with_a_repeated_member_is_a_parse_error_not_a_crash()
    {
        var parsed = _codec.Parse("""{"jsonrpc":"2.0","id":1,"id":2,"method":"ping"}""");
        Assert.False(parsed.Succeeded);
        Assert.Equal(JsonRpcCodes.ParseError, parsed.ErrorCode);
    }

    [Fact]
    public void Un_lotto_non_e_accettato()
    {
        Assert.Equal(JsonRpcCodes.InvalidRequest, _codec.Parse("[]").ErrorCode);
    }

    [Fact]
    public void Senza_versione_2_0_la_richiesta_non_vale()
    {
        var parsed = _codec.Parse("""{"id":1,"method":"ping"}""");
        Assert.Equal(JsonRpcCodes.InvalidRequest, parsed.ErrorCode);
        Assert.Equal(1, parsed.Id!.GetValue<int>());
    }

    [Fact]
    public void Una_notifica_non_ha_id_e_i_parametri_mancanti_diventano_un_oggetto_vuoto()
    {
        var parsed = _codec.Parse("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        Assert.True(parsed.Request!.IsNotification);
        Assert.Empty(parsed.Request.Params);
    }

    [Fact]
    public void Parametri_che_non_sono_un_oggetto_sono_rifiutati()
    {
        Assert.Equal(JsonRpcCodes.InvalidParams, _codec.Parse("""{"jsonrpc":"2.0","id":"a","method":"x","params":[1]}""").ErrorCode);
    }

    [Fact]
    public void Una_risposta_del_client_si_riconosce_e_non_si_esegue()
    {
        var parsed = _codec.Parse("""{"jsonrpc":"2.0","id":4,"result":{}}""");
        Assert.True(parsed.IsResponse);
        Assert.Null(parsed.Request);
    }

    [Fact]
    public void Risultati_ed_errori_portano_lo_stesso_id()
    {
        var result = JsonNode.Parse(_codec.Result("abc", new JsonObject { ["ok"] = true }))!;
        var error = JsonNode.Parse(_codec.Error(7, -32601, "nope"))!;
        Assert.Equal("abc", result["id"]!.GetValue<string>());
        Assert.Equal(7, error["id"]!.GetValue<int>());
        Assert.Equal(-32601, error["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public void La_versione_del_protocollo_si_negozia()
    {
        var versions = new McpProtocolVersions();
        Assert.Equal("2025-03-26", versions.Negotiate("2025-03-26"));
        Assert.Equal("2025-06-18", versions.Negotiate("1999-01-01"));
    }

    [Fact]
    public async Task Initialize_crea_una_sessione_e_dichiara_gli_strumenti()
    {
        var (endpoint, sessions) = Endpoint();
        var reply = await endpoint.HandleAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","clientInfo":{"name":"prova"}}}""",
            null, CancellationToken.None);

        Assert.Equal(200, reply.Status);
        Assert.NotNull(reply.SessionId);
        var result = JsonNode.Parse(reply.Body!)!["result"]!;
        Assert.Equal("2025-06-18", result["protocolVersion"]!.GetValue<string>());
        Assert.NotNull(result["capabilities"]!["tools"]);
        Assert.Equal("prova", Assert.Single(sessions.Active).Client);
    }

    [Fact]
    public async Task Initialize_con_campi_di_tipo_sbagliato_crea_comunque_la_sessione()
    {
        var (endpoint, sessions) = Endpoint();
        var reply = await endpoint.HandleAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":7,"clientInfo":"nome"}}""",
            null, CancellationToken.None);

        Assert.Equal(200, reply.Status);
        Assert.Equal("client", Assert.Single(sessions.Active).Client);
    }

    [Fact]
    public async Task Un_nome_del_client_lunghissimo_o_con_caratteri_di_controllo_si_accorcia()
    {
        var (endpoint, sessions) = Endpoint();
        var name = "x\u001b[2J" + new string('y', 5000);
        await endpoint.HandleAsync(
            new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize", ["params"] = new JsonObject { ["clientInfo"] = new JsonObject { ["name"] = name } } }.ToJsonString(),
            null, CancellationToken.None);

        var client = Assert.Single(sessions.Active).Client;
        Assert.True(client.Length <= 64);
        Assert.DoesNotContain('\u001b', client);
    }

    [Theory]
    [InlineData("1e20")]
    [InlineData("-1e20")]
    public void Un_intero_fuori_scala_e_un_argomento_sbagliato_non_un_numero_capovolto(string number)
    {
        var arguments = new PulseOffice.Agent.Tools.AgentArguments((JsonObject)JsonNode.Parse($$"""{"width":{{number}}}""")!);
        Assert.Throws<PulseOffice.Agent.Tools.AgentArgumentException>(() => arguments.Integer("width", 0));
    }

    [Fact]
    public async Task Senza_sessione_le_richieste_sono_rifiutate_e_una_sessione_ignota_e_404()
    {
        var (endpoint, _) = Endpoint();
        Assert.Equal(400, (await endpoint.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", null, CancellationToken.None)).Status);
        Assert.Equal(404, (await endpoint.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", "nessuna", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Metodo_ignoto_e_notifiche_dopo_l_inizializzazione()
    {
        var (endpoint, sessions) = Endpoint();
        var session = sessions.TryCreate("x", "2025-06-18")!.Id;

        var unknown = await endpoint.HandleAsync("""{"jsonrpc":"2.0","id":2,"method":"resources/list"}""", session, CancellationToken.None);
        Assert.Equal(JsonRpcCodes.MethodNotFound, JsonNode.Parse(unknown.Body!)!["error"]!["code"]!.GetValue<int>());

        var notification = await endpoint.HandleAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", session, CancellationToken.None);
        Assert.Equal(202, notification.Status);
        Assert.Null(notification.Body);

        var ping = await endpoint.HandleAsync("""{"jsonrpc":"2.0","id":3,"method":"ping"}""", session, CancellationToken.None);
        Assert.NotNull(JsonNode.Parse(ping.Body!)!["result"]);
    }

    [Fact]
    public async Task Le_sessioni_inattive_scadono()
    {
        var time = new ManualTime();
        var sessions = new AgentSessions(time, new AgentLimits());
        var session = sessions.TryCreate("x", "v")!;
        time.Advance(TimeSpan.FromMinutes(11));
        Assert.Null(sessions.Find(session.Id));
        Assert.Empty(sessions.Active);
        await Task.CompletedTask;
    }

    private static (McpEndpoint Endpoint, AgentSessions Sessions) Endpoint()
    {
        var harness = new Harness();
        var sessions = new AgentSessions(TimeProvider.System, new AgentLimits());
        var endpoint = new McpEndpoint(new JsonRpcCodec(),
            [new PingMethod(), new ToolsListMethod(harness.Catalog, harness.Invoker), new ToolsCallMethod(harness.Catalog, harness.Invoker)],
            sessions, new McpProtocolVersions(), new AgentRequestGate(new AgentLimits()), NullLogger<McpEndpoint>.Instance);
        return (endpoint, sessions);
    }
}

internal sealed class ManualTime : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
