using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Prompts;

public enum OpenPgpKeyProblem { None, WrongPassphrase, NoMatchingKey, InvalidKeyFile }

public sealed record SecretKeyAnswer(string KeyFile, string Passphrase);

public abstract record ProtectionRequest;

public sealed record PasswordRequest(string? Password) : ProtectionRequest;

public sealed record RecipientsRequest(IReadOnlyList<string> KeyFiles) : ProtectionRequest;

public interface IDocumentPasswordPrompt
{
    Task<string?> AskAsync(string documentName, bool retry, CancellationToken ct);

    Task<SecretKeyAnswer?> AskSecretKeyAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct);

    Task<ProtectionRequest?> AskProtectionAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct);
}

public sealed class DocumentPasswordPrompt(IDialogService dialogs, IFileDialogService files, ITextCatalog texts) : IDocumentPasswordPrompt
{
    private const char PathSeparator = ';';
    private static readonly IReadOnlyList<string> KeyExtensions = ["asc", "gpg", "pgp", "key"];

    private enum Method { Password, Recipients }

    public async Task<string?> AskAsync(string documentName, bool retry, CancellationToken ct)
    {
        var answers = await AskFormAsync(
            texts.Format(retry ? "password.open.wrong" : "password.open.explanation", documentName),
            [new TextField("password", texts["password.field"]) { Secret = true }],
            "password.open.title", ct).ConfigureAwait(true);
        return answers?.GetValueOrDefault("password") as string;
    }

    public async Task<SecretKeyAnswer?> AskSecretKeyAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct)
    {
        var answers = await AskFormAsync(
            texts.Format(ProblemKey(problem, "openPgp.open.explanation"), documentName),
            [
                new PathField("key", texts["openPgp.secretKey"], string.Empty, BrowseOneAsync),
                new TextField("passphrase", texts["openPgp.passphrase"]) { Secret = true },
            ],
            "openPgp.open.title", ct).ConfigureAwait(true);
        if (answers is null) return null;
        return new SecretKeyAnswer(answers.GetValueOrDefault("key") as string ?? string.Empty, answers.GetValueOrDefault("passphrase") as string ?? string.Empty);
    }

    public async Task<ProtectionRequest?> AskProtectionAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct)
    {
        var method = problem == OpenPgpKeyProblem.None
            ? await dialogs.ShowAsync<object>(
                new ChoicePromptView(
                    texts.Format("encrypt.question", documentName),
                    texts["encrypt.explanation"],
                    [
                        new PromptChoice(texts["close.cancel"], false, IsCancel: true),
                        new PromptChoice(texts["encrypt.recipients"], Method.Recipients) { Description = texts["encrypt.recipients.description"], Icon = "users" },
                        new PromptChoice(texts["encrypt.password"], Method.Password, IsDefault: true) { Description = texts["encrypt.password.description"], Icon = "lock" },
                    ],
                    texts),
                new DialogOptions { TitleKey = "password.save.title", PreferredWidth = 480, CanCancelWithEscape = true },
                ct).ConfigureAwait(true)
            : Method.Recipients;

        return method switch
        {
            Method.Password => await AskNewPasswordAsync(documentName, mismatch: false, ct).ConfigureAwait(true),
            Method.Recipients => await AskRecipientsAsync(documentName, problem, ct).ConfigureAwait(true),
            _ => null,
        };
    }

    private async Task<ProtectionRequest?> AskNewPasswordAsync(string documentName, bool mismatch, CancellationToken ct)
    {
        var answers = await AskFormAsync(
            texts.Format(mismatch ? "password.save.mismatch" : "password.save.explanation", documentName),
            [
                new TextField("password", texts["password.field"]) { Secret = true },
                new TextField("confirm", texts["password.confirm"]) { Secret = true },
            ],
            "password.save.title", ct).ConfigureAwait(true);

        if (answers is null) return null;
        var password = answers.GetValueOrDefault("password") as string ?? string.Empty;
        var confirm = answers.GetValueOrDefault("confirm") as string ?? string.Empty;
        if (password != confirm) return await AskNewPasswordAsync(documentName, mismatch: true, ct).ConfigureAwait(true);
        return new PasswordRequest(password.Length == 0 ? null : password);
    }

    private async Task<ProtectionRequest?> AskRecipientsAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct)
    {
        var answers = await AskFormAsync(
            texts.Format(ProblemKey(problem, "openPgp.recipients.explanation"), documentName),
            [new PathField("keys", texts["openPgp.publicKeys"], string.Empty, BrowseManyAsync, texts["openPgp.publicKeys.placeholder"])],
            "openPgp.recipients.title", ct).ConfigureAwait(true);
        if (answers is null) return null;
        var paths = (answers.GetValueOrDefault("keys") as string ?? string.Empty)
            .Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new RecipientsRequest(paths);
    }

    private string ProblemKey(OpenPgpKeyProblem problem, string fallback) => problem switch
    {
        OpenPgpKeyProblem.WrongPassphrase => "openPgp.problem.passphrase",
        OpenPgpKeyProblem.NoMatchingKey => "openPgp.problem.noKey",
        OpenPgpKeyProblem.InvalidKeyFile => "openPgp.problem.invalid",
        _ => fallback,
    };

    private Task<Dictionary<string, object>?> AskFormAsync(string explanation, IReadOnlyList<FormField> fields, string titleKey, CancellationToken ct) =>
        dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(explanation, fields, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 480 },
            ct);

    private async Task<string?> BrowseOneAsync(CancellationToken ct)
    {
        var chosen = await files.OpenFileAsync(texts["openPgp.secretKey"], [KeyFilter()], ct).ConfigureAwait(true);
        if (chosen is null) return null;
        await using (chosen) return chosen.Path;
    }

    private async Task<string?> BrowseManyAsync(CancellationToken ct)
    {
        var chosen = await files.OpenFilesAsync(texts["openPgp.publicKeys"], [KeyFilter()], ct).ConfigureAwait(true);
        var paths = chosen.Select(file => file.Path).ToList();
        foreach (var file in chosen) await file.DisposeAsync().ConfigureAwait(true);
        return paths.Count == 0 ? null : string.Join(PathSeparator, paths);
    }

    private FileFilter KeyFilter() => new(texts["openPgp.keyFiles"], KeyExtensions);
}
