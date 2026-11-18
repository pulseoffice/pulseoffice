using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.UI;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round203PivotKeyboardTests(HeadlessDesktopSession headless)
{
    [Fact]
    public Task Toggling_a_field_with_the_keyboard_keeps_the_focus_on_it() => headless.Session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var sheet = new PulseOffice.Sheets.Worksheet("Data");
        sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), PulseOffice.Sheets.Values.CellValue.Text("Zone"));
        sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 1), PulseOffice.Sheets.Values.CellValue.Text("Total"));
        sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(1, 0), PulseOffice.Sheets.Values.CellValue.Text("North"));
        sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(1, 1), PulseOffice.Sheets.Values.CellValue.Number(10));
        var source = new RangePivotSource(sheet, new PulseOffice.Sheets.Addressing.CellRange(new(0, 0), new(1, 1)));
        var view = new PivotPromptView(source.Headers, "Pivot1", provider.GetRequiredService<LedgerDialogKit>(),
            new SourcePivotPreviewer(provider.GetRequiredService<IPivotEngine>(), source, new PivotLabels("Total", "Total {0}")));
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.FieldBoxes[1].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([1], view.Model.In(PivotArea.Value));
        Assert.Same(view.FieldBoxes[1], window.FocusManager?.GetFocusedElement());
        window.Close();
    }, CancellationToken.None);
}
