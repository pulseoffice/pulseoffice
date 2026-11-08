using System.Globalization;
using System.Text;
using PulseOffice.Formats.Interchange;
using PulseOffice.Formats.Interchange.Text.Plain;
using PulseOffice.Foundation.Text;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.Dialogs;

public interface ICsvImportPrompt
{
    Task<CsvOptions?> AskAsync(string fileName, byte[] head, CsvOptions detected, CancellationToken ct);
}

public sealed record CsvEncodingChoice(string Key, int CodePage)
{
    public string Label { get; init; } = Key;
}

public sealed record CsvLanguageChoice(string Key, string Culture)
{
    public string Label { get; init; } = Key;
}

public sealed class CsvImportPrompt(IDialogService dialogs, ITextCatalog texts, ITextToColumns splitter, ICodePages codePages) : ICsvImportPrompt
{
    private const int PreviewLines = 12;

    private readonly IReadOnlyList<CsvEncodingChoice> _encodings =
    [
        new("csvImport.encoding.utf8", 65001),
        new("csvImport.encoding.windows1252", 1252),
        new("csvImport.encoding.iso885915", 28605),
        new("csvImport.encoding.utf16", 1200),
    ];

    private readonly IReadOnlyList<CsvLanguageChoice> _languages =
    [
        new("csvImport.language.en", "en-US"),
        new("csvImport.language.it", "it-IT"),
        new("csvImport.language.de", "de-DE"),
        new("csvImport.language.fr", "fr-FR"),
        new("csvImport.language.es", "es-ES"),
    ];

    public async Task<CsvOptions?> AskAsync(string fileName, byte[] head, CsvOptions detected, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(detected);

        var encodings = Encodings(detected.Encoding);
        var languages = Languages(detected.Culture);
        var encoding = encodings.FindIndex(choice => choice.CodePage == detected.Encoding.CodePage);
        var language = languages.FindIndex(choice => string.Equals(choice.Culture, detected.Culture.Name, StringComparison.OrdinalIgnoreCase));
        TextToColumnsEditor? editor = null;

        FormField[] fields =
        [
            new ChoiceField("encoding", texts["csvImport.encoding"], [.. encodings.Select(choice => choice.Label)], encoding),
            new ChoiceField("language", texts["csvImport.language"], [.. languages.Select(choice => choice.Label)], language),
            new NumberField("skip", texts["csvImport.skip"], 0, 0, 1000) { Integer = true },
            new TextToColumnsField("options", Lines(head, encodings[encoding].CodePage, 0), CellRange.Single(new CellAddress(0, 0)), splitter)
            {
                Initial = Initial(detected),
                Attached = created => editor = created,
            },
        ];

        var view = new FormPromptView(texts.Format("csvImport.explanation", fileName), fields, texts);
        var shown = (Encoding: encoding, Skip: 0);
        view.ValuesChanged += (_, _) =>
        {
            var values = view.Values;
            var now = (Encoding: Choice(values, "encoding", encodings.Count, encoding), Skip: (int)Number(values, "skip"));
            if (now == shown || editor is null) return;
            shown = now;
            editor.Resample(Lines(head, encodings[now.Encoding].CodePage, now.Skip));
        };

        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            view, new DialogOptions { TitleKey = "csvImport.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (answers is null || answers.GetValueOrDefault("options") is not TextToColumnsChoice choice) return null;

        var separators = choice.Separators();
        if (separators.Count == 0) separators = [detected.Delimiter.Separator];

        return detected with
        {
            Encoding = codePages.Get(encodings[Choice(answers, "encoding", encodings.Count, encoding)].CodePage),
            Culture = CultureInfo.GetCultureInfo(languages[Choice(answers, "language", languages.Count, language)].Culture),
            SkipRows = (int)Number(answers, "skip"),
            DetectTypes = choice.DetectNumbers,
            AutoDetect = false,
            Delimiter = new DelimitedFormat
            {
                Separator = separators[0],
                OtherSeparators = string.Concat(separators.Skip(1)),
                MergeSeparators = choice.MergeSeparators,
                Quote = choice.Qualifier switch
                {
                    TextQualifier.Double => '"',
                    TextQualifier.Single => '\'',
                    _ => null,
                },
            },
        };
    }

    private TextToColumnsChoice Initial(CsvOptions detected)
    {
        var separator = detected.Delimiter.Separator;
        return new TextToColumnsChoice(
            separator == '\t', separator == ';', separator == ',', separator == ' ',
            separator is '\t' or ';' or ',' or ' ' ? string.Empty : separator.ToString(),
            detected.Delimiter.MergeSeparators,
            detected.Delimiter.Quote switch
            {
                '"' => TextQualifier.Double,
                '\'' => TextQualifier.Single,
                _ => TextQualifier.None,
            },
            detected.DetectTypes);
    }

    private IReadOnlyList<string> Lines(byte[] head, int codePage, int skip)
    {
        var text = codePages.Get(codePage).GetString(head).TrimStart('﻿');
        return [.. text.Split('\n').Select(line => line.TrimEnd('\r')).Skip(skip).Where(line => line.Length > 0).Take(PreviewLines)];
    }

    public IReadOnlyList<CsvEncodingChoice> EncodingChoices(Encoding detected) => Encodings(detected);

    public IReadOnlyList<CsvLanguageChoice> LanguageChoices(CultureInfo detected) => Languages(detected);

    private List<CsvEncodingChoice> Encodings(Encoding detected)
    {
        var choices = _encodings.Select(choice => choice with { Label = texts[choice.Key] }).ToList();
        if (choices.All(choice => choice.CodePage != detected.CodePage))
            choices.Add(new CsvEncodingChoice(string.Empty, detected.CodePage) { Label = detected.WebName });
        return choices;
    }

    private List<CsvLanguageChoice> Languages(CultureInfo detected)
    {
        var choices = _languages.Select(choice => choice with { Label = texts[choice.Key] }).ToList();
        if (choices.All(choice => !string.Equals(choice.Culture, detected.Name, StringComparison.OrdinalIgnoreCase)))
            choices.Add(new CsvLanguageChoice(string.Empty, detected.Name) { Label = detected.NativeName });
        return choices;
    }

    private int Choice(IReadOnlyDictionary<string, object> values, string key, int count, int fallback) =>
        values.TryGetValue(key, out var value) && value is int index && index >= 0 && index < count ? index : fallback;

    private double Number(IReadOnlyDictionary<string, object> values, string key) =>
        values.TryGetValue(key, out var value) && value is double number && double.IsFinite(number) ? Math.Max(0, number) : 0;
}
