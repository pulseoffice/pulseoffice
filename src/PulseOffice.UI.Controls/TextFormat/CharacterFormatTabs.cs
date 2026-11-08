using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Text;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.TextFormat;

public interface ICharacterFormatTabs
{
    IReadOnlyList<FormTab> Tabs(CharacterFormatRequest request);

    CharacterFormatState Read(CharacterFormatRequest request, IReadOnlyDictionary<string, object> values);
}

public interface ICharacterFormatDialog
{
    Task<CharacterFormatState?> AskAsync(CharacterFormatRequest request, string titleKey, CancellationToken ct);
}

public sealed class CharacterFormatTabs(
    IFontPickerFactory fonts,
    ILanguageChoices languages,
    IIconCatalog icons,
    ITextFormatPreviews previews,
    ICharacterSampleLayout layout,
    ITextCatalog texts) : ICharacterFormatTabs
{
    private readonly ColorFieldValue _colors = new();

    private readonly IReadOnlyList<double> _sizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 60, 72];

    private readonly TextCase[] _cases = [TextCase.None, TextCase.Upper, TextCase.Lower, TextCase.Capitalize];

    private readonly FontRelief[] _reliefs = [FontRelief.None, FontRelief.Embossed, FontRelief.Engraved];

    private readonly TextBaseline[] _positions = [TextBaseline.Normal, TextBaseline.Superscript, TextBaseline.Subscript];

    private readonly IReadOnlyList<(string Key, CharacterFeatures Feature, Func<CharacterFormatState, bool> Get, Func<CharacterFormatState, bool, CharacterFormatState> Set)> _effects =
    [
        ("strikethrough", CharacterFeatures.Strikethrough, state => state.Strikethrough, (state, on) => state with { Strikethrough = on }),
        ("overline", CharacterFeatures.Overline, state => state.Overline, (state, on) => state with { Overline = on }),
        ("smallCaps", CharacterFeatures.SmallCaps, state => state.SmallCaps, (state, on) => state with { SmallCaps = on }),
        ("shadow", CharacterFeatures.Shadow, state => state.Shadow, (state, on) => state with { Shadow = on }),
        ("outline", CharacterFeatures.Outline, state => state.Outline, (state, on) => state with { Outline = on }),
        ("hidden", CharacterFeatures.Hidden, state => state.Hidden, (state, on) => state with { Hidden = on }),
    ];

    public IReadOnlyList<FormTab> Tabs(CharacterFormatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = request.Current;
        var font = texts["textFormat.section.font"];
        var effects = texts["textFormat.section.effects"];
        var main = new List<FormField>();
        if (request.Offers(CharacterFeatures.Font)) main.Add(new FontField("font", texts["textFormat.character.font"], current.Font, fonts) { Section = font });
        if (request.Offers(CharacterFeatures.Style))
            main.Add(new IconChoiceField("fontStyle", texts["textFormat.character.style"], StyleChoices(), icons, (current.Bold ? 1 : 0) + (current.Italic ? 2 : 0)) { ShowLabels = true, Section = font });
        if (request.Offers(CharacterFeatures.Size))
            main.Add(new NumberField("size", texts["textFormat.character.size"], current.Size, 1, 999, 0.5) { Unit = "pt", Presets = _sizes, LargeStep = 4, Section = font });
        if (request.Offers(CharacterFeatures.Language))
        {
            var choices = languages.For(current.Language);
            main.Add(new ChoiceField("language", texts["textFormat.character.language"], [.. choices.Select(choice => choice.Title)],
                Math.Max(0, choices.ToList().FindIndex(choice => string.Equals(choice.Tag, current.Language, StringComparison.OrdinalIgnoreCase)))) { Section = font });
        }
        if (request.Offers(CharacterFeatures.Color)) main.Add(new ColorField("color", texts["textFormat.character.color"], _colors.Hex(current.Color)) { Section = effects });
        if (request.Offers(CharacterFeatures.Highlight)) main.Add(new ColorField("highlight", texts["textFormat.character.highlight"], _colors.Hex(current.Highlight)) { Section = effects });
        if (request.Offers(CharacterFeatures.Underline))
            main.Add(new IconChoiceField("underline", texts["textFormat.character.underline"], UnderlineChoices(request), icons,
                Math.Min((int)current.Underline, UnderlineChoices(request).Count - 1)) { ShowLabels = true, Section = effects });
        if (request.Offers(CharacterFeatures.Case))
            main.Add(new ChoiceField("case", texts["textFormat.character.case"], [.. _cases.Select(item => texts[$"textFormat.character.case.{item.ToString().ToLowerInvariant()}"])],
                Math.Max(0, Array.IndexOf(_cases, current.Case))) { Section = effects });
        if (request.Offers(CharacterFeatures.Relief))
            main.Add(new ChoiceField("relief", texts["textFormat.character.relief"], [.. _reliefs.Select(item => texts[$"textFormat.character.relief.{item.ToString().ToLowerInvariant()}"])],
                Math.Max(0, Array.IndexOf(_reliefs, current.Relief))) { Section = effects });
        main.AddRange(_effects.Where(effect => request.Offers(effect.Feature))
            .Select(effect => new CheckField(effect.Key, texts[$"textFormat.character.{effect.Key}"], effect.Get(current)) { Section = texts["textFormat.section.more"] }));

        var position = new List<FormField>();
        if (request.Offers(CharacterFeatures.Position))
            position.Add(new IconChoiceField("position", texts["textFormat.character.position"], PositionChoices(), icons, Math.Max(0, Array.IndexOf(_positions, current.Baseline))) { ShowLabels = true });
        if (request.Offers(CharacterFeatures.Spacing))
            position.Add(new NumberField("spacing", texts["textFormat.character.spacing"], current.LetterSpacing, -10, 100, 0.1) { Unit = "pt", Presets = [-1, -0.5, 0, 0.5, 1, 2, 3] });
        if (request.Offers(CharacterFeatures.Scale))
            position.Add(new NumberField("scale", texts["textFormat.character.scale"], current.Scale, 1, 600) { Unit = "%", Integer = true, Presets = [50, 66, 80, 90, 100, 110, 150, 200] });
        if (request.Offers(CharacterFeatures.Kerning)) position.Add(new CheckField("kerning", texts["textFormat.character.kerning"], current.Kerning));

        var tabs = new List<FormTab>
        {
            new(texts["textFormat.character.tab.font"], texts["textFormat.character.explanation"], main) { LivePreview = feed => Preview(feed, request) },
        };
        if (position.Count > 0)
            tabs.Add(new FormTab(texts["textFormat.character.tab.position"], texts["textFormat.character.position.explanation"], position) { LivePreview = feed => Preview(feed, request) });
        return tabs;
    }

    public CharacterFormatState Read(CharacterFormatRequest request, IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(values);
        var answers = new FormAnswers(values);
        var current = request.Current;
        var state = current;
        if (request.Offers(CharacterFeatures.Font) && answers.Text("font") is { Length: > 0 } font) state = state with { Font = font };
        if (request.Offers(CharacterFeatures.Style) && answers.Has("fontStyle"))
        {
            var style = answers.Choice("fontStyle", 4);
            state = state with { Bold = style is 1 or 3, Italic = style is 2 or 3 };
        }
        if (request.Offers(CharacterFeatures.Size)) state = state with { Size = answers.Number("size", current.Size) };
        if (request.Offers(CharacterFeatures.Language) && answers.Has("language"))
        {
            var choices = languages.For(current.Language);
            state = state with { Language = choices[answers.Choice("language", choices.Count)].Tag };
        }
        if (request.Offers(CharacterFeatures.Color)) state = state with { Color = answers.Color("color", current.Color) };
        if (request.Offers(CharacterFeatures.Highlight)) state = state with { Highlight = answers.Color("highlight", current.Highlight) };
        if (request.Offers(CharacterFeatures.Underline) && answers.Has("underline"))
            state = state with { Underline = (UnderlineKind)answers.Choice("underline", UnderlineChoices(request).Count) };
        if (request.Offers(CharacterFeatures.Case) && answers.Has("case")) state = state with { Case = _cases[answers.Choice("case", _cases.Length)] };
        if (request.Offers(CharacterFeatures.Relief) && answers.Has("relief")) state = state with { Relief = _reliefs[answers.Choice("relief", _reliefs.Length)] };
        foreach (var effect in _effects.Where(effect => request.Offers(effect.Feature)))
            state = effect.Set(state, answers.Flag(effect.Key, effect.Get(current)));
        if (request.Offers(CharacterFeatures.Position) && answers.Has("position")) state = state with { Baseline = _positions[answers.Choice("position", _positions.Length)] };
        if (request.Offers(CharacterFeatures.Spacing)) state = state with { LetterSpacing = answers.Number("spacing", current.LetterSpacing) };
        if (request.Offers(CharacterFeatures.Scale)) state = state with { Scale = answers.Number("scale", current.Scale) };
        if (request.Offers(CharacterFeatures.Kerning)) state = state with { Kerning = answers.Flag("kerning", current.Kerning) };
        return state;
    }

    private Avalonia.Controls.Control Preview(IFormValuesFeed feed, CharacterFormatRequest request) =>
        previews.Create(feed, new FeedPreview<CharacterFormatState>(
            values => Read(request, values),
            state =>
            {
                var view = new CharacterSampleView(layout);
                view.Show(state, request.Sample.Length > 0 ? request.Sample : state.Font.Length > 0 ? state.Font : texts["textFormat.character.sample"]);
                return view;
            },
            PulseOffice.UI.Controls.Preview.PreviewBackdrop.Page,
            120));

    private IReadOnlyList<IconChoice> StyleChoices() =>
    [
        new("regular", "text.style.regular", texts["textFormat.character.style.regular"]),
        new("bold", "text.style.bold", texts["textFormat.character.style.bold"]),
        new("italic", "text.style.italic", texts["textFormat.character.style.italic"]),
        new("boldItalic", "text.style.boldItalic", texts["textFormat.character.style.boldItalic"]),
    ];

    private IReadOnlyList<IconChoice> UnderlineChoices(CharacterFormatRequest request)
    {
        var choices = new List<IconChoice>
        {
            new("none", "text.underline.none", texts["textFormat.character.underline.none"]),
            new("single", "text.underline.single", texts["textFormat.character.underline.single"]),
        };
        if (request.Offers(CharacterFeatures.DoubleUnderline)) choices.Add(new("double", "text.underline.double", texts["textFormat.character.underline.double"]));
        return choices;
    }

    private IReadOnlyList<IconChoice> PositionChoices() =>
    [
        new("normal", "text.position.normal", texts["textFormat.character.position.normal"]),
        new("superscript", "text.position.superscript", texts["textFormat.character.position.superscript"]),
        new("subscript", "text.position.subscript", texts["textFormat.character.position.subscript"]),
    ];
}

public sealed class CharacterFormatDialog(ICharacterFormatTabs tabs, ITabbedForms forms) : ICharacterFormatDialog
{
    public async Task<CharacterFormatState?> AskAsync(CharacterFormatRequest request, string titleKey, CancellationToken ct) =>
        await forms.AskAsync(titleKey, tabs.Tabs(request), ct).ConfigureAwait(true) is { } values ? tabs.Read(request, values) : null;
}
