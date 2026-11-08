using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class FormFieldVisibilityTests
{
    [Fact]
    public Task UnCampoCondizionatoCompareSoloQuandoLaSceltaLoChiede() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var form = new FormPromptView(string.Empty,
        [
            new ChoiceField("kind", "Kind", ["plain", "dated"]),
            new TextField("date", "Date") { ShownWhen = values => Convert.ToInt32(values["kind"], System.Globalization.CultureInfo.InvariantCulture) == 1 },
        ], texts);
        var window = new Window { Content = form };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var combo = form.GetVisualDescendants().OfType<ComboBox>().Single();
        var row = form.GetVisualDescendants().OfType<PulseOffice.UI.Controls.Dialogs.DialogRow>()
            .Single(candidate => candidate.Label?.Text == "Date" && candidate.Editor is TextBox);
        Assert.False(row.IsVisible);

        combo.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.True(row.IsVisible);

        combo.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.False(row.IsVisible);
        Assert.True(form.Values.ContainsKey("date"));
        window.Close();
    }, CancellationToken.None);
}
