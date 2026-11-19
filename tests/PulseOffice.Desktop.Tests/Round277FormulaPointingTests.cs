using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI;
using PulseOffice.Shell;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round277FormulaPointingTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-point-" + Guid.NewGuid().ToString("N"));

    public Round277FormulaPointingTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private static void Press(ShellWindow window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Arrow_keys_while_typing_a_formula_point_at_cells_and_Enter_returns_to_the_edited_cell() => _session.Dispatch(async () =>
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        await using var _ = provider;
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        try
        {
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)workspace.Active!.View).FocusDocument();
            Dispatcher.UIThread.RunJobs();
            var sheet = provider.GetRequiredService<IActiveSheetContext>();
            var worksheet = sheet.Workbook!.Sheets[sheet.SheetIndex];
            worksheet.SetValue(new CellAddress(1, 0), PulseOffice.Sheets.Values.CellValue.Number(4));
            worksheet.SetValue(new CellAddress(2, 0), PulseOffice.Sheets.Values.CellValue.Number(5));
            sheet.SelectRange(new CellRange(new CellAddress(0, 1), new CellAddress(0, 1)));
            Dispatcher.UIThread.RunJobs();

            window.KeyTextInput("=SUM(");
            Dispatcher.UIThread.RunJobs();
            Press(window, Key.Down, PhysicalKey.ArrowDown);
            Press(window, Key.Left, PhysicalKey.ArrowLeft);
            Press(window, Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            window.KeyTextInput(")");
            Dispatcher.UIThread.RunJobs();
            Press(window, Key.Enter, PhysicalKey.Enter);

            Assert.Equal("=SUM(A2:A3)", "=" + worksheet[new CellAddress(0, 1)].Formula?.TrimStart('='));
            Assert.Equal(new CellAddress(1, 1), sheet.Selection.Start);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Tab_accepts_a_function_name_and_the_argument_tip_follows_the_caret() => _session.Dispatch(async () =>
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        await using var _ = provider;
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        try
        {
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>().Use(PulseOffice.Localization.UiLanguage.English);
            var view = (LedgerView)workspace.Active!.View;
            view.FocusDocument();
            Dispatcher.UIThread.RunJobs();

            foreach (var letter in new[] { "=", "A", "B" })
            {
                window.KeyTextInput(letter);
                Dispatcher.UIThread.RunJobs();
            }
            Press(window, Key.Tab, PhysicalKey.Tab);
            var editor = Assert.IsType<Avalonia.Controls.TextBox>(window.FocusManager?.GetFocusedElement());

            Assert.EndsWith("(", editor.Text, StringComparison.Ordinal);
            var tip = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<Avalonia.Controls.TextBlock>()
                .Where(block => block.Inlines is { Count: > 0 }).ToList();
            Assert.Contains(tip, block => block.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);
}
