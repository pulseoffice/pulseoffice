using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFontworkCommands(
    IActiveDeckContext context,
    PulseOffice.UI.Controls.Shapes.IFontworkPrompt prompt) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.insert.fontwork") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.CurrentSlide() is not null,
            Execute = (_, ct) => InsertAsync(ct),
        },
        Prototype("deck.format.fontwork") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is TextElement { Text.IsEmpty: false }
                or ObjectElement { Content: FontworkObject or ShapeObject { Text.Length: > 0 } },
            Execute = (_, ct) => AlongPathAsync(ct),
        },
    ];

    private async Task InsertAsync(CancellationToken ct)
    {
        if (await prompt.CreateAsync("command.deck.insert.fontwork.title", ct).ConfigureAwait(true) is not { } fontwork) return;
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        var width = Twip.FromCentimeters(16);
        var height = Twip.FromCentimeters(6);
        var element = new ObjectElement
        {
            Id = fontwork.Id,
            Bounds = new DocumentRect(new Twip((t.Deck.Size.Width.Value - width.Value) / 2), new Twip((t.Deck.Size.Height.Value - height.Value) / 2), width, height),
            ZOrder = slide.Elements.Count,
            Content = fontwork,
        };
        t.Actions.InsertObject(slide, element);
        t.Select([element]);
    }

    private async Task AlongPathAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide || t.Selected() is not { } selected) return;
        var current = selected is ObjectElement { Content: FontworkObject existing } ? existing.Warp : FontworkWarp.ArchUp;
        if (await prompt.ChooseWarpAsync("command.deck.format.fontwork.title", current, ct).ConfigureAwait(true) is not { } warp) return;

        FontworkObject fontwork = selected switch
        {
            ObjectElement { Content: FontworkObject art } => art with { Warp = warp },
            ObjectElement { Content: ShapeObject shape } => new FontworkObject
            {
                Id = shape.Id, Name = shape.Name, Text = shape.Text ?? string.Empty, Warp = warp,
                Style = shape.Style with { Fill = shape.Style.Fill.IsVisible ? shape.Style.Fill : ShapeFill.Default },
            },
            TextElement text => new FontworkObject { Id = text.Id, Text = text.Text.PlainText.Replace('\n', ' '), Warp = warp },
            _ => throw new InvalidOperationException(),
        };
        var replacement = new ObjectElement { Id = selected.Id, Bounds = selected.Bounds, ZOrder = selected.ZOrder, RotationDegrees = selected.RotationDegrees, Content = fontwork };
        t.Actions.ReplaceElement(slide, replacement, "command.deck.format.fontwork.title");
        t.Select([replacement]);
    }
}
