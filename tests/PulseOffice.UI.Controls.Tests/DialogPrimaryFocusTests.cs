using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DialogPrimaryFocusTests
{
    [Fact]
    public Task Il_pulsante_principale_dei_dialoghi_mostra_il_fuoco_da_tastiera() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var quiet = new Button { Content = "Annulla", Classes = { "dialogQuiet" } };
        var primary = new Button { Content = "Va bene", Classes = { "dialogPrimary" } };
        var window = new Window { Width = 400, Height = 200, Content = new StackPanel { Children = { quiet, primary } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var presenter = primary.GetVisualDescendants().OfType<ContentPresenter>().First();
        Assert.Equal(new Thickness(0), presenter.BorderThickness);

        quiet.Focus(NavigationMethod.Tab);
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(primary.IsFocused);
        Assert.Equal(new Thickness(2), presenter.BorderThickness);
        window.Close();
    }, CancellationToken.None);
}
