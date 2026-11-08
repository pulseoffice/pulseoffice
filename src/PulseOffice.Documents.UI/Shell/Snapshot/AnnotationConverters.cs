using System.Text.Json;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public sealed class CommentConverter : SnapshotConverter<Comment>
{
    protected override Comment Read(SnapshotReader node) =>
        new(node.Require<string>("id"), node.Get("author", string.Empty), node.Get<DateTimeOffset>("date"), node.Get("content", new BlockContainer()))
        {
            Initials = node.Get<string>("initials"),
            ParentId = node.Get<string>("parent"),
            Resolved = node.Get("resolved", false),
        };

    protected override void Write(Comment value, SnapshotWriter node) =>
        node.Put("id", value.Id).Put("author", value.Author).Put("date", value.Date).Put("content", value.Content)
            .Put("initials", value.Initials).Put("parent", value.ParentId).Put("resolved", value.Resolved);
}

public sealed class NoteConverter : SnapshotConverter<Note>
{
    protected override Note Read(SnapshotReader node) =>
        new(node.Require<string>("id"), node.Get("kind", NoteKind.Footnote), node.Get("content", new BlockContainer())) { Citation = node.Get<string>("citation") };

    protected override void Write(Note value, SnapshotWriter node) =>
        node.Put("id", value.Id).Put("kind", value.Kind).Put("content", value.Content).Put("citation", value.Citation);
}

public sealed class TrackedChangeConverter : SnapshotConverter<TrackedChange>
{
    protected override TrackedChange Read(SnapshotReader node)
    {
        var id = node.Require<string>("id");
        var author = node.Get("author", string.Empty);
        var date = node.Get<DateTimeOffset>("date");
        TrackedChange change = node.Require<ChangeKind>("change") switch
        {
            ChangeKind.Insertion => new Insertion(id, author, date),
            ChangeKind.Deletion => new Deletion(id, author, date, node.Get("removed", new BlockContainer())),
            ChangeKind.Format => new FormatChange(id, author, date, node.Get<List<TextRun>>("previousRuns", [])),
            ChangeKind.ParagraphFormat => new ParagraphFormatChange(id, author, date, node.Require<TextParagraph>("previous")) { PreviousKnown = node.Get("previousKnown", true) },
            var other => throw new JsonException($"Unknown change kind '{other}'."),
        };
        change.Comment = node.Get<string>("comment");
        return change;
    }

    protected override void Write(TrackedChange value, SnapshotWriter node)
    {
        node.Put("change", value.Kind).Put("id", value.Id).Put("author", value.Author).Put("date", value.Date).Put("comment", value.Comment);
        switch (value)
        {
            case Deletion deletion:
                node.Put("removed", deletion.Removed);
                break;
            case FormatChange format:
                node.Put("previousRuns", format.PreviousRuns);
                break;
            case ParagraphFormatChange paragraph:
                node.Put("previous", paragraph.Previous).Put("previousKnown", paragraph.PreviousKnown);
                break;
        }
    }
}

public sealed class AnchoredObjectConverter : SnapshotConverter<AnchoredObject>
{
    protected override AnchoredObject Read(SnapshotReader node) =>
        new(node.Require<string>("id"), node.Require<ObjectAnchor>("anchor"), node.Require<AnchoredContent>("content"), node.Get<DocumentSize>("size"))
        {
            Placement = node.Get("placement", new ObjectPlacement(Twip.Zero, Twip.Zero)),
            Wrap = node.Get("wrap", WrapMode.Parallel),
            WrapSpacing = node.Get("wrapSpacing", Margins.Zero),
            StyleName = node.Get<string>("style"),
            Name = node.Get<string>("name"),
            ZOrder = node.Get("z", 0),
            RotationDegrees = node.Get("rotation", 0.0),
            InBackground = node.Get("background", false),
            WrapContour = node.Get("contourWrap", false),
            Contour = node.Get<ObjectContour>("contour"),
            WrapFirstParagraph = node.Get("firstParagraph", false),
            FlipHorizontal = node.Get("flipH", false),
            FlipVertical = node.Get("flipV", false),
            Hyperlink = node.Get<string>("hyperlink"),
        };

    protected override void Write(AnchoredObject value, SnapshotWriter node) =>
        node.Put("id", value.Id).Put("anchor", value.Anchor).Put("content", value.Content).Put("size", value.Size)
            .Put("placement", value.Placement).Put("wrap", value.Wrap).Put("wrapSpacing", value.WrapSpacing).Put("style", value.StyleName)
            .Put("name", value.Name).Put("z", value.ZOrder).Put("rotation", value.RotationDegrees).Put("background", value.InBackground)
            .Put("contourWrap", value.WrapContour).Put("contour", value.Contour).Put("firstParagraph", value.WrapFirstParagraph)
            .Put("flipH", value.FlipHorizontal).Put("flipV", value.FlipVertical).Put("hyperlink", value.Hyperlink);
}

public sealed class AnchoredContentConverter : SnapshotConverter<AnchoredContent>
{
    private const string Embedded = "embedded";
    private const string TextBox = "textBox";

    protected override AnchoredContent Read(SnapshotReader node) => node.Require<string>("kind") switch
    {
        Embedded => new EmbeddedContent(node.Require<EmbeddedObject>("value"), node.Get<BlockContainer>("story"),
            node.Get<Dictionary<string, BlockContainer>>("members") is { } members ? new Dictionary<string, BlockContainer>(members, StringComparer.Ordinal) : null),
        TextBox => new TextBoxContent(node.Get("content", new BlockContainer()))
        {
            AutoGrowHeight = node.Get("autoGrow", true),
            NextFrameName = node.Get<string>("next"),
            Look = node.Get<PulseOffice.Documents.Styles.FrameProperties>("look"),
        },
        var other => throw new JsonException($"Unknown anchored content '{other}'."),
    };

    protected override void Write(AnchoredContent value, SnapshotWriter node)
    {
        switch (value)
        {
            case EmbeddedContent embedded:
                node.Put("kind", Embedded).Put("value", embedded.Value).Put("story", embedded.Story)
                    .Put("members", embedded.MemberStories.Count == 0 ? null : embedded.MemberStories.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
                break;
            case TextBoxContent box:
                node.Put("kind", TextBox).Put("content", box.Content).Put("autoGrow", box.AutoGrowHeight).Put("next", box.NextFrameName).Put("look", box.Look);
                break;
            default:
                throw new JsonException($"Anchored content {value.GetType().Name} cannot be stored.");
        }
    }
}
