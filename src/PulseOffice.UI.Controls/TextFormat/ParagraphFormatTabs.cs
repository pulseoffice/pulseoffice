using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.TextFormat;

public interface IParagraphFormatTabs
{
    FormTab Tab(ParagraphFormatRequest request);

    ParagraphFormatState Read(ParagraphFormatRequest request, IReadOnlyDictionary<string, object> values);

    Avalonia.Controls.Control Preview(IFormValuesFeed feed, ParagraphFormatRequest request);
}

public interface IParagraphFormatDialog
{
    Task<ParagraphFormatState?> AskAsync(ParagraphFormatRequest request, string titleKey, CancellationToken ct);
}

public sealed class ParagraphFormatTabs(
    IIconCatalog icons,
    ITextFormatPreviews previews,
    IParagraphSampleLayout layout,
    ITextCatalog texts) : IParagraphFormatTabs
{
    private const int CustomLine = 4;

    private readonly TextAlign[] _aligns = [TextAlign.Left, TextAlign.Center, TextAlign.Right, TextAlign.Justify];

    private readonly LineHeightMode[] _modes = [LineHeightMode.Proportional, LineHeightMode.AtLeast, LineHeightMode.Exact, LineHeightMode.Leading];

    private readonly double[] _presets = [100, 115, 150, 200];

    public FormTab Tab(ParagraphFormatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = request.Current;
        var fields = new List<FormField>();
        var alignment = texts["textFormat.section.alignment"];
        var indents = texts["textFormat.section.indents"];
        var spacing = texts["textFormat.section.spacing"];
        if (request.Offers(ParagraphFeatures.Align))
            fields.Add(new IconChoiceField("align", texts["textFormat.paragraph.align"], AlignChoices(), icons, Math.Max(0, Array.IndexOf(_aligns, current.Align))) { Section = alignment });
        if (request.Offers(ParagraphFeatures.Indents))
        {
            fields.Add(new NumberField("left", texts["textFormat.paragraph.left"], current.Left, -1000, 1000) { Unit = "pt", LargeStep = 18, Presets = [0, 18, 36, 54, 72], Section = indents });
            if (request.Offers(ParagraphFeatures.RightIndent))
                fields.Add(new NumberField("right", texts["textFormat.paragraph.right"], current.Right, -1000, 1000) { Unit = "pt", LargeStep = 18, Presets = [0, 18, 36, 54, 72], Section = indents });
            fields.Add(new NumberField("indent", texts["textFormat.paragraph.firstLine"], current.FirstLine, -500, 500) { Unit = "pt", LargeStep = 18, Presets = [-18, 0, 18, 36], Section = indents });
        }
        if (request.Offers(ParagraphFeatures.Spacing))
        {
            fields.Add(new NumberField("before", texts["textFormat.paragraph.before"], current.Before, 0, 500) { Unit = "pt", LargeStep = 6, Presets = [0, 3, 6, 12, 18, 24], Section = spacing });
            fields.Add(new NumberField("after", texts["textFormat.paragraph.after"], current.After, 0, 500) { Unit = "pt", LargeStep = 6, Presets = [0, 3, 6, 8, 12, 18, 24], Section = spacing });
        }
        if (request.Offers(ParagraphFeatures.LineSpacing))
        {
            var preset = current.LineMode == LineHeightMode.Proportional && Array.FindIndex(_presets, value => Math.Abs(value - current.LineValue) < 0.5) is var found and >= 0 ? found : CustomLine;
            fields.Add(new IconChoiceField("linePreset", texts["textFormat.paragraph.lineSpacing"], LineChoices(), icons, preset) { ShowLabels = true, Section = spacing });
            if (request.Offers(ParagraphFeatures.LineModes))
                fields.Add(new ChoiceField("lineMode", texts["textFormat.paragraph.lineMode"], [.. _modes.Select(item => texts[$"textFormat.paragraph.line.{item.ToString().ToLowerInvariant()}"])],
                    Math.Max(0, Array.IndexOf(_modes, current.LineMode))) { Section = spacing, ShownWhen = Custom });
            fields.Add(new NumberField("lineValue", texts["textFormat.paragraph.lineValue"], current.LineValue, 0, 1000)
            {
                Unit = current.LineMode == LineHeightMode.Proportional ? "%" : "pt",
                Presets = current.LineMode == LineHeightMode.Proportional ? _presets : [],
                Section = spacing,
                ShownWhen = Custom,
            });
        }
        if (request.Offers(ParagraphFeatures.Contextual))
            fields.Add(new CheckField("contextual", texts["textFormat.paragraph.contextual"], current.Contextual) { Section = spacing });
        return new FormTab(texts["textFormat.paragraph.tab"], texts["textFormat.paragraph.explanation"], fields) { LivePreview = feed => Preview(feed, request) };
    }

    public ParagraphFormatState Read(ParagraphFormatRequest request, IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(values);
        var answers = new FormAnswers(values);
        var current = request.Current;
        var state = current;
        if (request.Offers(ParagraphFeatures.Align) && answers.Has("align")) state = state with { Align = _aligns[answers.Choice("align", _aligns.Length)] };
        if (request.Offers(ParagraphFeatures.Indents))
        {
            state = state with { Left = answers.Number("left", current.Left), FirstLine = answers.Number("indent", current.FirstLine) };
            if (request.Offers(ParagraphFeatures.RightIndent)) state = state with { Right = answers.Number("right", current.Right) };
        }
        if (request.Offers(ParagraphFeatures.Spacing)) state = state with { Before = answers.Number("before", current.Before), After = answers.Number("after", current.After) };
        if (request.Offers(ParagraphFeatures.LineSpacing) && answers.Has("linePreset"))
        {
            var preset = answers.Choice("linePreset", CustomLine + 1, CustomLine);
            state = preset < CustomLine
                ? state with { LineMode = LineHeightMode.Proportional, LineValue = _presets[preset] }
                : state with
                {
                    LineMode = request.Offers(ParagraphFeatures.LineModes) ? _modes[answers.Choice("lineMode", _modes.Length, Array.IndexOf(_modes, current.LineMode))] : LineHeightMode.Proportional,
                    LineValue = answers.Number("lineValue", current.LineValue),
                };
        }
        if (request.Offers(ParagraphFeatures.Contextual)) state = state with { Contextual = answers.Flag("contextual", current.Contextual) };
        return state;
    }

    public Avalonia.Controls.Control Preview(IFormValuesFeed feed, ParagraphFormatRequest request) =>
        previews.Create(feed, new FeedPreview<ParagraphFormatState>(
            values => Read(request, values),
            state =>
            {
                var view = new ParagraphSampleView(layout);
                view.Show(state);
                return view;
            },
            PulseOffice.UI.Controls.Preview.PreviewBackdrop.Page,
            200));

    private bool Custom(IReadOnlyDictionary<string, object> values) => new FormAnswers(values).Choice("linePreset", CustomLine + 1, CustomLine) == CustomLine;

    private IReadOnlyList<IconChoice> AlignChoices() =>
    [
        .. _aligns.Select(align => new IconChoice(align.ToString(), $"text.align.{align.ToString().ToLowerInvariant()}", texts[$"textFormat.paragraph.align.{align.ToString().ToLowerInvariant()}"])),
    ];

    private IReadOnlyList<IconChoice> LineChoices() =>
    [
        .. _presets.Select(value => new IconChoice(value.ToString(CultureInfo.InvariantCulture), string.Empty, (value / 100).ToString("0.##", CultureInfo.CurrentCulture))),
        new("custom", string.Empty, texts["textFormat.paragraph.line.custom"]),
    ];
}

public sealed class ParagraphFormatDialog(IParagraphFormatTabs tabs, ITabbedForms forms) : IParagraphFormatDialog
{
    public async Task<ParagraphFormatState?> AskAsync(ParagraphFormatRequest request, string titleKey, CancellationToken ct) =>
        await forms.AskAsync(titleKey, [tabs.Tab(request)], ct).ConfigureAwait(true) is { } values ? tabs.Read(request, values) : null;
}
