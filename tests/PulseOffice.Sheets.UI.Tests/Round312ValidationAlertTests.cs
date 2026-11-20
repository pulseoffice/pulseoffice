using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round312ValidationAlertTests
{
    private static readonly TextCatalog Texts = new(UiLanguage.English);

    private readonly ValidationAlertContents _contents = new(Texts);

    private static DataValidation Rule(ValidationAlert alert, string? title = null, string? message = null) => new()
    {
        Range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
        Kind = ValidationKind.WholeNumber,
        Alert = alert,
        ErrorTitle = title,
        ErrorMessage = message,
    };

    [Fact]
    public void Stop_offers_retry_and_cancel_with_the_rule_title_and_message()
    {
        var content = _contents.For(Rule(ValidationAlert.Stop, "Quantity", "From 1 to 9"));

        Assert.Equal("Quantity", content.Title);
        Assert.Equal("From 1 to 9", content.Message);
        Assert.Equal(new[] { ValidationAlertAnswer.Retry, ValidationAlertAnswer.Cancel }, content.Choices.Select(choice => (ValidationAlertAnswer)choice.Result));
        Assert.True(content.Choices[^1].IsCancel);
    }

    [Fact]
    public void Warning_asks_to_continue_with_yes_no_and_cancel()
    {
        var content = _contents.For(Rule(ValidationAlert.Warning));

        Assert.Equal(Texts["ledger.validation.alert.continue"], content.Explanation);
        Assert.Equal(new[] { ValidationAlertAnswer.Accept, ValidationAlertAnswer.Retry, ValidationAlertAnswer.Cancel }, content.Choices.Select(choice => (ValidationAlertAnswer)choice.Result));
    }

    [Fact]
    public void Information_offers_ok_and_cancel_with_default_texts_when_the_rule_has_none()
    {
        var content = _contents.For(Rule(ValidationAlert.Information, "  ", null));

        Assert.Equal(Texts["ledger.validation.alert.defaultTitle"], content.Title);
        Assert.Equal(Texts["ledger.validation.alert.defaultMessage"], content.Message);
        Assert.Equal(new[] { ValidationAlertAnswer.Accept, ValidationAlertAnswer.Cancel }, content.Choices.Select(choice => (ValidationAlertAnswer)choice.Result));
    }
}
