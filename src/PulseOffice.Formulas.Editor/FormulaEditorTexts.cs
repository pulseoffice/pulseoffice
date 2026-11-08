using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Formulas.Editor;

public interface IFormulaEditorTexts
{
    IReadOnlyDictionary<string, string> Build(ITextCatalog texts);
}

public sealed class FormulaEditorTexts(PulseOffice.Formulas.IFormulaCatalog catalog) : IFormulaEditorTexts
{
    public IReadOnlyDictionary<string, string> Build(ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cancel"] = texts["prompt.cancel"],
            ["confirm"] = texts["prompt.confirm"],
            ["guide"] = texts["formula.guide"],
            ["preview"] = texts["formula.preview"],
            ["source"] = texts["formula.source"],
            ["search"] = texts["formula.search"],
            ["empty"] = texts["formula.empty"],
            ["valid"] = texts["formula.valid"],
            ["noEntry"] = texts["formula.noEntry"],
            ["insertHint"] = texts["formula.insertHint"],
            ["guide.title"] = texts["formula.guide.title"],
            ["guide.syntax"] = texts["formula.guide.syntax"],
            ["guide.palette"] = texts["formula.guide.palette"],
            ["guide.preview"] = texts["formula.guide.preview"],
            ["guide.problems"] = texts["formula.guide.problems"],
        };

        foreach (var category in new[]
        {
            "structure", "matrices", "accents", "operators",
            "greek", "relations", "sets", "arrows", "functions", "misc",
        })
            map[$"category.{category}"] = texts[$"formula.category.{category}"];

        foreach (var problem in new[]
        {
            "unexpectedClose", "mismatched", "unclosed", "unclosedText", "unknownWord",
        })
            map[$"problem.{problem}"] = texts[$"formula.problem.{problem}"];

        foreach (var entry in catalog.All)
        {
            var name = $"formula.item.{entry.Id}";
            if (texts.Contains(name)) map[$"item.{entry.Id}"] = texts[name];

            if (!entry.HasSummary) continue;

            var summary = $"formula.summary.{entry.Id}";
            if (texts.Contains(summary)) map[$"summary.{entry.Id}"] = texts[summary];
        }

        return map;
    }
}

public interface IFormulaPrompt
{
    Task<string?> AskAsync(string existing, string titleKey, CancellationToken ct);
}

public sealed class FormulaPrompt(IDialogService dialogs, IFormulaEditorTexts formulas, ITextCatalog texts) : IFormulaPrompt
{
    public async Task<string?> AskAsync(string existing, string titleKey, CancellationToken ct)
    {
        var source = await dialogs.ShowAsync<string>(
            new FormulaEditorView(existing, formulas.Build(texts)),
            new DialogOptions
            {
                TitleKey = titleKey,
                PreferredWidth = 1120,
                PreferredHeight = 720,
                CanCancelWithEscape = false,
            },
            ct).ConfigureAwait(true);
        return source?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
    }
}
