using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Security;
using PulseOffice.Sheets.UI;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellSignatureCommands(
    IDocumentWorkspace workspace,
    IDocumentOperationsCatalog operations,
    IMacroSigner macroSigner,
    IDocumentSignatureService signatures,
    ICertificateSource certificates,
    IPdfSigner pdf,
    IFileDialogService files,
    ISignedDocumentTracker tracker,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.signatures"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            Execute = (_, ct) => SignaturesAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.tools.macroSignature"),
            MenuPath = "tools/macros",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Holder() is { SignableMacros.Count: > 0 } || Holder()?.MacroSignature is not null,
            Execute = (_, ct) => MacroSignatureAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.signPdf"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => SignPdfAsync(ct),
        },
    ];

    private async Task SignaturesAsync(CancellationToken ct)
    {
        if (workspace.Active is not { Path: { Length: > 0 } path, IsModified: false } || !signatures.CanSign(path))
        {
            status.Report(texts["signature.saveFirst"], NotificationSeverity.Warning, NotificationRemedies.Save);
            return;
        }

        var existing = signatures.Read(path);
        var installed = new Lazy<IReadOnlyList<X509Certificate2>>(certificates.Personal);

        var request = await dialogs.ShowAsync<PulseOffice.UI.Controls.Prompts.SignatureRequest>(
            new PulseOffice.UI.Controls.Prompts.SignaturesView(
                [.. existing.Select(signature => new PulseOffice.UI.Controls.Prompts.SignatureRow(
                    signature.Signer.Name, signature.Signer.Issuer, signature.SignedOn, signature.Signer.NotAfter,
                    Health(signature.Status), texts["signature.status." + Lower(signature.Status.ToString())]))],
                () => [.. installed.Value.Select(certificate => SignerIdentity.From(certificate).Name)],
                async () => (await files.OpenFileAsync(texts["signature.fromFile.choose"], [new FileFilter("PKCS #12", ["*.pfx", "*.p12"])], ct).ConfigureAwait(true))?.Path,
                texts),
            new DialogOptions { TitleKey = "command.shell.file.signatures.title", PreferredWidth = 600 },
            ct).ConfigureAwait(true);

        if (request is null) return;

        if (request.Kind == PulseOffice.UI.Controls.Prompts.SignatureRequestKind.RemoveAll)
        {
            foreach (var signature in existing) signatures.Remove(path, signature.Signer.Thumbprint);

            Refresh(path);
            status.Report(texts["signature.removed"]);
            return;
        }

        if (Chosen(request, installed) is not { } chosen)
        {
            status.Report(texts["signature.noCertificate"], NotificationSeverity.Error);
            return;
        }

        try
        {
            signatures.Sign(path, chosen);
            Refresh(path);
            status.Report(texts["signature.added"], NotificationSeverity.Success);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            status.Report(texts["signature.failed"], NotificationSeverity.Error);
        }
    }

    private void Refresh(string path)
    {
        if (string.Equals(tracker.Path, path, StringComparison.Ordinal)) tracker.Refresh();
    }

    private X509Certificate2? Chosen(PulseOffice.UI.Controls.Prompts.SignatureRequest request, Lazy<IReadOnlyList<X509Certificate2>> installed)
    {
        if (request.Certificate >= 0 && installed.IsValueCreated && request.Certificate < installed.Value.Count) return installed.Value[request.Certificate];

        return request.File.Length > 0 ? certificates.FromFile(request.File, request.Password) : null;
    }

    private PulseOffice.UI.Controls.Prompts.SignatureHealth Health(SignatureStatus status) => status switch
    {
        SignatureStatus.Valid => PulseOffice.UI.Controls.Prompts.SignatureHealth.Valid,
        SignatureStatus.Broken => PulseOffice.UI.Controls.Prompts.SignatureHealth.Broken,
        _ => PulseOffice.UI.Controls.Prompts.SignatureHealth.Warning,
    };

    private async Task SignPdfAsync(CancellationToken ct)
    {
        var chosen = await files.OpenFileAsync(
            texts["signPdf.choose"], [new FileFilter("PDF", ["*.pdf"])], ct).ConfigureAwait(true);

        if (chosen is null) return;

        if (!pdf.IsPdf(chosen.Path))
        {
            status.Report(texts["signPdf.notPdf"], NotificationSeverity.Error);
            return;
        }

        var available = certificates.Personal();

        if (available.Count == 0)
        {
            status.Report(texts["signature.noCertificate"], NotificationSeverity.Error);
            return;
        }

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                Describe(pdf.Read(chosen.Path)),
                [
                    new ChoiceField(
                        "certificate",
                        texts["signature.certificate"],
                        [.. available.Select(certificate => SignerIdentity.From(certificate).Name)]),
                    new TextField("reason", texts["signPdf.reason"], string.Empty),
                    new TextField("place", texts["signPdf.place"], string.Empty),
                    new CheckField("visible", texts["signPdf.visible"], false),
                ],
                texts),
            new DialogOptions { TitleKey = "command.shell.file.signPdf.title", PreferredWidth = 520 },
            ct).ConfigureAwait(true);

        if (values is null) return;

        var certificate = available[(int)Convert.ToInt64(values["certificate"], CultureInfo.InvariantCulture)];

        try
        {
            var appearance = values.TryGetValue("visible", out var visible) && visible is true
                ? new PdfSignatureAppearance(texts["signPdf.signedBy"], texts["signPdf.date"], texts["signPdf.reason"], texts["signPdf.place"])
                : null;
            pdf.Sign(chosen.Path, certificate, values["reason"] as string, values["place"] as string, appearance);
            status.Report(texts.Format("signPdf.done", chosen.Name), NotificationSeverity.Success);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            status.Report(texts["signature.failed"], NotificationSeverity.Error);
        }
    }

    private IMacroSignatureHolder? Holder() =>
        workspace.Active is { } document ? operations.For(document)?.MacroSigning(document) : null;

    private async Task MacroSignatureAsync(CancellationToken ct)
    {
        if (workspace.Active is not { } document || Holder() is not { } holder) return;

        var macros = holder.SignableMacros;
        var available = certificates.Personal();
        var current = holder.MacroSignature;

        var actions = new List<string> { texts["signature.keep"] };
        if (available.Count > 0 && macros.Count > 0) actions.Add(texts["signature.macro.sign"]);
        if (current is not null) actions.Add(texts["signature.macro.remove"]);

        List<FormField> fields = [new ChoiceField("action", texts["signature.action"], actions, Math.Min(1, actions.Count - 1))];
        if (available.Count > 0 && macros.Count > 0)
            fields.Add(new ChoiceField("certificate", texts["signature.certificate"],
                [.. available.Select(certificate => SignerIdentity.From(certificate).Name)]));

        var explanation = texts.Format("signature.macroExplanation", macros.Count) + "\n\n" + DescribeMacros(macros, current)
            + (available.Count == 0 ? "\n" + texts["signature.noCertificate"] : string.Empty);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(explanation, fields, texts),
            new DialogOptions { TitleKey = "command.shell.tools.macroSignature.title", PreferredWidth = 520 },
            ct).ConfigureAwait(true);
        if (values is null) return;

        var chosen = actions[values["action"] is int a ? a : 0];
        MacroSignature? next;
        if (chosen == texts["signature.macro.sign"])
        {
            var certificate = available[values.TryGetValue("certificate", out var c) && c is int i ? i : 0];
            try
            {
                next = macroSigner.Sign(macros, certificate);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Security.Cryptography.CryptographicException)
            {
                status.Report(texts["signature.failed"], NotificationSeverity.Error);
                return;
            }
        }
        else if (chosen == texts["signature.macro.remove"]) next = null;
        else return;

        holder.MacroSignature = next;
        document.History.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["command.shell.tools.macroSignature.title"],
            () => holder.MacroSignature = next,
            () => holder.MacroSignature = current));

        status.Report(next is null ? texts["signature.macro.removed"] : texts.Format("signature.macroSigned", next.Signer.Name),
            next is null ? NotificationSeverity.Info : NotificationSeverity.Success);
    }

    private string DescribeMacros(IReadOnlyList<(string Name, string Language, string Source)> macros, MacroSignature? signature) =>
        signature is null
            ? texts["signature.macro.none"]
            : string.Format(CultureInfo.CurrentCulture, "{0} — {1:g} — {2}",
                signature.Signer.Name, signature.SignedOn,
                texts["signature.status." + Lower(macroSigner.Verify(macros, signature).ToString())]);

    private string Describe(IReadOnlyList<DocumentSignature> existing) =>
        existing.Count == 0
            ? texts["signature.none"]
            : string.Join('\n', existing.Select(signature => string.Format(
                CultureInfo.CurrentCulture,
                "{0} — {1:g} — {2}",
                signature.Signer.Name,
                signature.SignedOn,
                texts["signature.status." + Lower(signature.Status.ToString())])));

    private static string Lower(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
