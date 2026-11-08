using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Spelling;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioSpellingCommands(
    IActiveFolioContext context,
    IFolioProofing proofing,
    IFolioLanguages languages,
    ISpellingPrompt prompt,
    IUserDictionary userWords,
    IAutoSpellSettings autoSpell,
    ISpellDictionaryProvider dictionaries,
    IThesaurusProvider thesaurus,
    ICaretWord words,
    IThesaurusPrompt synonymsPrompt,
    ISpellingLanguagePicker languagePicker,
    IDictionaryLanguageNames dictionaryLanguages,
    IStatusBarService status,
    ITextCatalog texts,
    IDictionaryNotice notice) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.spelling.check") with { DefaultGesture = "F7", Execute = (_, ct) => CheckAsync(ct) },
        Viewing("folio.spelling.automatic") with
        {
            DefaultGesture = "Shift+F7",
            CheckedWhen = _ => autoSpell.Enabled,
            Execute = (_, _) =>
            {
                autoSpell.Enabled = !autoSpell.Enabled;
                Editor?.Refresh();
                status.Report(texts[autoSpell.Enabled ? "spelling.on" : "spelling.off"]);
                return Task.CompletedTask;
            },
        },
        Editing("folio.spelling.thesaurus") with { DefaultGesture = "Ctrl+F7", Surface = CommandSurface.Advanced, Execute = (_, ct) => ThesaurusAsync(ct) },
        Editing("folio.language.selection") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => LanguageAsync(LanguageScope.Selection, ct) },
        Editing("folio.language.paragraph") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => LanguageAsync(LanguageScope.Paragraph, ct) },
        Editing("folio.language.all") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => LanguageAsync(LanguageScope.All, ct) },
    ];

    private enum LanguageScope
    {
        Selection,
        Paragraph,
        All,
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var round = new SpellingRound(userWords);
        var story = editor.Services.Order.StoryOf(editor.Document, editor.Caret.ParagraphId).Select(paragraph => paragraph.Id).ToList();
        var first = Math.Max(0, story.IndexOf(editor.Caret.ParagraphId));
        var order = story.Skip(first).Concat(story.Take(first)).ToList();
        var found = 0;
        for (var index = 0; index < order.Count; index++)
        {
            var paragraphId = order[index];
            var cursor = index == 0 ? editor.Caret.Offset : 0;
            while (editor.ContentOf(paragraphId) is { } content)
            {
                var mark = proofing.Marks(editor.Document, content, always: true)
                    .Where(candidate => candidate.Start >= cursor && !round.Ignores(Word(content.Text, candidate)))
                    .OrderBy(candidate => candidate.Start).FirstOrDefault();
                if (mark is null) break;
                var word = Word(content.Text, mark);
                var range = DocumentRange.In(paragraphId, mark.Start, mark.Start + mark.Length);
                found++;
                if (round.Automatic(word) is { } automatic)
                {
                    editor.Replace(range, automatic);
                    cursor = mark.Start + automatic.Length;
                    continue;
                }
                editor.Select(range.Start, range.End);
                var answer = await prompt.AskAsync(new SpellingFinding(word, Surrounding(content.Text, mark), proofing.Suggestions(mark, content.Text))
                {
                    Rule = mark.Rule,
                }, "command.folio.spelling.check.title", ct).ConfigureAwait(true);
                var step = round.Decide(word, answer);
                switch (step.Kind)
                {
                    case SpellingStepKind.Stop:
                        return;
                    case SpellingStepKind.Recheck:
                        found--;
                        continue;
                    case SpellingStepKind.Skip:
                        cursor = mark.Start + mark.Length;
                        break;
                    default:
                        editor.Replace(range, step.Replacement);
                        cursor = mark.Start + step.Replacement.Length;
                        break;
                }
            }
        }
        if (found == 0 && dictionaries.Available.Count == 0)
        {
            notice.ShowMissingDictionaries();
            return;
        }

        status.Report(found == 0 ? texts["spelling.none"] : texts["spelling.done"], NotificationSeverity.Success);
    }

    private string Word(string text, ProofMark mark) => text.Substring(mark.Start, Math.Min(mark.Length, text.Length - mark.Start));

    private string Surrounding(string text, ProofMark mark)
    {
        var start = Math.Max(0, mark.Start - 30);
        var end = Math.Min(text.Length, mark.Start + mark.Length + 30);
        return (start > 0 ? "…" : string.Empty) + text[start..end].Replace('\u2028', ' ') + (end < text.Length ? "…" : string.Empty);
    }

    private async Task ThesaurusAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || editor.ContentOf(editor.Caret.ParagraphId) is not { } content) return;
        var (start, end) = editor.HasSelection && editor.Selection.Start.ParagraphId == editor.Selection.End.ParagraphId
            ? (editor.Selection.Start.Offset, editor.Selection.End.Offset)
            : words.Around(content.Text, editor.Caret.Offset);
        if (end <= start)
        {
            status.Report(texts["thesaurus.noWord"], NotificationSeverity.Warning);
            return;
        }
        var word = content.Text[start..end];
        var language = LanguageTag.Parse(languages.At(editor.Document, content, start));
        if (thesaurus.For(language) is not { } book)
        {
            status.Report(texts["thesaurus.missing"], NotificationSeverity.Warning);
            return;
        }
        var synonyms = book.Lookup(word).SelectMany(meaning => meaning.Synonyms.Select(synonym => (Synonym: synonym, meaning.PartOfSpeech)))
            .DistinctBy(item => item.Synonym).Take(40).ToList();
        if (synonyms.Count == 0)
        {
            status.Report(texts["thesaurus.noWord"], NotificationSeverity.Warning);
            return;
        }
        if (await synonymsPrompt.AskAsync(word, [.. synonyms.Select(item => new ThesaurusChoice(item.Synonym, item.PartOfSpeech))],
                "command.folio.spelling.thesaurus.title", ct).ConfigureAwait(true) is not { } replacement) return;
        editor.Replace(DocumentRange.In(editor.Caret.ParagraphId, start, end), replacement);
    }

    private async Task LanguageAsync(LanguageScope scope, CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var current = editor.ContentOf(editor.Caret.ParagraphId) is { } content ? languages.At(editor.Document, content, editor.Caret.Offset) : editor.Document.Language;
        if (await languagePicker.PickAsync(LanguageTag.Parse(current), $"command.folio.language.{scope.ToString().ToLowerInvariant()}.title",
                new DictionaryLanguage(FolioLanguages.NoProofing, texts["folio.language.none"]), ct).ConfigureAwait(true) is not { } chosen) return;
        var range = scope switch
        {
            LanguageScope.Selection => editor.CharacterRange(),
            LanguageScope.Paragraph => Paragraphs(editor),
            _ => Body(editor),
        };
        if (range.IsCollapsed && scope != LanguageScope.All) return;
        editor.Apply((documents, target) =>
        {
            using var transaction = editor.History.BeginTransaction("language");
            if (scope == LanguageScope.All && chosen != FolioLanguages.NoProofing)
            {
                var recorder = new EditRecorder(editor.Document, "language").Touch<string?>(editor.Document.Properties, () => [editor.Document.Language], state => editor.Document.Language = state[0]);
                editor.Document.Language = chosen;
                recorder.Commit(editor.History);
            }
            if (!target.IsCollapsed) documents.MapRuns(editor.Document, editor.History, target, "language", run => run with { Language = chosen });
        }, range);
        editor.Refresh();
        status.Report(texts.Format("folio.language.set", chosen == FolioLanguages.NoProofing ? texts["folio.language.none"] : dictionaryLanguages.For(chosen).Name));
    }

    private DocumentRange Paragraphs(FolioEditor editor)
    {
        var range = editor.Selection;
        var last = editor.ContentOf(range.End.ParagraphId)?.Text.Length ?? range.End.Offset;
        return new DocumentRange(range.Start with { Offset = 0 }, range.End with { Offset = last });
    }

    private DocumentRange Body(FolioEditor editor)
    {
        var story = editor.Services.Order.StoryOf(editor.Document, editor.Document.Body.Blocks.OfType<ParagraphBlock>().FirstOrDefault()?.Id ?? editor.Caret.ParagraphId);
        return new DocumentRange(new DocumentPosition(story[0].Id, 0), new DocumentPosition(story[^1].Id, story[^1].Length));
    }
}
