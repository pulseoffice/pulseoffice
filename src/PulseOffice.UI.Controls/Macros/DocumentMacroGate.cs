using System.Runtime.CompilerServices;
using PulseOffice.Localization;
using PulseOffice.Security;
using PulseOffice.Settings;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Macros;

public enum MacroTrustDecision
{
    Run,
    AskOncePerDocument,
    AskPerMacro,
    Block,
}

public interface IMacroTrustPolicy
{
    MacroTrustDecision Decide(string level, SignatureStatus signature);
}

public sealed class MacroTrustPolicy : IMacroTrustPolicy
{
    public const string SettingKey = "macro.securityLevel";

    public MacroTrustDecision Decide(string level, SignatureStatus signature)
    {
        if (signature == SignatureStatus.Valid && level != "low") return MacroTrustDecision.Run;
        if (signature == SignatureStatus.Broken) return MacroTrustDecision.Block;

        return level switch
        {
            "low" => MacroTrustDecision.Run,
            "medium" => MacroTrustDecision.AskOncePerDocument,
            "veryHigh" => MacroTrustDecision.Block,
            _ => MacroTrustDecision.AskPerMacro,
        };
    }
}

public interface IMacroConsentPrompt
{
    Task<bool> AskAsync(string macroName, SignatureStatus signature, CancellationToken ct);
}

public sealed class MacroConsentPrompt(IDialogService dialogs, ITextCatalog texts) : IMacroConsentPrompt
{
    public async Task<bool> AskAsync(string macroName, SignatureStatus signature, CancellationToken ct)
    {
        var answer = await dialogs.ShowAsync<object>(
            new ChoicePromptView(
                texts.Format("macro.consent.question", macroName),
                texts[signature == SignatureStatus.Unknown ? "macro.consent.unsigned" : "macro.consent.untrusted"],
                [
                    new PromptChoice(texts["macro.consent.run"], true),
                    new PromptChoice(texts["macro.consent.block"], false, IsDefault: true, IsCancel: true),
                ],
                texts),
            new DialogOptions { TitleKey = "macro.consent.title", PreferredWidth = 480 },
            ct).ConfigureAwait(true);

        return answer is true;
    }
}

public interface IDocumentMacroGate
{
    Task<bool> AllowAsync(IMacroSignatureHolder document, string macroName, CancellationToken ct = default);
}

public sealed class DocumentMacroGate(
    ISettingsService settings,
    IMacroSigner signer,
    IMacroTrustPolicy policy,
    IMacroConsentPrompt prompt,
    IStatusBarService status,
    ITextCatalog texts) : IDocumentMacroGate
{
    private readonly ConditionalWeakTable<IMacroSignatureHolder, Consent> _consents = [];

    public async Task<bool> AllowAsync(IMacroSignatureHolder document, string macroName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(macroName);

        var signature = signer.Verify(document.SignableMacros, document.MacroSignature);
        var level = settings.Get<string>(MacroTrustPolicy.SettingKey) ?? "high";
        var consent = _consents.GetValue(document, _ => new Consent());

        switch (policy.Decide(level, signature))
        {
            case MacroTrustDecision.Run:
                return true;
            case MacroTrustDecision.Block:
                status.Report(texts.Format("macro.consent.blocked", macroName), NotificationSeverity.Warning);
                return false;
        }

        var perDocument = policy.Decide(level, signature) == MacroTrustDecision.AskOncePerDocument;
        if (consent.Refused || (!perDocument && consent.RefusedMacros.Contains(macroName))) return false;
        if (perDocument ? consent.Document : consent.Macros.Contains(macroName)) return true;

        var allowed = await prompt.AskAsync(macroName, signature, ct).ConfigureAwait(true);
        if (perDocument)
        {
            consent.Document = allowed;
            consent.Refused = !allowed;
        }
        else if (allowed) consent.Macros.Add(macroName);
        else consent.RefusedMacros.Add(macroName);

        if (!allowed) status.Report(texts.Format("macro.consent.blocked", macroName), NotificationSeverity.Warning);
        return allowed;
    }

    private sealed class Consent
    {
        public bool Document { get; set; }

        public bool Refused { get; set; }

        public HashSet<string> Macros { get; } = new(StringComparer.Ordinal);

        public HashSet<string> RefusedMacros { get; } = new(StringComparer.Ordinal);
    }
}
