using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Data.UI;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFormFieldCommands(
    IActiveDeckContext context,
    IDataImportFlow data,
    IFormControlCatalog catalog,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Text.Rich.ITextCultures cultures) : DeckCommandSet(context)
{
    private const int MaxFields = 12;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.form.addField") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.CurrentSlide() is not null,
            Execute = (_, ct) => AddAsync(ct),
        },
    ];

    private async Task AddAsync(CancellationToken ct)
    {
        if (await data.ChooseAsync(ct).ConfigureAwait(true) is not { } choice) return;
        var columns = choice.Preview.Columns;
        if (columns.Count == 0) return;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts.Format("form.field.explanation", choice.DisplayName),
                [.. columns.Take(MaxFields).Select((column, index) => (FormField)new CheckField($"c{index}", column.Name, true))],
                texts),
            new DialogOptions { TitleKey = "command.deck.form.addField.title", PreferredWidth = 460 }, ct).ConfigureAwait(true);
        if (values is null) return;
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        var chosen = columns.Take(MaxFields).Select((column, index) => (column, index))
            .Where(entry => values.TryGetValue($"c{entry.index}", out var on) && on is true).ToList();
        if (chosen.Count == 0) return;

        var first = choice.Preview.Rows.FirstOrDefault();
        var labelWidth = Twip.FromCentimeters(4);
        var fieldWidth = Twip.FromCentimeters(8);
        var rowHeight = Twip.FromCentimeters(0.9);
        var gap = Twip.FromCentimeters(0.3);
        var left = new Twip((t.Deck.Size.Width.Value - labelWidth.Value - gap.Value - fieldWidth.Value) / 2);
        var top = new Twip(Math.Max(0, (t.Deck.Size.Height.Value - chosen.Count * (rowHeight.Value + gap.Value)) / 2));
        var z = slide.Elements.Count == 0 ? 0 : slide.Elements.Max(element => element.ZOrder) + 1;

        var elements = new List<ObjectElement>();
        for (var row = 0; row < chosen.Count; row++)
        {
            var (column, index) = chosen[row];
            var y = new Twip(top.Value + row * (rowHeight.Value + gap.Value));
            var text = first is not null && index < first.Count ? Format(first[index], cultures.For(t.Deck.TextLanguage)) : null;

            elements.Add(Control(FormControlKind.Label, $"label{column.Name}",
                new DocumentRect(left, y, labelWidth, rowHeight), z++,
                properties => properties with { Label = column.Name }));
            elements.Add(Control(FormControlKind.TextBox, column.Name,
                new DocumentRect(new Twip(left.Value + labelWidth.Value + gap.Value), y, fieldWidth, rowHeight), z++,
                properties => properties with { DataField = column.Name, Value = properties.Value with { Text = text } }));
        }

        t.Actions.AddFormFields(slide, elements, t.Deck.FormSettings with
        {
            DataSource = choice.Query.ConnectionId,
            Command = choice.Query.Sql,
        });
        t.Select(elements);
        status.Report(texts.Format("form.field.added", chosen.Count), NotificationSeverity.Success);
    }

    private ObjectElement Control(FormControlKind kind, string name, DocumentRect bounds, int z, Func<FormControlProperties, FormControlProperties> shape)
    {
        var control = new FormControlObject
        {
            Id = Guid.NewGuid().ToString("N"),
            ControlKind = kind,
            NaturalSize = new DocumentSize(bounds.Width, bounds.Height),
            Properties = shape(catalog.Create(kind, name)),
        };
        return new ObjectElement { Id = control.Id, Bounds = bounds, ZOrder = z, Content = control };
    }

    private string? Format(object? value, CultureInfo culture) => value switch
    {
        null => null,
        IFormattable formattable => formattable.ToString(null, culture),
        _ => value.ToString(),
    };
}
