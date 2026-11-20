using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public enum ValidationAlertAnswer { Cancel, Retry, Accept }

public interface IValidationAlertPrompt
{
    Task<ValidationAlertAnswer> AskAsync(DataValidation rule, CancellationToken ct);
}

public sealed record ValidationAlertContent(string Title, string Message, string Explanation, IReadOnlyList<PromptChoice> Choices);

public interface IValidationAlertContents
{
    ValidationAlertContent For(DataValidation rule);
}

public sealed class ValidationAlertContents(ITextCatalog texts) : IValidationAlertContents
{
    public ValidationAlertContent For(DataValidation rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var title = rule.ErrorTitle?.Trim() is { Length: > 0 } custom ? custom : texts["ledger.validation.alert.defaultTitle"];
        var message = rule.ErrorMessage?.Trim() is { Length: > 0 } text ? text : texts["ledger.validation.alert.defaultMessage"];
        var cancel = new PromptChoice(texts["prompt.cancel"], ValidationAlertAnswer.Cancel, IsCancel: true);

        return rule.Alert switch
        {
            ValidationAlert.Warning => new ValidationAlertContent(title, message, texts["ledger.validation.alert.continue"],
            [
                new PromptChoice(texts["ledger.validation.alert.yes"], ValidationAlertAnswer.Accept),
                new PromptChoice(texts["ledger.validation.alert.no"], ValidationAlertAnswer.Retry, IsDefault: true),
                cancel,
            ]),
            ValidationAlert.Information => new ValidationAlertContent(title, message, string.Empty,
            [
                new PromptChoice(texts["ledger.validation.alert.ok"], ValidationAlertAnswer.Accept, IsDefault: true),
                cancel,
            ]),
            _ => new ValidationAlertContent(title, message, string.Empty,
            [
                new PromptChoice(texts["ledger.validation.alert.retry"], ValidationAlertAnswer.Retry, IsDefault: true),
                cancel,
            ]),
        };
    }
}

public sealed class ValidationAlertPrompt(IDialogService dialogs, ITextCatalog texts, IValidationAlertContents contents) : IValidationAlertPrompt
{
    public async Task<ValidationAlertAnswer> AskAsync(DataValidation rule, CancellationToken ct)
    {
        var content = contents.For(rule);
        var answer = await dialogs.ShowAsync<object>(
            new ChoicePromptView(content.Message, content.Explanation, content.Choices, texts),
            new DialogOptions { TitleKey = "dialog.validationAlert.title", TitleArguments = [content.Title], PreferredWidth = 440 },
            ct).ConfigureAwait(true);

        return answer is ValidationAlertAnswer chosen ? chosen : ValidationAlertAnswer.Cancel;
    }
}
