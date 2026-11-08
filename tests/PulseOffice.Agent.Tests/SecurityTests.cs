using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Tools;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class SecurityTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulse-agent-" + Guid.NewGuid().ToString("N"));
    private readonly PulseOffice.Platform.IPrivateFiles _files = new PulseOffice.Platform.PrivateFilesFactory().Create();

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Un_file_del_codice_leggibile_da_altri_torna_privato_alla_lettura()
    {
        if (OperatingSystem.IsWindows()) return;
        var path = Path.Combine(_folder, "loose", "access-code");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "abcdefghijklmnopqrstuvwxyz0123456789");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        _ = new FileAgentAccessCode(path, _files).Current;

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void Il_codice_d_accesso_si_conserva_in_un_file_privato_e_si_rigenera()
    {
        var path = Path.Combine(_folder, "agent", "access-code");
        var code = new FileAgentAccessCode(path, _files);
        var first = code.Current;

        Assert.True(first.Length >= 30);
        Assert.Equal(first, new FileAgentAccessCode(path, _files).Current);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));

        Assert.True(code.Matches(first));
        Assert.False(code.Matches(first + "x"));
        Assert.False(code.Matches(null));

        var second = code.Regenerate();
        Assert.NotEqual(first, second);
        Assert.False(code.Matches(first));
        Assert.Equal(second, new FileAgentAccessCode(path, _files).Current);
    }

    [Fact]
    public void Il_registro_delle_operazioni_tiene_le_ultime_e_le_scrive_su_file()
    {
        var path = Path.Combine(_folder, "operations.jsonl");
        var log = new AgentOperationLog(path, _files, NullLogger<AgentOperationLog>.Instance);
        for (var i = 0; i < 60; i++)
            log.Record(new AgentOperation(DateTimeOffset.Now, "c", $"tool{i}", AgentOperationOutcome.Done, null));

        Assert.Equal(50, log.Recent.Count);
        Assert.Equal("tool59", log.Recent[0].Tool);
        Assert.Equal(60, File.ReadAllLines(path).Length);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void Il_registro_delle_operazioni_ruota_e_non_cresce_senza_limite()
    {
        var path = Path.Combine(_folder, "operations.jsonl");
        var log = new AgentOperationLog(path, _files, NullLogger<AgentOperationLog>.Instance);
        var detail = new string('x', 100_000);
        for (var i = 0; i < 1200; i++)
            log.Record(new AgentOperation(DateTimeOffset.Now, "c", $"tool{i}", AgentOperationOutcome.Done, detail));

        var total = new FileInfo(path).Length + (File.Exists(path + ".1") ? new FileInfo(path + ".1").Length : 0);
        Assert.True(total <= 2 * AgentOperationLog.MaximumFileBytes, $"{total} bytes kept");
        Assert.Contains("tool1199", File.ReadAllText(path), StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path + ".1"));
    }

    [Theory]
    [InlineData("shell.tools.options", true)]
    [InlineData("ledger.macro.editor", true)]
    [InlineData("shell.file.password", true)]
    [InlineData("ledger.format.bold", false)]
    public void Alcuni_comandi_non_sono_mai_dell_agente(string id, bool forbidden)
    {
        Assert.Equal(forbidden, new AgentCommandPolicy().IsForbidden(id));
    }

    [Fact]
    public void I_comandi_dei_file_chiedono_il_livello_file_e_quelli_distruttivi_la_conferma()
    {
        var policy = new AgentCommandPolicy();
        var save = new AgentCommandInfo("shell.file.save", "Save", null, "File", "file", ModuleKind.Shell, true);
        var bold = new AgentCommandInfo("folio.format.bold", "Bold", null, "Format", "format", ModuleKind.Documents, true);
        var close = new AgentCommandInfo("shell.window.close", "Close", null, "Window", "window", ModuleKind.Shell, true);

        Assert.Equal(AgentAccessLevel.Files, policy.AccessOf(save));
        Assert.Equal(AgentAccessLevel.Edit, policy.AccessOf(bold));
        Assert.True(policy.IsIrreversible(close));
        Assert.False(policy.IsIrreversible(bold));
    }

    [Theory]
    [InlineData("deck.slide.insertFromFile")]
    [InlineData("deck.insert.imageLink")]
    [InlineData("deck.image.externalEdit")]
    [InlineData("folio.image.externalEdit")]
    public void Deck_commands_that_read_files_or_launch_an_editor_need_the_files_level(string id)
    {
        var command = new AgentCommandInfo(id, id, null, "Insert", "insert", ModuleKind.Presentations, true);

        Assert.Equal(AgentAccessLevel.Files, new AgentCommandPolicy().AccessOf(command));
    }

    [Fact]
    public void Proteggere_esce_dallo_storico_e_quindi_chiede_conferma()
    {
        var policy = new AgentCommandPolicy();
        var protect = new AgentCommandInfo("ledger.worksheet.protect", "Protect Sheet", null, "Sheet", "sheet", ModuleKind.Sheets, true, OutsideHistory: true);

        Assert.True(policy.IsIrreversible(protect));
        Assert.Equal(AgentAccessLevel.Edit, policy.AccessOf(protect));
    }

    [Fact]
    public void Running_the_document_queries_needs_the_files_level_and_a_confirmation()
    {
        var policy = new AgentCommandPolicy();
        var refresh = new AgentCommandInfo("ledger.data.pivotRefresh", "Refresh Pivot", null, "Data", "data", ModuleKind.Sheets, true, RunsDocumentQueries: true);
        var bold = new AgentCommandInfo("folio.format.bold", "Bold", null, "Format", "format", ModuleKind.Documents, true);

        Assert.Equal(AgentAccessLevel.Files, policy.AccessOf(refresh));
        Assert.Equal(AgentConfirmationKind.DocumentQueries, policy.ConfirmationOf(refresh));
        Assert.Null(policy.ConfirmationOf(bold));
    }

    [Theory]
    [InlineData("ledger.data.refresh")]
    [InlineData("ledger.data.providerRefresh")]
    [InlineData("ledger.data.provider")]
    [InlineData("ledger.data.streams")]
    [InlineData("ledger.data.externalLinks")]
    [InlineData("ledger.data.xmlSource")]
    [InlineData("folio.tools.updateLinks")]
    public void I_comandi_che_leggono_database_rete_o_file_esterni_chiedono_il_livello_file(string id)
    {
        var command = new AgentCommandInfo(id, id, null, "Data", "data", ModuleKind.Sheets, true);
        Assert.Equal(AgentAccessLevel.Files, new AgentCommandPolicy().AccessOf(command));
    }

    [Fact]
    public void Le_istruzioni_per_claude_code_portano_indirizzo_e_codice()
    {
        var command = new AgentClientInstructions().ClaudeCode(new Uri("http://127.0.0.1:47820/mcp"), "abc");
        Assert.Equal("claude mcp add --transport http pulseoffice http://127.0.0.1:47820/mcp --header \"Authorization: Bearer abc\"", command);
    }
}
