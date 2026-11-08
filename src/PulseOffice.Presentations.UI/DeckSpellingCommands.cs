using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Spelling;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckSpellingCommands(
    IActiveDeckContext context,
    IThesaurusProvider thesaurus,
    IHyphenationProvider hyphenation,
    IDeckSpelling spelling,
    IRichTextFormatter formatter,
    ISlideTextHyphenation slideHyphenation,
    IAutoSpellSettings autoSpell,
    ISpellChecker checker,
    IUserDictionary userWords,
    IStatusBarService status,
    ITextCatalog texts,
    ISpellingPrompt prompt,
    IThesaurusPrompt synonymsPrompt,
    ISpellingLanguagePicker languages,
    IDictionaryNotice notice) : DeckCommandSet(context)
{
    private LanguageTag Language => Context.Target is { } t ? spelling.LanguageOf(t.Deck) : LanguageTag.Parse(CultureInfo.CurrentCulture.Name);

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.spelling.check") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = "F7",
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => CheckAsync(ct),
        },
        Prototype("deck.spelling.automatic") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            CheckedWhen = _ => autoSpell.Enabled,
            Execute = (_, _) =>
            {
                autoSpell.Enabled = !autoSpell.Enabled;
                status.Report(texts[autoSpell.Enabled ? "spelling.on" : "spelling.off"]);
                return Task.CompletedTask;
            },
        },
        Prototype("deck.spelling.languageAll") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => LanguageAsync(ct),
        },
        Prototype("deck.spelling.thesaurus") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = "Ctrl+F7",
            EnabledWhen = _ => Context.Target is { } t && t.HasTextTarget() && t.SelectedText() is not null,
            Execute = (_, ct) => ThesaurusAsync(ct),
        },
        Prototype("deck.spelling.hyphenation") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.HasTextTarget(),
            Execute = (_, _) => { Hyphenate(); return Task.CompletedTask; },
        },
    ];

    private async Task LanguageAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target) return;
        if (await languages.PickAsync(spelling.LanguageOf(target.Deck), "command.deck.spelling.languageAll.title", ct).ConfigureAwait(true) is not { } chosen) return;

        target.Actions.SetLanguage(chosen);
        status.Report(texts.Format("spelling.deck.languageSet", chosen));
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        if (!checker.HasDictionary(Language))
        {
            notice.ShowMissingDictionaries();
            return;
        }

        var round = new SpellingRound(userWords);
        var start = t.CurrentIndex();
        while (await PassAsync(t, start, round, ct).ConfigureAwait(true) is { } restart) start = restart;
    }

    private async Task<int?> PassAsync(DeckTarget t, int start, SpellingRound round, CancellationToken ct)
    {
        var shifts = new Dictionary<(string Slide, string Element, int Paragraph), int>();
        foreach (var found in spelling.Walk(t.Deck, start, Language).ToList())
        {
            if (userWords.Knows(found.Mistake.Word.Text)) continue;
            var key = (found.Slide.Id, found.ElementId, found.Paragraph);
            var shift = shifts.GetValueOrDefault(key);
            var word = found.Mistake.Word with { Start = found.Mistake.Word.Start + shift };

            if (found.Slide.Find(found.ElementId) is not TextElement element) continue;
            var paragraphs = element.Text.Paragraphs;
            if (found.Paragraph >= paragraphs.Count) continue;
            var current = paragraphs[found.Paragraph].Text;
            if (word.Start + word.Length > current.Length || current.Substring(word.Start, word.Length) != word.Text) continue;

            t.GoToSlide(t.Deck.IndexOf(found.Slide));
            t.Select([element]);

            if (round.Ignores(word.Text)) continue;
            if (round.Automatic(word.Text) is not { } replacement)
            {
                var answer = await prompt.AskAsync(new SpellingFinding(word.Text, texts.Format("deck.review.slide", t.Deck.IndexOf(found.Slide) + 1), found.Mistake.Suggestions),
                    "command.deck.spelling.check.title", ct).ConfigureAwait(true);
                var step = round.Decide(word.Text, answer);
                if (step.Kind == SpellingStepKind.Stop) return null;
                if (step.Kind == SpellingStepKind.Recheck) return Math.Max(0, t.Deck.IndexOf(found.Slide));
                if (step.Kind == SpellingStepKind.Skip) continue;
                replacement = step.Replacement;
            }

            t.Actions.EditText(found.Slide, element.Id, spelling.Replace(element.Text, found.Paragraph, word, replacement));
            shifts[key] = shift + replacement.Length - word.Length;
        }

        status.Report(texts["spelling.done"]);
        return null;
    }

    private async Task ThesaurusAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.SelectedText()?.Trim() is not { Length: > 0 } word) return;
        if (thesaurus.For(Language) is not { } book) return;
        var slide = t.CurrentSlide();
        var selection = t.CommitSelection();
        if (slide is null || selection is null) return;

        var choices = book.Lookup(word)
            .SelectMany(meaning => meaning.Synonyms.Select(synonym => new ThesaurusChoice(synonym, meaning.PartOfSpeech)))
            .DistinctBy(choice => choice.Synonym).Take(40).ToList();
        if (choices.Count == 0) return;

        if (await synonymsPrompt.AskAsync(word, choices, "command.deck.spelling.thesaurus.title", ct).ConfigureAwait(true) is not { } replacement) return;

        if (!Holds(t, slide) || slide.Find(selection.ElementId) is not TextElement element) return;
        t.Actions.EditText(slide, element.Id,
            formatter.ReplaceWord(element.Text, (selection.Start, selection.End), replacement));
    }

    private void Hyphenate()
    {
        if (Context.Target is not { } t) return;
        if (hyphenation.For(Language) is not { } hyphenator) return;

        t.ApplyText((text, _) => slideHyphenation.Hyphenate(text, hyphenator));
    }
}
