using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxSlideRunLinkReader(SlidePart slide, PresentationPart presentation) : IDrawingRunLinks
{
    private readonly PptxSlidePageNames names = new();
    private readonly HyperlinkTargets _targets = new();

    private const string SlideJump = "ppaction://hlinksldjump";
    private const string FileLink = "ppaction://hlinkfile";
    private const string PresentationLink = "ppaction://hlinkpres";

    public string? Read(D.HyperlinkOnClick? link)
    {
        if (link?.Id?.Value is not { Length: > 0 } relationship) return null;
        var action = link.Action?.Value;
        if (string.IsNullOrEmpty(action) || action == FileLink || action.StartsWith(PresentationLink, StringComparison.Ordinal))
            return _targets.Of(slide, relationship);
        if (action != SlideJump || !slide.TryGetPartById(relationship, out var target) || target is not SlidePart targetSlide) return null;
        return "#" + names.PageName(targetSlide, presentation);
    }

    public D.HyperlinkOnClick? Build(string target) => null;
}

internal sealed class PptxSlidePageNames
{
    public string PageName(SlidePart target, PresentationPart presentation)
    {
        if (target.Slide?.CommonSlideData?.Name?.Value is { Length: > 0 } name) return name;
        var position = 0;
        foreach (var entry in presentation.Presentation?.SlideIdList?.Elements<P.SlideId>() ?? [])
        {
            if (entry.RelationshipId?.Value is not { } id || !presentation.TryGetPartById(id, out var part) || part is not SlidePart) continue;
            position++;
            if (ReferenceEquals(part, target)) break;
        }
        return $"slide{position}";
    }
}

internal sealed class PptxSlideRunLinks(SlidePart slide, PptxWriteSession session) : IDrawingRunLinks
{
    public string? Read(D.HyperlinkOnClick? link) => null;

    public D.HyperlinkOnClick? Build(string target)
    {
        if (target.StartsWith('#'))
        {
            var jump = new D.HyperlinkOnClick { Id = string.Empty, Action = "ppaction://hlinksldjump" };
            session.PendingSlideLinks.Add((slide, jump, target[1..]));
            return jump;
        }
        return Uri.TryCreate(target, UriKind.RelativeOrAbsolute, out var uri)
            ? new D.HyperlinkOnClick { Id = slide.AddHyperlinkRelationship(uri, true).Id }
            : null;
    }
}

internal sealed class PptxSlideLinkResolver(ISlideTargetResolver targets)
{
    public void Resolve(PptxWriteSession session)
    {
        foreach (var (source, link, target) in session.PendingSlideLinks)
        {
            var slide = targets.Find(session.Deck, target);
            if (slide is not null && session.RelationshipBySlide.TryGetValue(slide.Id, out var relationship)
                && session.Part.GetPartById(relationship) is SlidePart destination)
                link.Id = source.CreateRelationshipToPart(destination);
            else
                link.Remove();
        }
        session.PendingSlideLinks.Clear();
    }
}
