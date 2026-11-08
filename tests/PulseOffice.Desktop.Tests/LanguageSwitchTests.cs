using PulseOffice.Testing;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Localization;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class LanguageSwitchTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-language-" + Guid.NewGuid().ToString("N"));

    public LanguageSwitchTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    [Fact]
    public Task La_barra_della_formula_cambia_separatori_e_nomi_con_la_lingua() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var texts = provider.GetRequiredService<ITextCatalog>();
        var original = texts.Language;
        try
        {
            texts.Use(UiLanguage.English);
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, provider.GetRequiredService<IDocumentWorkspace>().Context);
            Dispatcher.UIThread.RunJobs();
            var sheet = provider.GetRequiredService<PulseOffice.Sheets.UI.IActiveSheetContext>();
            sheet.Engine!.SetFormula(new PulseOffice.Sheets.SheetCell(0, PulseOffice.Sheets.Addressing.CellAddress.First), "=SUM({1;2|3;4};0.5)");
            texts.Use(UiLanguage.Italian);
            texts.Use(UiLanguage.English);
            Dispatcher.UIThread.RunJobs();
            var bar = window.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "FormulaBar");
            Assert.Equal("=SUM({1,2;3,4},0.5)", bar.Text);

            texts.Use(UiLanguage.Italian);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("=SOMMA({1;2|3;4};0,5)", bar.Text);
        }
        finally
        {
            texts.Use(original);
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Nessuna_vista_dei_tre_moduli_resta_nella_lingua_precedente() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var texts = provider.GetRequiredService<ITextCatalog>();
        var original = texts.Language;
        var italianOnly = ItalianOnlyTexts();
        var stale = new List<string>();
        try
        {
            foreach (var module in new[] { "documents", "sheets", "presentations" })
            {
                texts.Use(UiLanguage.Italian);
                await executor.ExecuteAsync(registry.Find(new CommandId($"shell.file.new.{module}"))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
                var side = window.GetVisualDescendants().OfType<SidePanelView>().Single();
                foreach (var page in side.Pages)
                {
                    texts.Use(UiLanguage.Italian);
                    side.Show(page);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    texts.Use(UiLanguage.English);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    stale.AddRange(Shown(window).Where(italianOnly.Contains).Select(text => $"{module}/{page}: «{text}»"));
                }
            }
        }
        finally
        {
            texts.Use(original);
        }
        Assert.True(stale.Count == 0, string.Join("\n", stale.Distinct()));
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Le_impostazioni_aperte_seguono_il_cambio_di_lingua_che_vi_si_sceglie() => _session.Dispatch(async () =>
    {
        const string command = "shell.tools.options";
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var texts = provider.GetRequiredService<ITextCatalog>();
        var original = texts.Language;
        var italianOnly = ItalianOnlyTexts();
        var host = window.GetVisualDescendants().OfType<DialogHost>().Single();
        List<string> stale;
        try
        {
            texts.Use(UiLanguage.Italian);
            await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
            var showing = executor.ExecuteAsync(registry.Find(new CommandId(command))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(host.IsDialogOpen, command);
            texts.Use(UiLanguage.English);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            stale = Shown(window).Where(italianOnly.Contains).Distinct().ToList();
            host.Close(null);
            Dispatcher.UIThread.RunJobs();
            await showing;
        }
        finally
        {
            texts.Use(original);
        }
        Assert.True(stale.Count == 0, command + ": " + string.Join(" | ", stale));
        window.Close();
    }, CancellationToken.None);

    private static IEnumerable<string> Shown(Window window) =>
        window.GetVisualDescendants()
            .Where(visual => visual is not Visual { IsEffectivelyVisible: false })
            .SelectMany(visual => visual switch
            {
                TextBlock block => [block.Text ?? string.Empty],
                ContentControl { Content: string content } => [content],
                MenuItem { Header: string header } => [header],
                Control control when ToolTip.GetTip(control) is string tip => [tip],
                _ => Array.Empty<string>(),
            })
            .Select(text => text.Trim())
            .Where(text => text.Length > 3);

    private static HashSet<string> ItalianOnlyTexts()
    {
        var resources = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PulseOffice.Localization", "Resources");
        HashSet<string> Values(string language) =>
            JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(resources, language + ".json")))!
                .Values.SelectMany(value => new[] { value.Trim(), value.Trim().ToUpperInvariant() }).ToHashSet(StringComparer.Ordinal);
        var italian = Values("it");
        italian.ExceptWith(Values("en"));
        return italian;
    }
}
