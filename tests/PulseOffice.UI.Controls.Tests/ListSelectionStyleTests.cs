using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ListSelectionStyleTests
{
    [Fact]
    public Task La_voce_scelta_di_un_elenco_con_il_fuoco_ha_l_accento_tenue_non_la_tinta_piena() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var list = new ListBox { ItemsSource = new[] { new TextBlock { Text = "0,00" }, new TextBlock { Text = "1.234,00" } } };
        list.Classes.Add("compact");
        var combo = new ComboBox { ItemsSource = new[] { "uno", "due" }, SelectedIndex = 1 };
        var subtle = new SolidColorBrush(Color.Parse("#DCFCE7"));
        var window = new Window { Content = new StackPanel { Children = { list, combo } }, Width = 300, Height = 300 };
        window.Resources["PulseAccentSubtle"] = subtle;
        window.Show();
        list.SelectedIndex = 1;
        list.Focus();
        Dispatcher.UIThread.RunJobs();

        var item = (ListBoxItem)list.ContainerFromIndex(1)!;
        item.Focus();
        Dispatcher.UIThread.RunJobs();
        var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().First(part => part.Name == "PART_ContentPresenter");
        Assert.Equal(subtle.Color, Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);

        combo.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();
        if (combo.ContainerFromIndex(1) is ComboBoxItem chosen)
        {
            var part = chosen.GetVisualDescendants().OfType<ContentPresenter>().First(candidate => candidate.Name == "PART_ContentPresenter");
            Assert.Equal(subtle.Color, Assert.IsAssignableFrom<ISolidColorBrush>(part.Background).Color);
        }
        window.Close();
    }, CancellationToken.None);
}
