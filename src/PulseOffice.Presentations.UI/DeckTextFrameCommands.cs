using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckTextFrameCommands(
    IActiveDeckContext context,
    IRichTextFormatter formatter,
    IDialogService dialogs,
    ITextCatalog texts) : DeckCommandSet(context)
{
    private const double PointsPerCentimetre = 72 / 2.54;

    private static readonly TextAnchor[] Anchors = [TextAnchor.Top, TextAnchor.Middle, TextAnchor.Bottom, TextAnchor.Justified];

    private static readonly TextAutofit[] Fits = [TextAutofit.None, TextAutofit.ShrinkText, TextAutofit.ResizeShape];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Anchor("deck.paragraph.anchorTop", TextAnchor.Top),
        Anchor("deck.paragraph.anchorMiddle", TextAnchor.Middle),
        Anchor("deck.paragraph.anchorBottom", TextAnchor.Bottom),
        Editable("deck.format.textFrame", CommandSurface.Advanced) with { Execute = (_, ct) => FrameAsync(ct) },
    ];

    private CommandDefinition Anchor(string id, TextAnchor anchor) =>
        Editable(id, CommandSurface.Common) with
        {
            EnabledWhen = _ => Context.Target is { } t && t.HasTextTarget(),
            CheckedWhen = _ => Frame()?.Anchor == anchor,
            Execute = (_, _) =>
            {
                Context.Target?.ApplyText((text, _) => formatter.SetAnchor(text, anchor));
                return Task.CompletedTask;
            },
        };

    private CommandDefinition Editable(string id, CommandSurface surface) =>
        Prototype(id) with
        {
            Surface = surface,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.HasTextTarget(),
        };

    private TextFrame? Frame() => Context.Target?.TextTarget()?.Text.Frame;

    private async Task FrameAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;

        var text = t.TextTarget()?.Text;
        var frame = text?.Frame ?? TextFrame.Default;
        var paragraph = text is null ? null : formatter.ReadParagraph(text, t.TextRange());

        var fields = new List<FormField>
        {
            new ChoiceField("anchor", texts["deck.textFrame.anchor"],
                [.. Anchors.Select(anchor => texts[$"deck.textFrame.anchor.{anchor}"])], Math.Max(Array.IndexOf(Anchors, frame.Anchor), 0)),
            new ChoiceField("autofit", texts["deck.textFrame.autofit"],
                [.. Fits.Select(fit => texts[$"deck.textFrame.autofit.{fit}"])], Math.Max(Array.IndexOf(Fits, frame.Autofit), 0)),
            new CheckField("wrap", texts["deck.textFrame.wrap"], frame.WordWrap),
            Centimetres("left", "deck.textFrame.insetLeft", frame.Insets.Left, 0),
            Centimetres("top", "deck.textFrame.insetTop", frame.Insets.Top, 0),
            Centimetres("right", "deck.textFrame.insetRight", frame.Insets.Right, 0),
            Centimetres("bottom", "deck.textFrame.insetBottom", frame.Insets.Bottom, 0),
            Centimetres("before", "deck.textFrame.beforeText", paragraph?.MarginLeft ?? 0, 0),
            Centimetres("first", "deck.textFrame.firstLine", paragraph?.Indent ?? 0, -20),
        };

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.textFrame.explanation"], fields, texts),
            new DialogOptions { TitleKey = "command.deck.format.textFrame.title", PreferredWidth = 460 }, ct).ConfigureAwait(true);
        if (values is null) return;

        var chosen = frame with
        {
            Anchor = Anchors[Index(values, "anchor", Anchors.Length)],
            Autofit = Fits[Index(values, "autofit", Fits.Length)],
            WordWrap = values["wrap"] is true,
            Insets = new TextInsets(Points(values, "left"), Points(values, "top"), Points(values, "right"), Points(values, "bottom")),
        };
        var before = Points(values, "before");
        var first = Points(values, "first");
        var indentsChanged = paragraph is null
            || Math.Abs(before - (paragraph.MarginLeft ?? 0)) > 0.05 || Math.Abs(first - (paragraph.Indent ?? 0)) > 0.05;

        t.ApplyText((current, range) =>
        {
            var result = formatter.SetFrame(current, chosen);
            return indentsChanged ? formatter.SetIndents(result, range, before, first) : result;
        });
    }

    private NumberField Centimetres(string key, string label, double points, double minimum) =>
        new(key, texts[label], Math.Round(points / PointsPerCentimetre, 2), minimum, 20, 0.05);

    private double Points(IReadOnlyDictionary<string, object> values, string key) =>
        Convert.ToDouble(values[key], System.Globalization.CultureInfo.InvariantCulture) * PointsPerCentimetre;

    private int Index(IReadOnlyDictionary<string, object> values, string key, int count) =>
        values[key] is int i && i >= 0 && i < count ? i : 0;
}
