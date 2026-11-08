using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ChoicePromptCardsTests
{
    private static readonly TextCatalog Texts = new(UiLanguage.English);

    private static ChoicePromptView Protection() => new(
        "How?", "Pick one.",
        [
            new PromptChoice("Cancel", "cancel", IsCancel: true),
            new PromptChoice("Recipients", "recipients") { Description = "Only key holders." },
            new PromptChoice("Password", "password", IsDefault: true) { Description = "Anyone with the password." },
        ],
        Texts);

    [Fact]
    public Task LeSceltePrincipaliDiventanoSchedeEAnnullaRestaNelPiede() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = Protection();
        Assert.Equal(["Recipients", "Password"], view.Cards.Select(card => card.Choice.Label));
        Assert.NotNull(view.Footer);
        Assert.DoesNotContain(view.Footer!.GetLogicalDescendantsOfButtons(), label => label == "Recipients");
        Assert.Contains(view.Cards[1].GetVisualOrLogicalTexts(), text => text == "Anyone with the password.");
        Assert.True(view.Cards[1].IsDefault);
    }, CancellationToken.None);

    [Fact]
    public Task LaPredefinitaHaIlFuocoELeFrecceSpostanoLaScelta() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = Protection();
        object? result = "none";
        view.Completed += (_, value) => result = value;
        var window = new Window { Width = 480, Height = 400, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.Cards[1].IsFocused);
        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.Cards[0].IsFocused);

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("recipients", result);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task UnaSolaSceltaSenzaDescrizioneRestaUnPulsanteNelPiede() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = new ChoicePromptView("Sure?", string.Empty,
            [new PromptChoice("No", "no", IsCancel: true), new PromptChoice("Yes", "yes", IsDefault: true)], Texts);
        Assert.Empty(view.Cards);
        Assert.Contains(view.Footer!.GetLogicalDescendantsOfButtons(), label => label == "Yes");
    }, CancellationToken.None);
}

internal static class ChoicePromptProbe
{
    public static IEnumerable<string> GetLogicalDescendantsOfButtons(this Control root) =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<Button>().Select(button => button.Content?.ToString() ?? string.Empty);

    public static IEnumerable<string> GetVisualOrLogicalTexts(this Control root) =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<TextBlock>().Select(text => text.Text ?? string.Empty);
}
