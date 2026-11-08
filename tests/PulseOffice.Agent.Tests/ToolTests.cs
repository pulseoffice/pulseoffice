using System.Text.Json.Nodes;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Tools;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class ToolTests
{
    [Fact]
    public void Gli_strumenti_di_contenuto_nascono_dal_catalogo_delle_macro()
    {
        var names = new Harness().Catalog.All.Select(tool => tool.Name).ToList();

        Assert.Contains("ledger_sheet_Write", names);
        Assert.Contains("ledger_sheet_Read", names);
        Assert.Contains("ledger_sheet_properties", names);
        Assert.Contains("ledger_sheet_ExportPdf", names);
        Assert.DoesNotContain("ledger_sheet_Log", names);
        Assert.DoesNotContain(names, name => name.StartsWith("shell_", StringComparison.Ordinal) || name.StartsWith("app", StringComparison.Ordinal));
        Assert.All(names, name => Assert.Matches("^[a-zA-Z0-9_-]{1,64}$", name));
    }

    [Fact]
    public void I_livelli_degli_strumenti_seguono_quello_che_fanno()
    {
        var catalog = new Harness().Catalog;
        Assert.Equal(AgentAccessLevel.Read, catalog.Find("ledger_sheet_Read")!.Access);
        Assert.Equal(AgentAccessLevel.Read, catalog.Find("ledger_sheet_properties")!.Access);
        Assert.Equal(AgentAccessLevel.Edit, catalog.Find("ledger_sheet_Write")!.Access);
        Assert.Equal(AgentAccessLevel.Files, catalog.Find("ledger_sheet_ExportPdf")!.Access);
        Assert.Equal(AgentAccessLevel.Files, catalog.Find("export_document")!.Access);
        Assert.Equal(AgentAccessLevel.Read, catalog.Find("render_page")!.Access);
    }

    [Fact]
    public async Task Scrivere_e_leggere_passa_per_la_stessa_api_delle_macro_e_fa_un_passo_annullabile()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        var write = await harness.CallAsync("ledger_sheet_Write", new JsonObject { ["reference"] = "B2", ["value"] = "=1+2" });
        Assert.False(write.IsError);
        Assert.Equal(["Write title"], harness.Workspace.UndoSteps);

        var read = await harness.CallAsync("ledger_sheet_Read", new JsonObject { ["reference"] = "B2" });
        Assert.Equal("\"=1+2\"", Assert.IsType<AgentText>(Assert.Single(read.Content)).Text);
        Assert.Single(harness.Workspace.UndoSteps);
    }

    [Fact]
    public async Task Gli_argomenti_si_passano_come_letterali_json_e_non_come_codice()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        await harness.CallAsync("ledger_sheet_Write", new JsonObject { ["reference"] = "A1", ["value"] = "\"); sheet.Write(\"Z9\", 1); (\"" });
        Assert.Equal("\"); sheet.Write(\"Z9\", 1); (\"", harness.Sheet.Read("A1"));
        Assert.Equal(string.Empty, harness.Sheet.Read("Z9"));

        var array = await harness.CallAsync("ledger_sheet_WriteRange", new JsonObject { ["reference"] = "A1:B2", ["values"] = new JsonArray(new JsonArray(1, 2), new JsonArray(3, 4)) });
        Assert.False(array.IsError);
        Assert.Equal("2", harness.Sheet.Read("A1:B2"));
    }

    [Fact]
    public async Task Le_proprieta_si_leggono_insieme()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");
        var result = await harness.CallAsync("ledger_sheet_properties");
        var json = JsonNode.Parse(Assert.IsType<AgentText>(Assert.Single(result.Content)).Text)!;
        Assert.Equal("Sheet1", json["Name"]!.GetValue<string>());
        Assert.Equal("A1", json["Selection"]!.GetValue<string>());
    }

    [Fact]
    public async Task Un_argomento_obbligatorio_mancante_e_un_errore_leggibile()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");
        var result = await harness.CallAsync("ledger_sheet_Write", new JsonObject { ["value"] = 3 });
        Assert.True(result.IsError);
        Assert.Contains("reference", Assert.IsType<AgentText>(result.Content[0]).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Senza_documento_del_modulo_lo_strumento_lo_dice()
    {
        var result = await new Harness().CallAsync("ledger_sheet_Read", new JsonObject { ["reference"] = "A1" });
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Il_livello_di_sola_lettura_rifiuta_le_modifiche_e_lo_registra()
    {
        var harness = new Harness();
        harness.Preferences.Level = AgentAccessLevel.Read;
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        var result = await harness.CallAsync("ledger_sheet_Write", new JsonObject { ["reference"] = "A1", ["value"] = 1 });
        Assert.True(result.IsError);
        Assert.Equal(string.Empty, harness.Sheet.Read("A1"));
        Assert.Equal(AgentOperationOutcome.Refused, harness.Log.Recent[0].Outcome);
        Assert.False(harness.Invoker.IsAllowed(harness.Catalog.Find("create_document")!));
        Assert.True(harness.Invoker.IsAllowed(harness.Catalog.Find("list_documents")!));
    }

    [Fact]
    public async Task Esportare_sopra_un_file_esistente_chiede_conferma_e_il_rifiuto_ferma_tutto()
    {
        var harness = new Harness();
        var document = harness.Workspace.Add(ModuleKind.Documents, "Lettera");
        harness.Files.Existing.Add("/tmp/lettera.pdf");
        harness.Confirmation.Answer = false;

        var refused = await harness.CallAsync("export_document", new JsonObject { ["document"] = document.Id, ["path"] = "/tmp/lettera.pdf" });
        Assert.True(refused.IsError);
        Assert.Empty(harness.Workspace.Exported);
        Assert.Equal(AgentOperationOutcome.Declined, harness.Log.Recent[0].Outcome);

        harness.Confirmation.Answer = true;
        var done = await harness.CallAsync("export_document", new JsonObject { ["document"] = document.Id, ["path"] = "/tmp/lettera.pdf" });
        Assert.False(done.IsError);
        Assert.Equal(["/tmp/lettera.pdf"], harness.Workspace.Exported);
        Assert.Equal(2, harness.Confirmation.Asked.Count);
    }

    [Fact]
    public async Task Esportare_su_un_file_nuovo_non_chiede_conferma_ma_vuole_un_percorso_assoluto()
    {
        var harness = new Harness();
        var document = harness.Workspace.Add(ModuleKind.Documents, "Lettera");

        Assert.False((await harness.CallAsync("export_document", new JsonObject { ["document"] = document.Id, ["path"] = "/tmp/nuovo.pdf" })).IsError);
        Assert.Empty(harness.Confirmation.Asked);
        Assert.True((await harness.CallAsync("export_document", new JsonObject { ["document"] = document.Id, ["path"] = "relativo.pdf" })).IsError);
    }

    [Fact]
    public async Task Salvare_sovrascrive_e_chiede_sempre_conferma()
    {
        var harness = new Harness();
        var document = harness.Workspace.Add(ModuleKind.Documents, "Lettera", "/tmp/lettera.odt");
        Assert.False((await harness.CallAsync("save_document", new JsonObject { ["document"] = document.Id })).IsError);
        var asked = Assert.Single(harness.Confirmation.Asked);
        Assert.Equal(PulseOffice.Agent.Hosting.AgentConfirmationKind.Overwrite, asked.Kind);
        Assert.Equal("/tmp/lettera.odt", asked.Subject);
    }

    [Fact]
    public async Task I_comandi_si_elencano_senza_quelli_vietati_e_si_eseguono_per_nome()
    {
        var harness = new Harness();
        var listed = JsonNode.Parse(((AgentText)(await harness.CallAsync("list_commands")).Content[0]).Text)!.AsArray();
        var ids = listed.Select(node => node!["id"]!.GetValue<string>()).ToList();
        Assert.Contains("ledger.format.bold", ids);
        Assert.DoesNotContain("shell.tools.options", ids);
        Assert.DoesNotContain("ledger.data.sort", ids);
        Assert.Equal("files", listed.First(node => node!["id"]!.GetValue<string>() == "shell.file.exportPdf")!["permission"]!.GetValue<string>());

        Assert.False((await harness.CallAsync("run_command", new JsonObject { ["id"] = "ledger.format.bold" })).IsError);
        Assert.Equal(["ledger.format.bold"], harness.Commands.Ran);
        Assert.Empty(harness.Confirmation.Asked);
        Assert.True((await harness.CallAsync("run_command", new JsonObject { ["id"] = "shell.tools.options" })).IsError);
        Assert.True((await harness.CallAsync("run_command", new JsonObject { ["id"] = "ledger.data.sort" })).IsError);
    }

    [Fact]
    public async Task Un_comando_che_l_app_rifiuta_non_si_dice_eseguito()
    {
        var harness = new Harness();
        harness.Commands.Refusal = "The protected sheet does not allow formatting cells.";

        var result = await harness.CallAsync("run_command", new JsonObject { ["id"] = "ledger.format.bold" });

        Assert.True(result.IsError);
        Assert.Contains("protected sheet", Assert.IsType<AgentText>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
        Assert.Empty(harness.Commands.Ran);
    }

    [Fact]
    public async Task Un_comando_irreversibile_chiede_conferma_e_uno_di_file_rispetta_il_livello()
    {
        var harness = new Harness();
        await harness.CallAsync("run_command", new JsonObject { ["id"] = "ledger.sheet.delete" });
        Assert.Equal("run_command", Assert.Single(harness.Confirmation.Asked).Tool);

        harness.Preferences.Level = AgentAccessLevel.Edit;
        var refused = await harness.CallAsync("run_command", new JsonObject { ["id"] = "shell.file.exportPdf" });
        Assert.True(refused.IsError);
        Assert.DoesNotContain("shell.file.exportPdf", harness.Commands.Ran);
    }

    [Fact]
    public async Task La_vista_restituisce_un_immagine_png()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Presentations, "Slides");
        var result = await harness.CallAsync("render_page", new JsonObject { ["module"] = "deck", ["page"] = 2 });
        var image = Assert.IsType<AgentImage>(result.Content[0]);
        Assert.Equal("image/png", image.MimeType);
        Assert.Contains("\"page\":2", ((AgentText)result.Content[1]).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Creare_e_elencare_documenti()
    {
        var harness = new Harness();
        var created = JsonNode.Parse(((AgentText)(await harness.CallAsync("create_document", new JsonObject { ["module"] = "folio" })).Content[0]).Text)!;
        Assert.Equal("folio", created["module"]!.GetValue<string>());
        Assert.True((await harness.CallAsync("create_document", new JsonObject { ["module"] = "calc" })).IsError);
        var listed = JsonNode.Parse(((AgentText)(await harness.CallAsync("list_documents")).Content[0]).Text)!.AsArray();
        Assert.Single(listed);
    }
}

public sealed class ScriptPermissionTests
{
    [Fact]
    public async Task A_script_member_outside_the_undo_history_is_confirmed_as_irreversible()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");
        harness.Confirmation.Answer = false;

        await harness.CallAsync("ledger_sheet_SetTracking", new JsonObject { ["on"] = true });

        Assert.Equal(AgentConfirmationKind.Irreversible, Assert.Single(harness.Confirmation.Asked).Kind);
        Assert.False(harness.Sheet.Tracking);
    }

    [Fact]
    public async Task A_huge_argument_does_not_flood_the_confirmation_the_user_reads()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");
        harness.Confirmation.Answer = false;

        await harness.CallAsync("ledger_sheet_ExportPdf", new JsonObject { ["path"] = "/tmp/" + new string('a', 200_000) + ".pdf" });

        var asked = Assert.Single(harness.Confirmation.Asked);
        Assert.True(asked.Subject.Length <= AgentConfirmationNeed.MaximumSubjectLength + 64, $"{asked.Subject.Length} characters");
        Assert.StartsWith("sheet.ExportPdf", asked.Subject, StringComparison.Ordinal);
        Assert.EndsWith("…", asked.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Al_livello_file_l_agente_esporta_e_lo_script_vede_il_permesso_di_scrittura()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");

        var result = await harness.CallAsync("ledger_sheet_ExportPdf", new JsonObject { ["path"] = "/tmp/conti.pdf" });

        Assert.False(result.IsError);
        Assert.False(harness.Permissions.Current.FileSystemWrite);
    }

    [Fact]
    public async Task Uno_script_al_livello_modifica_non_scrive_file()
    {
        var harness = new Harness();
        harness.Workspace.Add(ModuleKind.Sheets, "Conti");
        var runner = new PulseOffice.Agent.Tools.ScriptToolRunner(harness.Workspace,
            new ModuleAgentScripts([new PulseOffice.Scripting.JavaScript.JavaScriptEngine()], [new FakeGlobals(harness.Sheet)], harness.Permissions));

        var result = await runner.RunAsync(ModuleKind.Sheets, null, "x", "sheet.ExportPdf(\"/tmp/x.pdf\")", true, AgentAccessLevel.Edit, CancellationToken.None);

        Assert.True(result.IsError);
    }
}

public sealed class ScriptResultTests
{
    private sealed class NotANumber : PulseOffice.Agent.Hosting.IAgentScripts
    {
        public Task<PulseOffice.Agent.Hosting.AgentScriptOutcome> RunAsync(PulseOffice.Foundation.Modules.ModuleKind module, string source, PulseOffice.Agent.AgentAccessLevel access, CancellationToken ct) =>
            Task.FromResult(new PulseOffice.Agent.Hosting.AgentScriptOutcome(true, double.NaN, null));
    }

    [Fact]
    public async Task Un_numero_che_non_c_e_torna_come_NaN_e_non_come_errore()
    {
        var workspace = new FakeWorkspace();
        workspace.Add(PulseOffice.Foundation.Modules.ModuleKind.Sheets, "Ledger1");
        var runner = new PulseOffice.Agent.Tools.ScriptToolRunner(workspace, new NotANumber());

        var result = await runner.RunAsync(PulseOffice.Foundation.Modules.ModuleKind.Sheets, null, null, "sheet.Number(\"A1\")", true, PulseOffice.Agent.AgentAccessLevel.Read, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("\"NaN\"", Assert.IsType<PulseOffice.Agent.Tools.AgentText>(Assert.Single(result.Content)).Text);
    }

    private sealed class Nothing : PulseOffice.Agent.Hosting.IAgentScripts
    {
        public Task<PulseOffice.Agent.Hosting.AgentScriptOutcome> RunAsync(PulseOffice.Foundation.Modules.ModuleKind module, string source, PulseOffice.Agent.AgentAccessLevel access, CancellationToken ct) =>
            Task.FromResult(new PulseOffice.Agent.Hosting.AgentScriptOutcome(true, null, null));
    }

    [Theory]
    [InlineData(true, "null")]
    [InlineData(false, "Done.")]
    public async Task Una_lettura_senza_valore_torna_null_e_non_Done(bool returnsValue, string expected)
    {
        var workspace = new FakeWorkspace();
        workspace.Add(PulseOffice.Foundation.Modules.ModuleKind.Sheets, "Ledger1");
        var runner = new PulseOffice.Agent.Tools.ScriptToolRunner(workspace, new Nothing());

        var result = await runner.RunAsync(PulseOffice.Foundation.Modules.ModuleKind.Sheets, null, null, "sheet.Formula(\"A1\")", returnsValue, PulseOffice.Agent.AgentAccessLevel.Read, CancellationToken.None);

        Assert.Equal(expected, Assert.IsType<PulseOffice.Agent.Tools.AgentText>(Assert.Single(result.Content)).Text);
    }
}
