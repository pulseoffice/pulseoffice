using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxInteractionMapper
{
    private readonly IReadOnlyList<PptxClickAction> _actions =
    [
        new JumpClickAction(InteractionKind.NextSlide, "nextslide"),
        new JumpClickAction(InteractionKind.PreviousSlide, "previousslide"),
        new JumpClickAction(InteractionKind.FirstSlide, "firstslide"),
        new JumpClickAction(InteractionKind.LastSlide, "lastslide"),
        new JumpClickAction(InteractionKind.StopShow, "endshow"),
        new MacroClickAction(),
        new SlideJumpClickAction(),
        new HyperlinkClickAction(),
    ];

    private readonly PptxSounds _sounds = new(new SoundTypes());

    private const string NoAction = "ppaction://noaction";

    internal void Apply(P.NonVisualDrawingProperties nvPr, InteractiveAction action, SlidePart part, PptxWriteSession session)
    {
        if (action is not { Kind: InteractionKind.GoToSlide, Reference: { Length: > 0 } target })
            Apply(nvPr, action, part);
        else
        {
            var jump = new D.HyperlinkOnClick { Id = string.Empty, Action = SlideJumpClickAction.Jump };
            session.PendingSlideLinks.Add((part, jump, target));
            nvPr.RemoveAllChildren<D.HyperlinkOnClick>();
            nvPr.PrependChild(jump);
        }

        if (_sounds.Write(action.SoundPath, part, session) is not { } sound) return;
        if (nvPr.GetFirstChild<D.HyperlinkOnClick>() is not { } link)
        {
            link = new D.HyperlinkOnClick { Id = string.Empty, Action = NoAction };
            nvPr.PrependChild(link);
        }
        link.PrependChild(_sounds.Click(sound));
    }

    public void Apply(P.NonVisualDrawingProperties nvPr, InteractiveAction action, SlidePart part)
    {
        if (_actions.FirstOrDefault(candidate => candidate.Builds(action))?.Build(action, part) is not { } link) return;

        nvPr.RemoveAllChildren<D.HyperlinkOnClick>();
        nvPr.PrependChild(link);
    }

    public InteractiveAction? Read(P.NonVisualDrawingProperties nvPr, SlidePart part, string targetId) =>
        nvPr.GetFirstChild<D.HyperlinkOnClick>() is { } link
            ? _actions.Select(candidate => candidate.Read(link, part, targetId)).FirstOrDefault(read => read is not null)
            : null;
}

internal abstract class PptxClickAction
{
    public abstract bool Builds(InteractiveAction action);

    public abstract D.HyperlinkOnClick Build(InteractiveAction action, SlidePart part);

    public abstract InteractiveAction? Read(D.HyperlinkOnClick link, SlidePart part, string targetId);
}

internal sealed class JumpClickAction(InteractionKind kind, string jump) : PptxClickAction
{
    private string Action => $"ppaction://hlinkshowjump?jump={jump}";

    public override bool Builds(InteractiveAction action) => action.Kind == kind;

    public override D.HyperlinkOnClick Build(InteractiveAction action, SlidePart part) => new() { Id = string.Empty, Action = Action };

    public override InteractiveAction? Read(D.HyperlinkOnClick link, SlidePart part, string targetId) =>
        link.Action?.Value is { } action && action.StartsWith("ppaction://hlinkshowjump?", StringComparison.Ordinal)
        && action[(action.IndexOf('?', StringComparison.Ordinal) + 1)..].Split('&').Contains("jump=" + jump, StringComparer.Ordinal)
            ? new InteractiveAction { TargetId = targetId, Kind = kind }
            : null;
}

internal sealed class SlideJumpClickAction : PptxClickAction
{
    public const string Jump = "ppaction://hlinksldjump";

    private readonly PptxSlidePageNames _names = new();

    public override bool Builds(InteractiveAction action) => false;

    public override D.HyperlinkOnClick Build(InteractiveAction action, SlidePart part) => throw new NotSupportedException();

    public override InteractiveAction? Read(D.HyperlinkOnClick link, SlidePart part, string targetId)
    {
        if (link.Action?.Value != Jump || link.Id?.Value is not { Length: > 0 } relationship
            || !part.TryGetPartById(relationship, out var found) || found is not SlidePart target
            || part.GetParentParts().OfType<PresentationPart>().FirstOrDefault() is not { } presentation)
            return null;
        return new InteractiveAction { TargetId = targetId, Kind = InteractionKind.GoToSlide, Reference = _names.PageName(target, presentation) };
    }
}

internal sealed class MacroClickAction : PptxClickAction
{
    private const string Prefix = "ppaction://macro";
    private const string Name = "name=";

    public override bool Builds(InteractiveAction action) => action is { Kind: InteractionKind.RunMacro, Reference: not null };

    public override D.HyperlinkOnClick Build(InteractiveAction action, SlidePart part) =>
        new() { Id = string.Empty, Action = $"{Prefix}?{Name}{action.Reference}" };

    public override InteractiveAction? Read(D.HyperlinkOnClick link, SlidePart part, string targetId)
    {
        if (link.Action?.Value is not { } action || !action.StartsWith(Prefix, StringComparison.Ordinal)) return null;

        var at = action.IndexOf(Name, StringComparison.Ordinal);
        return new InteractiveAction { TargetId = targetId, Kind = InteractionKind.RunMacro, Reference = at < 0 ? null : action[(at + Name.Length)..] };
    }
}

internal sealed class HyperlinkClickAction : PptxClickAction
{
    private const string FileLink = "ppaction://hlinkfile";
    private const string PresentationLink = "ppaction://hlinkpres";

    private readonly HyperlinkRelationshipIndex _relationships = new();

    public override bool Builds(InteractiveAction action) => action is { Kind: InteractionKind.Hyperlink, Reference: not null };

    public override D.HyperlinkOnClick Build(InteractiveAction action, SlidePart part) =>
        new() { Id = part.AddHyperlinkRelationship(Target(action.Reference!), true).Id };

    private Uri Target(string reference) =>
        Uri.TryCreate(reference, UriKind.RelativeOrAbsolute, out var target) ? target : new Uri(Uri.EscapeDataString(reference), UriKind.Relative);

    public override InteractiveAction? Read(D.HyperlinkOnClick link, SlidePart part, string targetId) =>
        Plain(link.Action?.Value) && link.Id?.Value is { Length: > 0 } relationship
            ? new InteractiveAction
            {
                TargetId = targetId,
                Kind = InteractionKind.Hyperlink,
                Reference = Reference(_relationships.Of(part, relationship)?.Uri),
            }
            : null;

    private string? Reference(Uri? target) => target is null ? null : target.IsAbsoluteUri ? target.OriginalString : target.ToString();

    private bool Plain(string? action) =>
        string.IsNullOrEmpty(action) || action == FileLink || action.StartsWith(PresentationLink, StringComparison.Ordinal);
}
