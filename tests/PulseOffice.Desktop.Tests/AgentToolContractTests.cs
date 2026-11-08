using PulseOffice.Testing;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Tools;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class AgentToolContractTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-agent-contract-" + Guid.NewGuid().ToString("N"));

    private readonly string _files = Path.Combine(Path.GetTempPath(), "pulseoffice-agent-files-" + Guid.NewGuid().ToString("N"));

    public AgentToolContractTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
        Directory.CreateDirectory(_files);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
        if (Directory.Exists(_files)) Directory.Delete(_files, recursive: true);
    }

    private sealed class FullAccess : IAgentPreferences
    {
        public bool Enabled => true;

        public int Port => 0;

        public AgentAccessLevel Level => AgentAccessLevel.Files;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    private sealed class AlwaysYes : IAgentConfirmation
    {
        public Task<bool> ConfirmAsync(AgentConfirmationRequest request, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Agent(ServiceProvider provider)
    {
        private readonly IAgentToolCatalog _catalog = provider.GetRequiredService<IAgentToolCatalog>();
        private readonly IAgentToolInvoker _invoker = provider.GetRequiredService<IAgentToolInvoker>();

        public async Task<(bool Error, string Text, AgentToolResult Result)> Call(string tool, JsonObject? arguments = null)
        {
            var running = _invoker.InvokeAsync(_catalog.Find(tool)!, arguments ?? [], "contract", CancellationToken.None);
            while (!running.IsCompleted) Dispatcher.UIThread.RunJobs();
            var result = await running;
            Dispatcher.UIThread.RunJobs();
            return (result.IsError, string.Join("\n", result.Content.OfType<AgentText>().Select(text => text.Text)), result);
        }

        public async Task<string> Fails(string tool, JsonObject arguments)
        {
            var (error, text, _) = await Call(tool, arguments);
            Assert.True(error, $"{tool} {arguments.ToJsonString()} should fail but said: {text}");
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
            return text;
        }

        public async Task<JsonNode> Works(string tool, JsonObject? arguments = null)
        {
            var (error, text, _) = await Call(tool, arguments);
            Assert.False(error, $"{tool} {arguments?.ToJsonString()} failed: {text}");
            return JsonNode.Parse(text.Split('\n')[^1])!;
        }

        public async Task<string> Create(string module) => (await Works("create_document", new JsonObject { ["module"] = module }))["id"]!.GetValue<string>();
    }

    private static readonly IReadOnlyList<string> Covered =
    [
        "list_documents", "activate_document", "create_document", "open_document", "save_document", "save_document_as",
        "export_document", "render_page", "list_commands", "run_command",
    ];

    private ServiceCollection Register()
    {
        var services = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable());
        services.AddSingleton<IAgentPreferences>(new FullAccess());
        services.AddSingleton<IAgentConfirmation>(new AlwaysYes());
        return services;
    }

    private Task Run(Func<Agent, Task> body) => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        try
        {
            await body(new Agent(provider));
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Ogni_strumento_che_non_viene_dal_catalogo_ha_il_suo_caso() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var handwritten = provider.GetServices<IAgentToolSource>()
            .Where(source => source is not ScriptCatalogToolSource)
            .SelectMany(source => source.Tools)
            .Select(tool => tool.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(Covered.Order(StringComparer.Ordinal), handwritten);
    }, CancellationToken.None);

    [Fact]
    public Task Documenti_creati_elencati_e_attivati() => Run(async agent =>
    {
        Assert.Empty((await agent.Works("list_documents")).AsArray());
        await agent.Fails("create_document", new JsonObject { ["module"] = "writer" });
        await agent.Fails("create_document", []);
        var ledger = await agent.Create("ledger");
        var folio = await agent.Create("folio");
        var listed = (await agent.Works("list_documents")).AsArray();
        Assert.Equal(2, listed.Count);
        Assert.True(listed.Single(document => document!["id"]!.GetValue<string>() == folio)!["active"]!.GetValue<bool>());

        await agent.Fails("activate_document", new JsonObject { ["document"] = "nope" });
        await agent.Fails("activate_document", []);
        Assert.True((await agent.Works("activate_document", new JsonObject { ["document"] = ledger }))["active"]!.GetValue<bool>());
    });

    [Fact]
    public Task Aprire_rifiuta_con_un_motivo_e_apre_cio_che_e_stato_salvato() => Run(async agent =>
    {
        await agent.Fails("open_document", []);
        await agent.Fails("open_document", new JsonObject { ["path"] = "relative.ods" });
        Assert.Contains("does not exist", await agent.Fails("open_document", new JsonObject { ["path"] = Path.Combine(_files, "missing.ods") }), StringComparison.Ordinal);
        Assert.Contains("folder", await agent.Fails("open_document", new JsonObject { ["path"] = _files }), StringComparison.Ordinal);
        var strange = Path.Combine(_files, "strange.xyz");
        File.WriteAllText(strange, "nothing");
        await agent.Fails("open_document", new JsonObject { ["path"] = strange });
        var csv = Path.Combine(_files, "data.csv");
        File.WriteAllText(csv, "a;b\n1;2\n");
        await agent.Fails("open_document", new JsonObject { ["path"] = csv, ["csv_skip_rows"] = "many" });
        await agent.Fails("open_document", new JsonObject { ["path"] = csv, ["csv_encoding"] = "klingon" });

        var ledger = await agent.Create("ledger");
        var saved = Path.Combine(_files, "conti.ods");
        await agent.Works("save_document_as", new JsonObject { ["document"] = ledger, ["path"] = saved });
        Assert.True(File.Exists(saved));
        var opened = await agent.Works("open_document", new JsonObject { ["path"] = saved });
        Assert.Equal(saved, opened["path"]!.GetValue<string>());
        var fromCsv = await agent.Works("open_document", new JsonObject { ["path"] = csv, ["csv_separator"] = "semicolon" });
        Assert.Equal("ledger", fromCsv["module"]!.GetValue<string>());
    });

    [Fact]
    public Task Salvare_ed_esportare_dicono_perche_non_scrivono() => Run(async agent =>
    {
        var folio = await agent.Create("folio");
        Assert.Contains("save_document_as", await agent.Fails("save_document", new JsonObject { ["document"] = folio }), StringComparison.Ordinal);
        await agent.Fails("save_document", new JsonObject { ["document"] = "nope" });
        await agent.Fails("save_document_as", new JsonObject { ["document"] = folio });
        await agent.Fails("save_document_as", new JsonObject { ["document"] = folio, ["path"] = "testo.odt" });
        await agent.Fails("save_document_as", new JsonObject { ["document"] = folio, ["path"] = Path.Combine(_files, "testo.xlsx") });
        await agent.Fails("save_document_as", new JsonObject { ["document"] = folio, ["path"] = Path.Combine(_files, "manca", "testo.odt") });
        await agent.Fails("save_document_as", new JsonObject { ["document"] = folio, ["path"] = _files });

        var odt = Path.Combine(_files, "testo.odt");
        await agent.Works("save_document_as", new JsonObject { ["document"] = folio, ["path"] = odt });
        await agent.Works("save_document", new JsonObject { ["document"] = folio });
        var docx = Path.Combine(_files, "testo.docx");
        await agent.Works("save_document_as", new JsonObject { ["document"] = folio, ["path"] = docx });
        Assert.True(File.Exists(odt) && File.Exists(docx));

        await agent.Fails("export_document", new JsonObject { ["document"] = folio });
        await agent.Fails("export_document", new JsonObject { ["document"] = folio, ["path"] = "copia.pdf" });
        await agent.Fails("export_document", new JsonObject { ["document"] = folio, ["path"] = Path.Combine(_files, "copia.pptx") });
        await agent.Fails("export_document", new JsonObject { ["document"] = folio, ["path"] = Path.Combine(_files, "manca", "copia.pdf") });
        await agent.Fails("export_document", new JsonObject { ["document"] = folio, ["path"] = Path.Combine(_files, "manca", "copia.odt") });
        var pdf = Path.Combine(_files, "copia.pdf");
        await agent.Works("export_document", new JsonObject { ["document"] = folio, ["path"] = pdf });
        Assert.StartsWith("%PDF", File.ReadAllText(pdf)[..4], StringComparison.Ordinal);
    });

    [Fact]
    public Task Disegnare_una_pagina_rifiuta_gli_argomenti_limite() => Run(async agent =>
    {
        await agent.Fails("render_page", []);
        var ledger = await agent.Create("ledger");
        var folio = await agent.Create("folio");
        await agent.Fails("render_page", new JsonObject { ["document"] = folio, ["page"] = 0 });
        await agent.Fails("render_page", new JsonObject { ["document"] = folio, ["page"] = 99 });
        await agent.Fails("render_page", new JsonObject { ["document"] = folio, ["width"] = 50 });
        await agent.Fails("render_page", new JsonObject { ["document"] = folio, ["width"] = 9000 });
        await agent.Fails("render_page", new JsonObject { ["document"] = folio, ["range"] = "A1:B2" });
        await agent.Fails("render_page", new JsonObject { ["document"] = folio, ["module"] = "ledger" });
        await agent.Fails("render_page", new JsonObject { ["document"] = "nope" });
        await agent.Fails("render_page", new JsonObject { ["document"] = ledger, ["range"] = "ZZZ" });
        await agent.Fails("render_page", new JsonObject { ["module"] = "deck" });

        var (error, text, result) = await agent.Call("render_page", new JsonObject { ["document"] = folio, ["width"] = 400 });
        Assert.False(error, text);
        var image = Assert.Single(result.Content.OfType<AgentImage>());
        Assert.Equal("image/png", image.MimeType);
    });

    [Fact]
    public Task I_comandi_si_elencano_e_ogni_rifiuto_arriva_all_agente() => Run(async agent =>
    {
        Assert.Empty((await agent.Works("list_commands", new JsonObject { ["search"] = "zzzz-nessuno" })).AsArray());
        await agent.Fails("list_commands", new JsonObject { ["includeDisabled"] = "maybe" });
        await agent.Fails("run_command", []);
        await agent.Fails("run_command", new JsonObject { ["id"] = "ledger.nope" });
        await agent.Fails("run_command", new JsonObject { ["id"] = "shell.file.quit" });

        await agent.Create("ledger");
        var listed = (await agent.Works("list_commands", new JsonObject { ["search"] = "bold" })).AsArray();
        Assert.Contains(listed, command => command!["id"]!.GetValue<string>() == "ledger.format.bold");
        var bold = await agent.Call("run_command", new JsonObject { ["id"] = "ledger.format.bold" });
        Assert.False(bold.Error, bold.Text);
        Assert.Contains("disabled", await agent.Fails("run_command", new JsonObject { ["id"] = "shell.edit.redo" }), StringComparison.Ordinal);
        Assert.Contains("did not run", await agent.Fails("run_command", new JsonObject { ["id"] = "ledger.trace.precedents" }), StringComparison.Ordinal);
    });
}
