using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Spelling;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerSpellingCommands(
    IActiveSheetContext context,
    ISheetSpelling spelling,
    ISpellDictionaryProvider dictionaries,
    IUserDictionary userWords,
    IThesaurusProvider thesaurus,
    IHyphenationProvider hyphenation,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    ISheetFormattingService formatting,
    ISheetEditGuard guard,
    PulseOffice.UI.Controls.Spelling.ISpellingPrompt prompt,
    IThesaurusPrompt synonymsPrompt,
    ISpellingLanguagePicker languages,
    IDictionaryNotice notice) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.spelling.check") with
        {
            IconKey = "ledger.spelling.check",
            MenuPath = "tools/language",
            DefaultGesture = "F7",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => CheckAsync(ct),
        },

        Prototype("ledger.spelling.automatic") with
        {
            IconKey = "ledger.spelling.automatic",
            MenuPath = "tools/language",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            CheckedWhen = _ => spelling.Enabled,
            Execute = (_, _) =>
            {
                spelling.Enabled = !spelling.Enabled;
                status.Report(texts[spelling.Enabled ? "spelling.on" : "spelling.off"]);
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.spelling.thesaurus") with
        {
            MenuPath = "tools/language",
            ContextPaths = ["cell/language"],
            DefaultGesture = "Ctrl+F7",
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => ThesaurusAsync(ct),
        },

        Prototype("ledger.spelling.hyphenation") with
        {
            MenuPath = "tools/language",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => HyphenationAsync(ct),
        },

        Prototype("ledger.spelling.languageSelection") with
        {
            MenuPath = "tools/language",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => LanguageAsync(ct, everything: false),
        },

        Prototype("ledger.spelling.languageAll") with
        {
            MenuPath = "tools/language",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => LanguageAsync(ct, everything: true),
        },
    ];

    private async Task CheckAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;
        if (dictionaries.Available.Count == 0)
        {
            notice.ShowMissingDictionaries();
            return;
        }

        var round = new SpellingRound(userWords);
        var start = Context.Selection.Start;
        while (await PassAsync(workbook, engine, start, round, ct).ConfigureAwait(true) is { } restart) start = restart;
    }

    private async Task<CellAddress?> PassAsync(Workbook workbook, PulseOffice.Sheets.Engine.Recalculation.CalculationEngine engine, CellAddress start, SpellingRound round, CancellationToken ct)
    {
        var sheet = workbook.Sheets[Context.SheetIndex];
        var replaced = new ShiftedReplacements<CellAddress>();
        foreach (var found in spelling.Walk(workbook, sheet, start))
        {
            var word = found.Mistake.Word.Text;
            if (round.Ignores(word)) continue;
            if (round.Automatic(word) is { } automatic)
            {
                Replace(engine, sheet, found, automatic, replaced);
                continue;
            }

            Context.SelectRange(CellRange.Single(found.Address));
            var answer = await prompt.AskAsync(new SpellingFinding(word, found.Address.ToA1(), found.Mistake.Suggestions),
                "command.ledger.spelling.check.title", ct).ConfigureAwait(true);

            var step = round.Decide(word, answer);
            switch (step.Kind)
            {
                case SpellingStepKind.Stop:
                    return null;
                case SpellingStepKind.Recheck:
                    return found.Address;
                case SpellingStepKind.Skip:
                    continue;
            }

            Replace(engine, sheet, found, step.Replacement, replaced);
        }

        status.Report(texts["spelling.done"]);
        return null;
    }

    private void Replace(
        Engine.Recalculation.CalculationEngine engine, Worksheet sheet, SheetMistake found, string replacement,
        ShiftedReplacements<CellAddress> replaced)
    {
        var cell = sheet[found.Address];
        if (cell.Value.Kind != ValueKind.Text) return;

        if (replaced.Replace(found.Address, cell.Value.AsText(), found.Mistake.Word, replacement) is not { } updated)
            return;

        if (Locked(sheet, found.Address)) return;

        var target = new SheetCell(Context.SheetIndex, found.Address);
        var previous = cell;

        engine.SetValue(target, CellValue.Text(updated));

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.spelling.replace"],
            redo: () => engine.SetValue(target, CellValue.Text(updated)),
            undo: () => engine.SetValue(target, previous.Value)));

        Context.RequestRefresh();
    }

    private async Task ThesaurusAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var address = Context.Selection.Start;
        var cell = sheet[address];

        if (cell.HasFormula || cell.Value.Kind != ValueKind.Text)
        {
            status.Report(texts["thesaurus.noWord"], NotificationSeverity.Warning);
            return;
        }

        var word = cell.Value.AsText().Trim();
        var language = spelling.LanguageOf(workbook, sheet, address);

        if (thesaurus.For(language) is not { } book)
        {
            status.Report(texts["thesaurus.missing"], NotificationSeverity.Warning);
            return;
        }

        var meanings = book.Lookup(word);

        if (meanings.Count == 0)
        {
            status.Report(texts["thesaurus.noWord"], NotificationSeverity.Warning);
            return;
        }

        var choices = meanings
            .SelectMany(meaning => meaning.Synonyms.Select(synonym => new ThesaurusChoice(synonym, meaning.PartOfSpeech)))
            .DistinctBy(choice => choice.Synonym)
            .Take(40)
            .ToList();

        if (await synonymsPrompt.AskAsync(word, choices, "command.ledger.spelling.thesaurus.title", ct).ConfigureAwait(true) is not { } replacement) return;

        if (Locked(sheet, address)) return;

        var target = new SheetCell(Context.SheetIndex, address);
        var previous = cell;

        engine.SetValue(target, CellValue.Text(replacement));

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.spelling.replace"],
            redo: () => engine.SetValue(target, CellValue.Text(replacement)),
            undo: () => engine.SetValue(target, previous.Value)));

        Context.RequestRefresh();
    }

    private async Task HyphenationAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var address = Context.Selection.Start;
        var cell = sheet[address];

        if (cell.Value.Kind != ValueKind.Text)
        {
            status.Report(texts["hyphenation.noWord"], NotificationSeverity.Warning);
            return;
        }

        var language = spelling.LanguageOf(workbook, sheet, address);

        if (hyphenation.For(language) is not { } hyphenator)
        {
            status.Report(texts["hyphenation.missing"], NotificationSeverity.Warning);
            return;
        }

        var text = cell.Value.AsText();
        var tokenizer = new WordTokenizer();

        var lines = tokenizer.Words(text)
            .Select(word => string.Join('-', Pieces(word.Text, hyphenator.Positions(word.Text))))
            .ToList();

        var form = new FormPromptView(
            string.Join('\n', lines),
            [],
            texts);

        _ = await dialogs.ShowAsync<Dictionary<string, object>>(
            form,
            new DialogOptions { TitleKey = "command.ledger.spelling.hyphenation.title", PreferredWidth = 420 },
            ct).ConfigureAwait(true);
    }

    private static IEnumerable<string> Pieces(string word, IReadOnlyList<int> positions)
    {
        var previous = 0;

        foreach (var position in positions)
        {
            yield return word[previous..position];
            previous = position;
        }

        yield return word[previous..];
    }

    private async Task LanguageAsync(CancellationToken ct, bool everything)
    {
        if (Context.Workbook is not { } workbook) return;

        var titleKey = everything ? "command.ledger.spelling.languageAll.title" : "command.ledger.spelling.languageSelection.title";
        if (await languages.PickAsync(LanguageTag.Parse(workbook.DefaultLanguage ?? string.Empty), titleKey, ct).ConfigureAwait(true) is not { } chosen) return;

        if (everything)
        {
            var before = workbook.DefaultLanguage;
            if (before == chosen) return;

            workbook.DefaultLanguage = chosen;
            Context.History?.Push(new Document.Commands.DelegateAction(
                texts["history.spelling.language"],
                redo: () => workbook.DefaultLanguage = chosen,
                undo: () => workbook.DefaultLanguage = before));
            Context.RequestRefresh();
            return;
        }

        formatting.ApplyToSelection(texts["history.spelling.language"], style => style with { Language = chosen });
    }

    private bool Locked(Worksheet sheet, CellAddress address)
    {
        if (Context.Workbook is not { } workbook || guard.CanWrite(workbook, sheet, new CellRange(address, address))) return false;

        status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }
}
