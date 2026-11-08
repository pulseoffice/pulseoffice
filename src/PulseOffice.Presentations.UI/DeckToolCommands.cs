using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Text.Scripts;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Prompts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckToolCommands(
    IActiveDeckContext context,
    IRichTextFormatter formatter,
    IDeckTextRedaction redaction,
    ITextConversionPrompt conversion,
    IRedactionPrompt redactionPrompt,
    IStatusBarService status,
    ITextCatalog texts) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Tool("deck.tools.chineseConversion", _ => true,
            ct => ConvertAsync(ct, TextConversionKind.SimplifiedToTraditional, TextConversionKind.TraditionalToSimplified)),
        Tool("deck.tools.hangulHanja", _ => true,
            ct => ConvertAsync(ct, TextConversionKind.HangulToHanja, TextConversionKind.HanjaToHangul)),
        Tool("deck.tools.redact", t => t.Selected() is TextElement { Text.IsEmpty: false }, RedactAsync),
        Tool("deck.tools.autoRedact", _ => true, AutoRedactAsync),
    ];

    private CommandDefinition Tool(string id, Func<DeckTarget, bool> enabled, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && enabled(t),
            Execute = (_, ct) => run(ct),
        };

    private async Task ConvertAsync(CancellationToken ct, TextConversionKind forward, TextConversionKind backward)
    {
        if (await conversion.AskAsync(forward, backward, ct).ConfigureAwait(true) is not { } choice) return;
        if (Context.Target is not { } t) return;

        if (t.HasTextTarget())
        {
            t.ApplyText((text, range) => formatter.Transform(text, range, choice.Convert));
            status.Report(texts.Format("conversion.done", choice.Label));
            return;
        }

        var changed = t.Actions.RewriteTexts("conversion.title", text => formatter.Transform(text, null, choice.Convert));
        status.Report(changed > 0 ? texts.Format("conversion.done", choice.Label) : texts["conversion.nothing"],
            changed > 0 ? NotificationSeverity.Info : NotificationSeverity.Warning);
    }

    private async Task RedactAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.Selected() is not TextElement element) return;
        if (!await redactionPrompt.ConfirmAsync(texts["redact.deck.question"],
                "command.deck.tools.redact.title", ct).ConfigureAwait(true)) return;
        if (t.CurrentSlide() is not { } slide || slide.Find(element.Id) is not TextElement current) return;

        t.Actions.EditText(slide, current.Id, redaction.MaskAll(current.Text));
        status.Report(texts.Format("redact.deck.done", 1), NotificationSeverity.Success);
    }

    private async Task AutoRedactAsync(CancellationToken ct)
    {
        if (await redactionPrompt.ChoosePatternsAsync("command.deck.tools.autoRedact.title", "redact.deck.auto.explanation", ct).ConfigureAwait(true) is not { } patterns) return;
        if (Context.Target is not { } t) return;

        var changed = t.Actions.RewriteTexts("command.deck.tools.autoRedact.title",
            text => redaction.Redact(text, patterns),
            notes => redaction.RedactNotes(notes, patterns));

        status.Report(changed > 0 ? texts.Format("redact.deck.done", changed) : texts["redact.nothing"],
            changed > 0 ? NotificationSeverity.Success : NotificationSeverity.Warning);
    }
}
