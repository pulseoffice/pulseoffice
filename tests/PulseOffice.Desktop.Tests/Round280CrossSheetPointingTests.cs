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
public sealed class Round280CrossSheetPointingTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-cross-" + Guid.NewGuid().ToString("N"));

    public Round280CrossSheetPointingTests(HeadlessDesktopSession headless)
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
    public Task Switching_sheet_while_typing_a_formula_points_at_the_other_sheet_and_Enter_returns() => _session.Dispatch(async () =>
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
            var first = sheet.Workbook!.Sheets[0];
            var other = sheet.Workbook.AddSheet("Data 2");
            other.SetValue(new CellAddress(2, 1), PulseOffice.Sheets.Values.CellValue.Number(5));

            window.KeyTextInput("=");
            Dispatcher.UIThread.RunJobs();
            sheet.SelectSheet(1);
            Dispatcher.UIThread.RunJobs();
            sheet.SelectRange(new CellRange(new CellAddress(2, 1), new CellAddress(2, 1)));
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("*2");
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("Data 2", first[CellAddress.First].Formula ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("B3", first[CellAddress.First].Formula ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(0, sheet.SheetIndex);
            Assert.True(other[new CellAddress(2, 1)].Formula is null);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);
}
