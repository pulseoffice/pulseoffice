using System.Text.Json;
using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Settings;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell.Desktop;

public sealed record DesktopIntegrationPreferences(bool Declined, bool QuietRelocation);

public interface IDesktopIntegrationPreferencesStore
{
    DesktopIntegrationPreferences Load();

    void Save(DesktopIntegrationPreferences preferences);
}

public sealed class JsonDesktopIntegrationPreferencesStore(IUserDataPaths paths) : IDesktopIntegrationPreferencesStore
{
    private const string FileName = "desktop-integration.json";

    public DesktopIntegrationPreferences Load()
    {
        var path = paths.Configuration(FileName);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return new DesktopIntegrationPreferences(false, false);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new DesktopIntegrationPreferences(
                root.TryGetProperty("declined", out var declined) && declined.ValueKind == JsonValueKind.True,
                root.TryGetProperty("quietRelocation", out var quiet) && quiet.ValueKind == JsonValueKind.True);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new DesktopIntegrationPreferences(false, false);
        }
    }

    public void Save(DesktopIntegrationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var path = paths.Configuration(FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["declined"] = preferences.Declined,
            ["quietRelocation"] = preferences.QuietRelocation,
        }));
        File.Move(temporary, path, overwrite: true);
    }
}

public interface IDesktopIntegrationCoordinator
{
    bool IsAvailable { get; }

    bool IsIntegrated { get; }

    Task CheckAsync(CancellationToken ct = default);

    Task ToggleAsync(CancellationToken ct = default);
}

public sealed class DesktopIntegrationCoordinator(
    IDesktopIntegration integration,
    IDesktopIntegrationPreferencesStore preferences,
    IDialogService dialogs,
    INotificationCenter notifications,
    ITextCatalog texts,
    ILogger<DesktopIntegrationCoordinator> logger) : IDesktopIntegrationCoordinator
{
    public const string CommandId = "shell.help.desktopMenu";

    private enum Answer
    {
        MoveAndAdd,
        Add,
        Move,
        Keep,
        NotNow,
        Never,
    }

    public bool IsAvailable => integration.IsAvailable;

    public bool IsIntegrated => integration.IsAvailable && integration.IsIntegrated;

    public async Task CheckAsync(CancellationToken ct = default)
    {
        if (!integration.IsAvailable) return;
        var stored = preferences.Load();

        if (!integration.IsIntegrated)
        {
            if (!stored.Declined) await OfferIntegrationAsync(stored, ct).ConfigureAwait(true);
            return;
        }

        if (string.Equals(integration.RegisteredImage, integration.CurrentImage, StringComparison.Ordinal)) return;

        if (integration.IsInApplicationsFolder || stored.QuietRelocation)
        {
            Act(integration.RegisterCurrentLocation);
            return;
        }

        await OfferRelocationAsync(stored, ct).ConfigureAwait(true);
    }

    public Task ToggleAsync(CancellationToken ct = default)
    {
        if (!integration.IsAvailable) return Task.CompletedTask;
        if (integration.IsIntegrated)
        {
            if (Act(integration.Remove)) Tell("desktop.removed");
            return Task.CompletedTask;
        }

        preferences.Save(preferences.Load() with { Declined = false });
        return OfferIntegrationAsync(preferences.Load(), ct);
    }

    private async Task OfferIntegrationAsync(DesktopIntegrationPreferences stored, CancellationToken ct)
    {
        List<PromptChoice> choices = integration.IsInApplicationsFolder
            ? [new(texts["desktop.integrate.add"], Answer.Add, IsDefault: true)]
            :
            [
                new(texts.Format("desktop.integrate.moveAndAdd", integration.ApplicationsFolder), Answer.MoveAndAdd, IsDefault: true),
                new(texts["desktop.integrate.addHere"], Answer.Add),
            ];
        choices.Add(new PromptChoice(texts["desktop.integrate.notNow"], Answer.NotNow, IsCancel: true));
        choices.Add(new PromptChoice(texts["desktop.integrate.never"], Answer.Never));

        var answer = await AskAsync("desktop.integrate.question", "desktop.integrate.explanation", "desktop.integrate.title", choices, ct)
            .ConfigureAwait(true);

        switch (answer)
        {
            case Answer.MoveAndAdd:
                if (Act(() => integration.MoveToApplicationsFolder()) && Act(integration.Integrate)) Tell("desktop.integrated");
                break;
            case Answer.Add:
                if (Act(integration.Integrate)) Tell("desktop.integrated");
                break;
            case Answer.Never:
                preferences.Save(stored with { Declined = true });
                break;
        }
    }

    private async Task OfferRelocationAsync(DesktopIntegrationPreferences stored, CancellationToken ct)
    {
        var folder = Path.GetDirectoryName(integration.CurrentImage) ?? string.Empty;
        var answer = await AskAsync(
            texts.Format("desktop.relocate.question", folder, integration.ApplicationsFolder),
            "desktop.relocate.explanation",
            "desktop.relocate.title",
            [
                new PromptChoice(texts.Format("desktop.relocate.move", integration.ApplicationsFolder), Answer.Move, IsDefault: true),
                new PromptChoice(texts["desktop.relocate.keep"], Answer.Keep, IsCancel: true),
                new PromptChoice(texts["desktop.relocate.never"], Answer.Never),
            ],
            ct).ConfigureAwait(true);

        switch (answer)
        {
            case Answer.Move:
                Act(() => integration.MoveToApplicationsFolder());
                break;
            case Answer.Never:
                preferences.Save(stored with { QuietRelocation = true });
                Act(integration.RegisterCurrentLocation);
                break;
            default:
                Act(integration.RegisterCurrentLocation);
                break;
        }
    }

    private async Task<Answer?> AskAsync(string questionKeyOrText, string explanationKey, string titleKey, IReadOnlyList<PromptChoice> choices, CancellationToken ct)
    {
        var question = texts.Contains(questionKeyOrText) ? texts[questionKeyOrText] : questionKeyOrText;
        var result = await dialogs.ShowAsync<object>(
            new ChoicePromptView(question, texts[explanationKey], choices, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 520 },
            ct).ConfigureAwait(true);
        return result is Answer answer ? answer : null;
    }

    private bool Act(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Desktop integration failed");
            notifications.Post(new Notification(texts.Format("desktop.failed", ex.Message), NotificationSeverity.Warning));
            return false;
        }
    }

    private void Tell(string key) => notifications.Post(new Notification(texts[key], NotificationSeverity.Success));
}
