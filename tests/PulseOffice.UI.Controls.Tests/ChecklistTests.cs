using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Lists;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ChecklistTests
{
    private static IEnumerable<string> Sheets(int count) => Enumerable.Range(1, count).Select(index => $"Foglio{index}");

    [Fact]
    public void IlFiltroNonDistingueMaiuscoleEAccenti()
    {
        var model = new ChecklistModel(["Città", "Vendite", "Costi"], []);
        model.Filter("CITTA");

        Assert.Equal(["Città"], model.Visible.Select(item => item.Name));
    }

    [Fact]
    public void SelezionaTuttiTocaSoloLeVociVisibili()
    {
        var model = new ChecklistModel(["Nord", "Sud", "Nord-est"], []);
        model.Filter("nord");
        model.SetVisible(true);
        model.Filter(string.Empty);

        Assert.Equal(["Nord", "Nord-est"], model.Checked);
    }

    [Fact]
    public void LeSceltePresentiRestanoNellOrdineOriginale()
    {
        var model = new ChecklistModel(["C", "A", "B"], ["B", "C"]);

        Assert.Equal(["C", "B"], model.Checked);
        Assert.Equal(2, model.CheckedCount);
    }

    [Fact]
    public Task TrecentoVociNonCreanoTrecentoRigheEdHannoUnaAltezzaFissa() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        var names = Sheets(300).ToList();
        var control = new ChecklistControl(new ChecklistModel(names, names), new TextCatalog(UiLanguage.Italian), metrics);
        control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = new Window { Width = 420, Height = 900, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(control.RealizedRows < 40, $"righe create: {control.RealizedRows}");
        Assert.True(control.Bounds.Height < 360, $"altezza: {control.Bounds.Height}");
        Assert.Equal(300, control.Model.CheckedCount);

        var search = control.GetVisualDescendants().OfType<TextBox>().Single();
        search.Text = "Foglio299";
        Dispatcher.UIThread.RunJobs();
        Assert.Single(control.Model.Visible);
        search.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(300, control.Model.Visible.Count);
        window.Close();
    }, CancellationToken.None);
}
