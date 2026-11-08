using PulseOffice.Testing;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class NumberBoxTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    [Fact]
    public void Format_hides_conversion_noise_below_the_step()
    {
        var entry = new NumberEntry(new NumericBounds(0, 300, 0.1), integer: false, Italian);

        Assert.Equal("21", entry.Format(21.000861));
        Assert.Equal("2", entry.Format(2.00025));
        Assert.Equal("1,25", entry.Format(1.25));
    }

    [Fact]
    public void EntryParsesInTheCultureClampsAndRounds()
    {
        var entry = new NumberEntry(new NumericBounds(0, 10, 0.5), integer: false, Italian);

        Assert.True(entry.TryParse(" 2,5 ", out var value));
        Assert.Equal(2.5, value);
        Assert.True(entry.TryParse("99", out value));
        Assert.Equal(10, value);
        Assert.False(entry.TryParse("abc", out _));
        Assert.False(entry.TryParse("NaN", out _));
        Assert.Equal(9.5, entry.Step(9, 1));
        Assert.Equal("2,5", entry.Format(2.5));
    }

    [Fact]
    public void IntegerEntryRoundsAndSteps()
    {
        var entry = new NumberEntry(new NumericBounds(1, 5, 1), integer: true, Italian);

        Assert.True(entry.TryParse("2,6", out var value));
        Assert.Equal(3, value);
        Assert.Equal(5, entry.Step(5, 1));
        Assert.Equal(1, entry.Normalize(-4));
    }

    [Fact]
    public Task KeyboardStepsAcceptsAndCancels() => Session.Dispatch(() =>
    {
        var box = new NumberBox(new NumberEntry(new NumericBounds(0, 100, 1), true, Italian), 10, DesignTokens.For(InterfaceDensity.Compact));
        var window = new Window { Content = box };
        window.Show();
        var text = box.GetVisualDescendants().OfType<TextBox>().Single();
        var committed = new List<double>();
        box.ValueCommitted += (_, value) => committed.Add(value);

        text.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
        Assert.Equal(11, box.Value);

        text.Text = "42";
        text.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Equal("11", text.Text);

        text.Text = "250";
        text.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(100, box.Value);

        text.Text = "7";
        Assert.Equal(7, box.Read());
        Assert.Equal([11, 100, 7], committed);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Invio_nella_casella_conferma_il_dialogo() => Session.Dispatch(() =>
    {
        var box = new NumberBox(new NumberEntry(new NumericBounds(0, 100, 1), true, Italian), 10, DesignTokens.For(InterfaceDensity.Compact));
        var ok = new Button { IsDefault = true, Content = "OK" };
        var pressed = 0;
        ok.Click += (_, _) => pressed++;
        var window = new Window { Content = new StackPanel { Children = { box, ok } } };
        window.Show();
        var text = box.GetVisualDescendants().OfType<TextBox>().Single();
        text.Focus();
        text.Text = "5";

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Equal(5, box.Value);
        Assert.Equal(1, pressed);
        window.Close();
    }, CancellationToken.None);
}
