using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditSharedViewTests
{
    private sealed class CountingTexts : ITextCatalog
    {
        private readonly TextCatalog _inner = new(UiLanguage.English);
        private EventHandler? _changed;

        public int Listeners => _changed?.GetInvocationList().Length ?? 0;

        public UiLanguage Language => _inner.Language;

        public string this[string key] => _inner[key];

        public string Format(string key, params object?[] arguments) => _inner.Format(key, arguments);

        public bool Contains(string key) => _inner.Contains(key);

        public void Use(UiLanguage language) => _inner.Use(language);

        public event EventHandler? LanguageChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }
    }

    private sealed class NoRunner : INotificationActionRunner
    {
        public void Run(NotificationAction action)
        {
        }
    }

    [Fact]
    public Task IlPulsanteDelloStoricoSiSgancia() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var texts = new CountingTexts();
        var button = new NotificationHistoryButton(new NotificationHistory(TimeProvider.System), new IconCatalog(), texts, new NoRunner());
        var window = new Window { Content = button };
        window.Show();
        var attached = texts.Listeners;

        window.Content = null;
        window.Close();

        Assert.Equal(1, attached);
        Assert.Equal(0, texts.Listeners);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task SfogliaCheFallisceNonFaCadereLeFirme() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = new PulseOffice.UI.Controls.Prompts.SignaturesView(
            [], () => [], () => Task.FromException<string?>(new InvalidOperationException("picker")), new TextCatalog(UiLanguage.English));
        var window = new Window { Content = view };
        window.Show();
        var browse = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view).OfType<Button>()
            .Single(button => Equals(button.Content, new TextCatalog(UiLanguage.English)["signature.fromFile.browse"]));

        browse.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(browse.IsEnabled);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);
}
