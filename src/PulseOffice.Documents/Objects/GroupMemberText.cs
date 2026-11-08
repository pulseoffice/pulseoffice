using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Documents.Objects;

public interface IGroupMemberText
{
    EmbeddedObject Painted(EmbeddedContent content);
}

public sealed class GroupMemberText : IGroupMemberText
{
    public EmbeddedObject Painted(EmbeddedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.Value is ShapeGroupObject group && content.MemberStories.Count > 0
            ? WithoutOwnText(group, content.MemberStories)
            : content.Value;
    }

    private ShapeGroupObject WithoutOwnText(ShapeGroupObject group, IReadOnlyDictionary<string, BlockContainer> stories) => group with
    {
        Children = [.. group.Children.Select(child => child with
        {
            Content = child.Content switch
            {
                ShapeGroupObject nested => WithoutOwnText(nested, stories),
                ShapeObject shape when stories.ContainsKey(shape.Id) => shape with { Text = null, Runs = null },
                var other => other,
            },
        })],
    };
}
