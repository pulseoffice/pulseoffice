using System.Text.Json;
using System.Text.Json.Nodes;

namespace PulseOffice.Text.Spelling;

public interface IAutoCorrectProfileStore
{
    AutoCorrectProfile Load();

    void Save(AutoCorrectProfile profile);
}

public interface IAutoCorrectProfileCodec
{
    string Write(AutoCorrectProfile profile);

    AutoCorrectProfile Read(string json);
}

public sealed class AutoCorrectProfileCodec : IAutoCorrectProfileCodec
{
    private const int Version = 1;

    private readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly IReadOnlyList<OptionField> _fields =
    [
        new("replacements", options => options.Replacements, (options, value) => options with { Replacements = value }),
        new("capitaliseFirstLetter", options => options.CapitaliseFirstLetter, (options, value) => options with { CapitaliseFirstLetter = value }),
        new("fixTwoInitialCapitals", options => options.FixTwoInitialCapitals, (options, value) => options with { FixTwoInitialCapitals = value }),
        new("ordinalSuperscript", options => options.OrdinalSuperscript, (options, value) => options with { OrdinalSuperscript = value }),
        new("smartQuotes", options => options.SmartQuotes, (options, value) => options with { SmartQuotes = value }),
        new("dashes", options => options.Dashes, (options, value) => options with { Dashes = value }),
        new("automaticLists", options => options.AutomaticLists, (options, value) => options with { AutomaticLists = value }),
        new("applyBorders", options => options.ApplyBorders, (options, value) => options with { ApplyBorders = value }),
        new("wordCompletion", options => options.WordCompletion, (options, value) => options with { WordCompletion = value }),
    ];

    public string Write(AutoCorrectProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var options = new JsonObject();
        foreach (var field in _fields) options[field.Name] = field.Read(profile.Options);

        var languages = new JsonObject();
        foreach (var (language, customization) in profile.Languages.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (customization.IsEmpty) continue;

            var replacements = new JsonObject();
            foreach (var (from, to) in customization.Replacements.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                replacements[from] = to;

            languages[language] = new JsonObject
            {
                ["replacements"] = replacements,
                ["removedReplacements"] = Strings(customization.RemovedReplacements),
                ["abbreviations"] = Words(customization.Abbreviations),
                ["twoCapitalWords"] = Words(customization.TwoCapitalWords),
            };
        }

        return new JsonObject
        {
            ["version"] = Version,
            ["enabled"] = profile.Enabled,
            ["options"] = options,
            ["languages"] = languages,
        }.ToJsonString(Indented);
    }

    public AutoCorrectProfile Read(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root) throw new JsonException("The autocorrect profile is not a JSON object.");

        var options = AutoCorrectOptions.Default;
        if (root["options"] is JsonObject stored)
            foreach (var field in _fields)
                if (Flag(stored[field.Name]) is { } value)
                    options = field.Write(options, value);

        var languages = new Dictionary<string, LanguageCustomization>(StringComparer.Ordinal);
        if (root["languages"] is JsonObject entries)
            foreach (var (language, node) in entries)
                if (language.Length > 0 && node is JsonObject body)
                    languages[language] = Language(body);

        return new AutoCorrectProfile(Flag(root["enabled"]) ?? true, options, languages);
    }

    private LanguageCustomization Language(JsonObject body)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (body["replacements"] is JsonObject pairs)
            foreach (var (from, to) in pairs)
                if (from.Trim().Length > 0 && Text(to) is { Length: > 0 } value)
                    replacements[from.Trim()] = value;

        return new LanguageCustomization(
            replacements,
            ReadStrings(body["removedReplacements"]),
            ReadWords(body["abbreviations"]),
            ReadWords(body["twoCapitalWords"]));
    }

    private JsonObject Words(WordListCustomization words) => new()
    {
        ["added"] = Strings(words.Added),
        ["removed"] = Strings(words.Removed),
    };

    private WordListCustomization ReadWords(JsonNode? node) =>
        node is JsonObject words ? new(ReadStrings(words["added"]), ReadStrings(words["removed"])) : WordListCustomization.None;

    private JsonArray Strings(IEnumerable<string> values) =>
        [.. values.Order(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value))];

    private IReadOnlyCollection<string> ReadStrings(JsonNode? node) =>
        node is JsonArray array
            ? [.. array.Select(Text).OfType<string>().Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)]
            : [];

    private string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private bool? Flag(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? value.GetValue<bool>() : null;

    private sealed record OptionField(
        string Name,
        Func<AutoCorrectOptions, bool> Read,
        Func<AutoCorrectOptions, bool, AutoCorrectOptions> Write);
}
