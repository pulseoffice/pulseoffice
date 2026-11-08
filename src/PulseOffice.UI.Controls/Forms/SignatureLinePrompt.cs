using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Forms;

public interface ISignatureLinePrompt
{
    Task<SignatureLineObject?> AskAsync(string titleKey, CancellationToken ct);
}

public sealed class SignatureLinePrompt(IDialogService dialogs, ITextCatalog texts) : ISignatureLinePrompt
{
    public async Task<SignatureLineObject?> AskAsync(string titleKey, CancellationToken ct)
    {
        var form = new FormPromptView(
            texts["signatureLine.explanation"],
            [
                new TextField("name", texts["signatureLine.name"], string.Empty),
                new TextField("title", texts["signatureLine.title"], string.Empty),
                new TextField("instructions", texts["signatureLine.instructions"], string.Empty),
                new CheckField("showDate", texts["signatureLine.showDate"], true),
                new CheckField("comments", texts["signatureLine.allowComments"], true),
            ],
            texts);
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = titleKey, PreferredWidth = 480 }, ct).ConfigureAwait(true);
        if (values is null) return null;
        return new SignatureLineObject
        {
            Id = Guid.NewGuid().ToString("N"),
            NaturalSize = SignatureLineObject.StandardSize,
            SignerName = Text(values, "name"),
            SignerTitle = Text(values, "title"),
            Instructions = Text(values, "instructions"),
            ShowDate = values["showDate"] is true,
            AllowComments = values["comments"] is true,
        };
    }

    private string? Text(Dictionary<string, object> values, string key) =>
        values.TryGetValue(key, out var value) && value as string is { Length: > 0 } text ? text.Trim() : null;
}
