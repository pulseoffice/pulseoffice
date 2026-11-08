using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Protocol;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Transport;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class AgentServerHardeningTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulse-agent-hardening-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_undo_step_and_the_confirmation_name_the_member_by_its_title_not_by_the_tool_name()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        await harness.CallAsync("ledger_sheet_Write", new JsonObject { ["reference"] = "A1", ["value"] = 1 });
        await harness.CallAsync("ledger_sheet_SetTracking", new JsonObject { ["on"] = true });

        Assert.Equal("Write title", harness.Workspace.UndoSteps[0]);
        var asked = Assert.Single(harness.Confirmation.Asked);
        Assert.Equal("ledger_sheet_SetTracking", asked.Tool);
        Assert.Equal("SetTracking title", asked.Title);
        Assert.Equal("Write title", harness.Log.Recent[^1].Title);
    }

    [Fact]
    public async Task The_properties_tool_is_named_by_the_localized_title_of_its_object()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        await harness.CallAsync("ledger_sheet_properties");

        Assert.Equal("Read sheet properties", harness.Catalog.Find("ledger_sheet_properties")!.Title);
        Assert.Equal("Read sheet properties", harness.Log.Recent[^1].Title);
    }

    [Fact]
    public async Task The_operation_log_records_the_shape_of_the_arguments_never_the_document_text()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        await harness.CallAsync("ledger_sheet_Write", new JsonObject { ["reference"] = "A1", ["value"] = "Salary of Mario Rossi: 4200" });
        harness.Confirmation.Answer = false;
        await harness.CallAsync("ledger_sheet_SetTracking", new JsonObject { ["on"] = "private note" });

        foreach (var operation in harness.Log.Recent)
        {
            Assert.DoesNotContain("Mario", operation.Detail ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("private", operation.Detail ?? string.Empty, StringComparison.Ordinal);
        }

        Assert.Contains("value", harness.Log.Recent[^1].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_request_whose_id_is_null_is_refused_instead_of_being_taken_for_a_notification()
    {
        var parsed = new JsonRpcCodec().Parse("""{"jsonrpc":"2.0","id":null,"method":"ping"}""");

        Assert.False(parsed.Succeeded);
        Assert.Equal(JsonRpcCodes.InvalidRequest, parsed.ErrorCode);
    }

    [Fact]
    public void A_huge_numeric_id_is_echoed_back_unchanged()
    {
        var codec = new JsonRpcCodec();
        var parsed = codec.Parse("""{"jsonrpc":"2.0","id":123456789012345678901234567890,"method":"ping"}""");

        Assert.True(parsed.Succeeded);
        Assert.Contains("\"id\":123456789012345678901234567890", codec.Result(parsed.Id, new JsonObject()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_port_already_in_use_leaves_the_server_off_with_a_reason_instead_of_throwing()
    {
        using var occupant = new TcpListener(IPAddress.Loopback, 0);
        occupant.Start();
        var port = ((IPEndPoint)occupant.LocalEndpoint).Port;
        var server = Server();

        server.Start(port);

        Assert.False(server.IsRunning);
        Assert.False(string.IsNullOrEmpty(server.Failure));
        await server.DisposeAsync();
    }

    [Fact]
    public async Task The_server_restarts_on_the_same_port_after_a_stop()
    {
        var port = FreePort();
        var server = Server();

        server.Start(port);
        await server.StopAsync();
        server.Start(port);

        Assert.True(server.IsRunning);
        Assert.Equal(port, server.Address!.Port);
        await server.DisposeAsync();
    }

    [Fact]
    public async Task The_controller_reports_when_the_preferences_have_been_applied_and_subscribes_once()
    {
        var preferences = new FakePreferences { Enabled = true, Port = FreePort() };
        var server = new CountingServer();
        var controller = new AgentServerController(preferences, server, NullLogger<AgentServerController>.Instance);

        controller.Start();
        controller.Start();
        await controller.Settled;
        preferences.Port = FreePort();
        preferences.Raise();
        await controller.Settled;

        Assert.Equal(2, server.Starts);
        Assert.Equal(preferences.Port, server.Port);
    }

    [Fact]
    public void Two_codes_of_different_length_are_compared_without_an_early_exit_on_length()
    {
        var code = new FileAgentAccessCode(Path.Combine(_folder, "access-code"), new FakePrivateFiles());
        var current = code.Current;

        Assert.True(code.Matches(current));
        Assert.False(code.Matches(current[..^1]));
        Assert.False(code.Matches(current + current));
    }

    [Fact]
    public void The_access_code_and_the_operation_log_are_written_through_the_private_file_store()
    {
        var files = new FakePrivateFiles();
        _ = new FileAgentAccessCode(Path.Combine(_folder, "agent", "access-code"), files).Current;
        new AgentOperationLog(Path.Combine(_folder, "agent", "operations.jsonl"), files, NullLogger<AgentOperationLog>.Instance)
            .Record(new AgentOperation(DateTimeOffset.Now, "c", "t", AgentOperationOutcome.Done, null));

        Assert.Contains(Path.Combine(_folder, "agent", "access-code"), files.Restricted);
        Assert.Contains(Path.Combine(_folder, "agent", "operations.jsonl"), files.Restricted);
        Assert.Contains(Path.Combine(_folder, "agent"), files.Folders);
    }

    [Theory]
    [InlineData(0, 800, 1200)]
    [InlineData(1, 100000, 1200)]
    [InlineData(100000, 1, 3000)]
    [InlineData(double.NaN, 800, 1200)]
    public void A_degenerate_page_gives_a_bounded_picture(double pageWidth, double pageHeight, int width)
    {
        var picture = new AgentPictureFrame().Fit(pageWidth, pageHeight, width);

        Assert.InRange(picture.Width, 1, width);
        Assert.InRange(picture.Height, 1, AgentPictureFrame.MaximumHeight);
        Assert.True(double.IsFinite(picture.Scale) && picture.Scale > 0);
    }

    [Fact]
    public void An_ordinary_page_keeps_the_requested_width_and_its_proportions()
    {
        var picture = new AgentPictureFrame().Fit(595, 842, 1200);

        Assert.Equal(1200, picture.Width);
        Assert.Equal(1699, picture.Height);
    }

    private AgentHttpServer Server()
    {
        var sessions = new AgentSessions(TimeProvider.System, new AgentLimits());
        var endpoint = new McpEndpoint(new JsonRpcCodec(), [new PingMethod()], sessions, new McpProtocolVersions(),
            new AgentRequestGate(new AgentLimits()), NullLogger<McpEndpoint>.Instance);
        return new AgentHttpServer(endpoint, new FileAgentAccessCode(Path.Combine(_folder, "code"), new FakePrivateFiles()), sessions,
            NullLogger<AgentHttpServer>.Instance, TimeProvider.System);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class CountingServer : IAgentServer
    {
        public int Starts { get; private set; }

        public int Port { get; private set; }

        public bool IsRunning { get; private set; }

        public Uri? Address => null;

        public string? Failure => null;

        public event EventHandler? StateChanged;

        public void Start(int port)
        {
            Starts++;
            Port = port;
            IsRunning = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
