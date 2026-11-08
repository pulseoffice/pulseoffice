using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Text.Redaction;
using PulseOffice.Text.Scripts;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Prompts;

public sealed record TextConversionChoice(string Label, Func<string, string> Convert);

public interface ITextConversionPrompt
{
    Task<TextConversionChoice?> AskAsync(TextConversionKind forward, TextConversionKind backward, CancellationToken ct);
}

public sealed class TextConversionPrompt(
    ITextConversionTables tables,
    ITextConverter converter,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts) : ITextConversionPrompt
{
    public async Task<TextConversionChoice?> AskAsync(
        TextConversionKind forward, TextConversionKind backward, CancellationToken ct)
    {
        if (tables.For(forward) is null && tables.For(backward) is null)
        {
            ReportMissing();
            return null;
        }

        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["conversion.explanation"],
                [
                    new ChoiceField("direction", texts["conversion.direction"],
                        [texts[$"conversion.kind.{forward}"], texts[$"conversion.kind.{backward}"]]),
                ],
                texts),
            new DialogOptions { TitleKey = "conversion.title", PreferredWidth = 460 },
            ct).ConfigureAwait(true);

        if (answers is null) return null;

        var chosen = Convert.ToInt64(answers["direction"], CultureInfo.InvariantCulture) == 0 ? forward : backward;

        if (tables.For(chosen) is not { } table)
        {
            ReportMissing();
            return null;
        }

        return new TextConversionChoice(texts[$"conversion.kind.{chosen}"], text => converter.Convert(text, table));
    }

    private void ReportMissing() =>
        status.Report(texts.Format("conversion.missingTable",
            tables is FolderTextConversionTables folder ? folder.Folder : string.Empty), NotificationSeverity.Warning);
}

public interface IRedactionPrompt
{
    Task<bool> ConfirmAsync(string question, string titleKey, CancellationToken ct);

    Task<IReadOnlyList<RedactionPattern>?> ChoosePatternsAsync(string titleKey, string explanationKey, CancellationToken ct);
}

public sealed class RedactionPrompt(
    ITextRedactor redactor,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts) : IRedactionPrompt
{
    public async Task<bool> ConfirmAsync(string question, string titleKey, CancellationToken ct)
    {
        var confirmed = await dialogs.ShowAsync<object>(
            new ChoicePromptView(
                question,
                texts["redact.explanation"],
                [
                    new PromptChoice(texts["redact.confirm"], true, IsDefault: true),
                    new PromptChoice(texts["prompt.cancel"], false, IsCancel: true),
                ],
                texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 460 },
            ct).ConfigureAwait(true);

        return confirmed is true;
    }

    public async Task<IReadOnlyList<RedactionPattern>?> ChoosePatternsAsync(string titleKey, string explanationKey, CancellationToken ct)
    {
        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts[explanationKey],
                [.. redactor.Patterns.Select(pattern =>
                    (FormField)new CheckField(pattern.Name, texts[$"redact.pattern.{pattern.Name}"], true))],
                texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 460 },
            ct).ConfigureAwait(true);

        if (answers is null) return null;

        var chosen = redactor.Patterns.Where(pattern => answers[pattern.Name] is true).ToList();
        if (chosen.Count == 0)
        {
            status.Report(texts["redact.noPattern"], NotificationSeverity.Warning);
            return null;
        }

        return chosen;
    }
}
