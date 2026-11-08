using System.Globalization;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFieldCommands(
    IActiveDeckContext context,
    IDeckTextInsertion insertion,
    IDialogService dialogs,
    ITextCatalog texts) : DeckCommandSet(context)
{
    private readonly TextFieldResolver _resolver = new();
    private readonly SlideFieldsFactory _fields = new();

    private readonly IReadOnlyList<FieldChoice> _choices =
    [
        new LiveField("slideNumber", TextFieldKind.SlideNumber),
        new LiveField("slideCount", TextFieldKind.SlideCount),
        new LiveField("slideTitle", TextFieldKind.SlideTitle),
        new LiveField("date", TextFieldKind.Date),
        new LiveField("time", TextFieldKind.Time),
        new FixedField("dateFixed", TextFieldKind.Date),
        new FixedField("timeFixed", TextFieldKind.Time),
        new LiveField("author", TextFieldKind.Author),
        new LiveField("fileName", TextFieldKind.FileName),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
        [Chooser(), Modifier(), .. _choices.Select(Direct)];

    private CommandDefinition Modifier() =>
        Prototype("deck.field.modify") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && FieldElement(t) is not null,
            Execute = (_, ct) => ModifyAsync(ct),
        };

    private TextElement? FieldElement(DeckTarget target) =>
        target.Selected() is TextElement element
        && element.Text.Paragraphs.SelectMany(p => p.Runs).Any(run => run.Field != TextFieldKind.None)
            ? element
            : null;

    private async Task ModifyAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || FieldElement(t) is not { } element || t.CurrentSlide() is not { } slide) return;
        var live = _choices.OfType<LiveField>().ToList();

        var picked = await dialogs.ShowAsync<string>(
            new ChoicePromptView(
                texts["deck.field.modify"], texts["deck.field.chooseHint"],
                [
                    .. live.Select(choice => new PromptChoice(texts[$"deck.field.{choice.Key}"], choice.Key)),
                    new PromptChoice(texts["dialog.cancel"], string.Empty, IsCancel: true),
                ],
                texts),
            new DialogOptions { TitleKey = "command.deck.field.modify.title" },
            ct).ConfigureAwait(true);
        if (live.FirstOrDefault(choice => choice.Key == picked) is not { } chosen || !Holds(t, slide, element)) return;

        var fields = _fields.For(t.Deck, slide);
        var updated = element.Text with
        {
            Paragraphs = [.. element.Text.Paragraphs.Select(paragraph => paragraph.Runs.Count == 0 ? paragraph : paragraph.WithRuns(
                [.. paragraph.Runs.Select(run => run.Field == TextFieldKind.None
                    ? run
                    : Retargeted(run, chosen.Kind, fields))]))],
        };
        t.Actions.EditText(slide, element.Id, updated);
    }

    private TextRun Retargeted(TextRun run, TextFieldKind kind, TextFields fields)
    {
        var changed = run with { Field = kind, FieldDetail = kind == run.Field ? run.FieldDetail : null };
        return changed with { Text = _resolver.Resolve(changed, fields) };
    }

    private CommandDefinition Chooser() =>
        Prototype("deck.insert.field") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && insertion.CanInsert(t),
            Execute = (_, ct) => ChooseAsync(ct),
        };

    private CommandDefinition Direct(FieldChoice choice) =>
        Prototype($"deck.insert.field.{choice.Key}") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && insertion.CanInsert(t),
            Execute = (_, _) =>
            {
                Insert(choice);
                return Task.CompletedTask;
            },
        };

    private async Task ChooseAsync(CancellationToken ct)
    {
        if (Context.Target is null) return;

        var picked = await dialogs.ShowAsync<string>(
            new ChoicePromptView(
                texts["deck.field.choose"], texts["deck.field.chooseHint"],
                [
                    .. _choices.Select(choice => new PromptChoice(texts[$"deck.field.{choice.Key}"], choice.Key)),
                    new PromptChoice(texts["dialog.cancel"], string.Empty, IsCancel: true),
                ],
                texts),
            new DialogOptions { TitleKey = "command.deck.insert.field.title" },
            ct).ConfigureAwait(true);

        if (_choices.FirstOrDefault(choice => choice.Key == picked) is { } chosen) Insert(chosen);
    }

    private void Insert(FieldChoice choice)
    {
        if (Context.Target is not { } t) return;

        var fields = _fields.For(t.Deck, t.CurrentSlide());
        insertion.Insert(t, choice.Run(fields, _resolver), freeFontSize: 20);
    }

    private abstract record FieldChoice(string Key)
    {
        public abstract TextRun Run(TextFields fields, TextFieldResolver resolver);
    }

    private sealed record LiveField(string Key, TextFieldKind Kind) : FieldChoice(Key)
    {
        public override TextRun Run(TextFields fields, TextFieldResolver resolver) =>
            new(resolver.Resolve(Kind, fields)) { Field = Kind };
    }

    private sealed record FixedField(string Key, TextFieldKind Kind) : FieldChoice(Key)
    {
        public override TextRun Run(TextFields fields, TextFieldResolver resolver) => new(resolver.Resolve(Kind, fields));
    }
}
