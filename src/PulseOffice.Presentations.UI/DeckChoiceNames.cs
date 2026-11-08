using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed record DeckNamedChoice<T>(T Value, string Name)
{
    public override string ToString() => Name;
}

public interface IDeckChoiceNames
{
    IReadOnlyList<DeckNamedChoice<SlideLayout>> Layouts();

    IReadOnlyList<DeckNamedChoice<ColorRole>> ColorRoles();

    string Layout(SlideLayout kind);
}

public sealed class DeckChoiceNames(ITextCatalog texts) : IDeckChoiceNames
{
    public IReadOnlyList<DeckNamedChoice<SlideLayout>> Layouts() =>
        [.. Enum.GetValues<SlideLayout>().Select(layout => new DeckNamedChoice<SlideLayout>(layout, texts[$"deck.layout.{Slug(layout)}"]))];

    public string Layout(SlideLayout kind) => texts[$"deck.layout.{Slug(kind)}"];

    public IReadOnlyList<DeckNamedChoice<ColorRole>> ColorRoles() =>
        [.. Enum.GetValues<ColorRole>().Select(role => new DeckNamedChoice<ColorRole>(role, texts[$"deck.colorRole.{Slug(role)}"]))];

    private string Slug<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
