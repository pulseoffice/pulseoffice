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

public sealed class Round126SortLevelsTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly TextCatalog _texts = new(UiLanguage.English);

    private (SortLevelsView View, Window Window) Show(SortLevelsRequest request)
    {
        var view = new SortLevelsView(request, _texts, new DataDialogIcons(), DesignTokens.For(InterfaceDensity.Compact));
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    private Button Confirm(SortLevelsView view) => ((DialogFooter)view.Footer!).GetLogicalDescendants().OfType<Button>().Last();

    [Fact]
    public Task A_duplicate_column_blocks_the_confirmation() => Session.Dispatch(() =>
    {
        var (view, window) = Show(new SortLevelsRequest("command.ledger.data.sort.title", ["A", "B"], ["Column A", "Column B"]));
        object? result = null;
        view.Completed += (_, value) => result = value;

        view.Actions.Press("add");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Confirm(view).IsEnabled);

        view.ColumnBoxes[1].SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.False(Confirm(view).IsEnabled);
        Assert.NotEmpty(view.Warning);
        Confirm(view).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Null(result);

        view.ColumnBoxes[1].SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.True(Confirm(view).IsEnabled);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Alt_plus_adds_a_level_and_alt_minus_removes_it_without_an_insert_key() => Session.Dispatch(() =>
    {
        var (view, window) = Show(new SortLevelsRequest("command.ledger.data.sort.title", ["A", "B", "C"], ["Column A", "Column B", "Column C"]));
        view.ColumnBoxes[0].Focus();

        window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, view.Model.Levels.Count);

        window.KeyPressQwerty(PhysicalKey.Minus, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.Single(view.Model.Levels);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task The_left_to_right_option_offers_rows_and_is_returned() => Session.Dispatch(() =>
    {
        var (view, window) = Show(new SortLevelsRequest("command.ledger.data.sort.title", ["Name", "Total"], ["Column A", "Column B"])
        {
            RowHeaders = ["Key", "Value", "Note"],
            RowLetters = ["Row 1", "Row 2", "Row 3"],
            LeftToRight = true,
            Initial = [new SortLevel(2)],
        });
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.True(view.DirectionBox.IsVisible);
        Assert.Equal("Note", view.ColumnBoxes[0].SelectedItem);
        Assert.Equal(3, view.Model.Columns);

        view.DirectionBox.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, view.Model.Columns);
        Assert.Equal("Name", view.ColumnBoxes[0].SelectedItem);

        view.DirectionBox.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Confirm(view).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var choice = Assert.IsType<SortLevelsChoice>(result);
        Assert.True(choice.LeftToRight);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Without_row_names_the_direction_option_is_hidden() => Session.Dispatch(() =>
    {
        var (view, window) = Show(new SortLevelsRequest("command.ledger.data.sort.title", ["A"], ["Column A"]));

        Assert.False(view.DirectionBox.IsVisible);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);
}
