using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFormControlCommands(IActiveDeckContext context, IFormControlCatalog catalog, IFormControlNaming naming) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() => Enum.GetValues<FormControlKind>().Select(Control);

    private static string Slug(FormControlKind kind) =>
        char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private CommandDefinition Control(FormControlKind kind) =>
        Prototype($"deck.control.{Slug(kind)}") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                {
                    var descriptor = catalog.Describe(kind);
                    var control = new FormControlObject
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ControlKind = kind,
                        NaturalSize = descriptor.DefaultSize,
                        Properties = catalog.Create(kind, naming.NextName(kind, t.Deck.Slides
                            .SelectMany(each => each.Elements).OfType<ObjectElement>()
                            .Select(each => each.Content).OfType<FormControlObject>()
                            .Select(each => each.Properties.Name))),
                    };

                    var w = descriptor.DefaultSize.Width;
                    var h = descriptor.DefaultSize.Height;
                    var x = new Twip((t.Deck.Size.Width.Value - w.Value) / 2);
                    var y = new Twip((t.Deck.Size.Height.Value - h.Value) / 2);

                    var element = new ObjectElement
                    {
                        Id = control.Id,
                        Bounds = new DocumentRect(x, y, w, h),
                        ZOrder = slide.Elements.Count,
                        Content = control,
                    };
                    t.Actions.InsertObject(slide, element);
                    t.Select([element]);
                }
                return Task.CompletedTask;
            },
        };
}
