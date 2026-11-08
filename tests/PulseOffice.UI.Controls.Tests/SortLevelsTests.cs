using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Sorting;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SortLevelsTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly TextCatalog _texts = new(UiLanguage.Italian);

    [Fact]
    public void Il_modello_aggiunge_copia_sposta_ed_elimina_i_livelli()
    {
        var model = new SortLevelsModel(3);
        Assert.Equal([new SortLevel(0)], model.Levels);
        Assert.False(model.CanRemove);

        model.Add();
        Assert.Equal(new SortLevel(1), model.Levels[1]);
        Assert.Equal(1, model.Selected);

        model.Set(1, new SortLevel(1, Descending: true));
        model.MoveUp();
        Assert.Equal(new SortLevel(1, true), model.Levels[0]);
        Assert.Equal(0, model.Selected);
        Assert.False(model.CanMoveUp);

        model.CopySelected();
        Assert.True(model.HasDuplicates);
        Assert.False(model.CanAdd);

        model.RemoveSelected();
        Assert.False(model.HasDuplicates);
        Assert.Equal(2, model.Levels.Count);
        Assert.Equal(1, model.Selected);
    }

    [Fact]
    public Task La_vista_usa_le_intestazioni_e_restituisce_i_livelli() => Session.Dispatch(() =>
    {
        var view = new SortLevelsView(
            new SortLevelsRequest("command.ledger.data.sort.title", ["Nome", "Città", "Totale"], ["Colonna A", "Colonna B", "Colonna C"])
            {
                CustomOrders = ["lun, mar, mer, …"],
                Initial = [new SortLevel(2)],
            },
            _texts, new DataDialogIcons(), DesignTokens.For(InterfaceDensity.Compact));
        var window = new Window { Width = 700, Height = 500, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.Equal("Totale", view.ColumnBoxes[0].SelectedItem);
        Assert.False(view.Actions.IsActionEnabled("delete"));

        Assert.True(view.Actions.Press("add"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, view.ColumnBoxes.Count);
        Assert.Equal("Nome", view.ColumnBoxes[1].SelectedItem);
        view.OrderGroups[1].SelectKey("descending");

        view.HeaderBox.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Colonna C", view.ColumnBoxes[0].SelectedItem);

        view.ColumnBoxes[1].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new SortLevel(0, true), view.Model.Levels[0]);

        Assert.Empty(view.Warning);
        view.ColumnBoxes[1].SelectedIndex = 0;
        Assert.NotEmpty(view.Warning);
        view.ColumnBoxes[1].SelectedIndex = 1;

        ((DialogFooter)view.Footer!).GetLogicalDescendants().OfType<Button>().Last().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var choice = Assert.IsType<SortLevelsChoice>(result);
        Assert.False(choice.HasHeader);
        Assert.Equal([new SortLevel(0, true), new SortLevel(1)], choice.Levels);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);
}
