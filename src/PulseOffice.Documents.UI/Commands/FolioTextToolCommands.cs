using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Redaction;
using PulseOffice.Text.Scripts;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Prompts;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioTextToolCommands(
    IActiveFolioContext context,
    ITextConversionPrompt conversion,
    IRedactionPrompt redaction,
    ITextRedactor redactor,
    PulseOffice.Text.Spelling.IHyphenationProvider hyphenation,
    IFolioLanguages languages,
    IFolioForms forms,
    IStatusBarService status,
    ITextCatalog texts,
    IMetadataRedaction metadata,
    IDirectDocumentEditor direct) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.tools.chineseConversion") with
        {
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => ConvertAsync(TextConversionKind.SimplifiedToTraditional, TextConversionKind.TraditionalToSimplified, ct),
        },
        Editing("folio.tools.hangulHanja") with
        {
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => ConvertAsync(TextConversionKind.HangulToHanja, TextConversionKind.HanjaToHangul, ct),
        },
        Editing("folio.tools.redact") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { HasSelection: true } editor && CanRedact(editor),
            Execute = (_, ct) => RedactAsync(ct),
        },
        Editing("folio.tools.autoRedact") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && CanRedact(editor),
            Execute = (_, ct) => AutoRedactAsync(ct),
        },
        Editing("folio.tools.hyphenate") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => HyphenateAsync(ct) },
    ];

    private bool CanRedact(FolioEditor editor) => !editor.ReadOnly() && !editor.Services.Guard.ForcesRecording(editor.Document);

    private async Task HyphenateAsync(CancellationToken ct)
    {
        if (Editor is null) return;
        if (await forms.AskAsync("command.folio.tools.hyphenate.title", "folio.hyphenate.explanation",
                [new PulseOffice.UI.Controls.NumberField("length", texts["folio.hyphenate.length"], 7, 4, 30) { Integer = true }], ct).ConfigureAwait(true) is not { } values
            || Editor is not { } editor) return;
        var minimum = (int)values.Number("length", 7);
        var missing = false;
        var range = editor.HasSelection ? editor.Selection : null as DocumentRange?;
        var paragraphs = range is { } selected
            ? editor.Services.Order.Between(editor.Document, selected.Start, selected.End)
            : editor.Services.Order.StoryOf(editor.Document, editor.Caret.ParagraphId);
        var count = 0;
        editor.Change(() =>
        {
            using (editor.History.BeginTransaction("hyphenate"))
                foreach (var paragraph in paragraphs.ToList())
                {
                    var language = PulseOffice.Text.Spelling.LanguageTag.Parse(languages.At(editor.Document, paragraph.Content, 0));
                    if (hyphenation.For(language) is not { } hyphenator)
                    {
                        missing = true;
                        continue;
                    }
                    var from = range is { } r && paragraph.Id == r.Start.ParagraphId ? r.Start.Offset : 0;
                    var to = range is { } s && paragraph.Id == s.End.ParagraphId ? s.End.Offset : paragraph.Length;
                    foreach (var at in Breaks(paragraph.Text, from, to, minimum, hyphenator).OrderByDescending(offset => offset))
                    {
                        editor.Services.Editor.InsertText(editor.Document, editor.History, new DocumentPosition(paragraph.Id, at), "\u00AD");
                        count++;
                    }
                }
        });
        status.Report(count > 0 ? texts.Format("folio.hyphenate.done", count) : texts[missing ? "hyphenation.missing" : "folio.hyphenate.nothing"],
            count > 0 ? NotificationSeverity.Success : NotificationSeverity.Warning);
    }

    private IEnumerable<int> Breaks(string text, int from, int to, int minimum, PulseOffice.Text.Spelling.IHyphenator hyphenator)
    {
        var start = -1;
        for (var index = from; index <= to; index++)
        {
            var letter = index < to && char.IsLetter(text[index]);
            if (letter && start < 0) start = index;
            if (letter || start < 0) continue;
            var word = text[start..index];
            if (word.Length >= minimum && !word.Contains('\u00AD'))
                foreach (var position in hyphenator.Positions(word)) yield return start + position;
            start = -1;
        }
    }

    private async Task ConvertAsync(TextConversionKind forward, TextConversionKind backward, CancellationToken ct)
    {
        if (await conversion.AskAsync(forward, backward, ct).ConfigureAwait(true) is not { } choice || Editor is not { } editor) return;
        var changed = Rewrite(editor, editor.Services.Editor, choice.Convert, "conversion");
        status.Report(changed > 0 ? texts.Format("conversion.done", choice.Label) : texts["conversion.nothing"],
            changed > 0 ? NotificationSeverity.Info : NotificationSeverity.Warning);
    }

    private async Task RedactAsync(CancellationToken ct)
    {
        if (!await redaction.ConfirmAsync(texts["redact.folio.question"], "command.folio.tools.redact.title", ct).ConfigureAwait(true)) return;
        if (Editor is not { HasSelection: true } editor || !CanRedact(editor)) return;
        var count = Rewrite(editor, direct, text => new string('■', text.Length), "redact");
        status.Report(texts.Format("redact.folio.done", count), NotificationSeverity.Success);
    }

    private async Task AutoRedactAsync(CancellationToken ct)
    {
        if (await redaction.ChoosePatternsAsync("command.folio.tools.autoRedact.title", "redact.folio.auto.explanation", ct).ConfigureAwait(true) is not { } patterns
            || Editor is not { } editor || !CanRedact(editor)) return;
        var changed = 0;
        var report = new MetadataRedactionReport(0, 0, 0);
        string Redacted(string text) => redactor.Redact(text, patterns);
        editor.Change(() =>
        {
            using (editor.History.BeginTransaction("auto-redact"))
            {
                changed = RewriteEach(editor, direct, Redacted, [.. editor.Services.Stories.Paragraphs(editor.Document)], null);
                report = metadata.Redact(editor.Document, editor.History, texts["redact.folio.author"], Redacted);
            }
        });
        if (changed == 0 && report.IsEmpty)
        {
            status.Report(texts["redact.nothing"], NotificationSeverity.Warning);
            return;
        }
        status.Report(texts.Format("redact.folio.auto.done", changed, report.Properties, report.Bookmarks, report.Authors, report.Deletions, report.Versions), NotificationSeverity.Success);
    }

    private int Rewrite(FolioEditor editor, IDocumentEditor writer, Func<string, string> transform, string description)
    {
        var range = editor.HasSelection ? editor.Selection : null as DocumentRange?;
        var paragraphs = range is { } selected
            ? editor.Services.Order.Between(editor.Document, selected.Start, selected.End)
            : editor.Services.Order.StoryOf(editor.Document, editor.Caret.ParagraphId);
        var changed = 0;
        editor.Change(() =>
        {
            using (editor.History.BeginTransaction(description))
                changed = RewriteEach(editor, writer, transform, paragraphs, range);
        });
        return changed;
    }

    private int RewriteEach(FolioEditor editor, IDocumentEditor writer, Func<string, string> transform, IReadOnlyList<ParagraphBlock> paragraphs, DocumentRange? range)
    {
        var changed = 0;
        foreach (var paragraph in paragraphs.ToList())
        {
            var from = range is { } r && paragraph.Id == r.Start.ParagraphId ? r.Start.Offset : 0;
            var to = range is { } s && paragraph.Id == s.End.ParagraphId ? s.End.Offset : paragraph.Length;
            if (to <= from) continue;
            if (Replace(editor, writer, paragraph, from, to, transform)) changed++;
        }
        return changed;
    }

    private bool Replace(FolioEditor editor, IDocumentEditor writer, ParagraphBlock paragraph, int from, int to, Func<string, string> transform)
    {
        var before = paragraph.Text[from..to];
        var after = transform(before);
        if (after == before) return false;
        if (after.Length == before.Length)
        {
            writer.TransformText(editor.Document, editor.History, DocumentRange.In(paragraph.Id, from, to), "transform", transform);
            return true;
        }
        var prefix = 0;
        while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < after.Length - prefix && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
        var start = from + prefix;
        var end = to - suffix;
        if (end > start) writer.Delete(editor.Document, editor.History, DocumentRange.In(paragraph.Id, start, end));
        var inserted = after[prefix..(after.Length - suffix)];
        if (inserted.Length > 0) writer.InsertText(editor.Document, editor.History, new DocumentPosition(paragraph.Id, start), inserted);
        return true;
    }
}
