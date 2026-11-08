using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Prompts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class TextPromptValidationTests
{
    private static (TextPromptView View, Window Window) Open(string initial, params ITextInputRule[] rules)
    {
        var view = new TextPromptView("Nome del foglio", initial, new TextCatalog(UiLanguage.Italian), rules: rules);
        var window = new Window { Content = view, Width = 420, Height = 240 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    [Fact]
    public Task Il_nome_gia_usato_si_segnala_sotto_il_campo_mentre_si_scrive() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (view, window) = Open("Foglio1",
            new RequiredTextRule("Scrivi un nome."),
            new ForbiddenCharactersRule(['/', ':'], found => $"Vietati: {found}"),
            new UniqueNameRule(name => name == "Vendite", name => $"{name} esiste già"));

        Assert.True(view.CanConfirm);
        Assert.Null(view.Message);

        view.Text = "Vendite";
        Assert.Equal("Vendite esiste già", view.Message);
        Assert.False(view.CanConfirm);

        view.Text = "Q1/Q2:";
        Assert.Equal("Vietati: / :", view.Message);

        view.Text = "  ";
        Assert.Equal("Scrivi un nome.", view.Message);

        object? result = "untouched";
        view.Completed += (_, value) => result = value;
        view.Confirm();
        Assert.Equal("untouched", result);

        view.Text = "Acquisti";
        view.Confirm();
        Assert.Equal("Acquisti", result);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Senza_regole_il_campo_resta_libero() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (view, window) = Open(string.Empty);

        Assert.True(view.CanConfirm);
        window.Close();
    }, CancellationToken.None);
}
