using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.OpenXml;

internal interface IPptxReadLoss
{
    Diagnostic Loss { get; }

    bool Present(PresentationPart presentation);
}

internal sealed class PptxEquations : IPptxReadLoss
{
    private const string Drawing2010 = "http://schemas.microsoft.com/office/drawing/2010/main";

    public Diagnostic Loss { get; } = Diagnostic.Warning("PPTX183",
        "Equations in the text of PowerPoint shapes are imported as the picture PowerPoint stores beside them: they can no longer be edited as equations.");

    public bool Present(PresentationPart presentation) =>
        presentation.SlideParts.Any(slide => slide.Slide?.Descendants().Any(element => element.LocalName == "m" && element.NamespaceUri == Drawing2010) == true);
}

internal sealed class PptxUnknownPresets(UnknownPresetShapes shapes) : IPptxReadLoss
{
    public Diagnostic Loss { get; } = Diagnostic.Warning("PPTX184",
        "Shapes with a preset geometry name that is not part of the DrawingML standard are imported as rectangles.");

    public bool Present(PresentationPart presentation) =>
        presentation.SlideParts.Any(slide => shapes.Present(slide.Slide));
}

internal sealed class PptxUnsupportedClickActions : IPptxReadLoss
{
    private readonly IReadOnlyList<string> _prefixes = ["ppaction://program", "ppaction://ole"];

    public Diagnostic Loss { get; } = Diagnostic.Warning("PPTX185",
        "Click actions that run a program or an object verb are not imported: Deck never starts external programs from a presentation.");

    public bool Present(PresentationPart presentation) =>
        presentation.SlideParts.Any(slide => slide.Slide?.Descendants<DocumentFormat.OpenXml.Drawing.HyperlinkOnClick>()
            .Any(link => link.Action?.Value is { } action && _prefixes.Any(prefix => action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) == true);
}

internal sealed class PptxCommentThreads : IPptxReadLoss
{
    public Diagnostic Loss { get; } = Diagnostic.Info("PPTX223",
        "Some modern PowerPoint comments have replies or a resolved state: Deck slide comments have no threads, so the replies are joined to the comment text with their authors and the resolved state is lost.");

    public bool Present(PresentationPart presentation) =>
        presentation.SlideParts.Any(slide => slide.GetPartsOfType<PowerPointCommentPart>()
            .Any(part => part.CommentList?.Elements<DocumentFormat.OpenXml.Office2021.PowerPoint.Comment.Comment>()
                .Any(comment => comment.GetFirstChild<DocumentFormat.OpenXml.Office2021.PowerPoint.Comment.CommentReplyList>()?.HasChildren == true
                    || comment.Status?.Value == DocumentFormat.OpenXml.Office2021.PowerPoint.Comment.CommentStatus.Resolved) == true));
}

internal sealed class PptxMixedTableRuns : IPptxReadLoss
{
    public Diagnostic Loss { get; } = Diagnostic.Info("PPTX225",
        "Some table cells have text runs with different formatting: a Deck table cell has one look, so it takes the format of its first run.");

    public bool Present(PresentationPart presentation) =>
        presentation.SlideParts.Any(slide => slide.Slide?.Descendants<DocumentFormat.OpenXml.Drawing.TableCell>().Any(Mixed) == true);

    private bool Mixed(DocumentFormat.OpenXml.Drawing.TableCell cell) =>
        cell.TextBody?.Descendants<DocumentFormat.OpenXml.Drawing.Run>()
            .Where(run => !string.IsNullOrEmpty(run.Text?.Text))
            .Select(run => Key(run.RunProperties))
            .Distinct(StringComparer.Ordinal)
            .Skip(1).Any() == true;

    private string Key(DocumentFormat.OpenXml.Drawing.RunProperties? run) => string.Join('|',
        run?.Bold?.Value == true, run?.Italic?.Value == true,
        run?.Underline?.InnerText is { Length: > 0 } underline ? underline : "none",
        run?.Strike?.InnerText is { Length: > 0 } strike ? strike : "noStrike",
        run?.FontSize?.Value, run?.GetFirstChild<DocumentFormat.OpenXml.Drawing.SolidFill>()?.OuterXml,
        run?.GetFirstChild<DocumentFormat.OpenXml.Drawing.LatinFont>()?.Typeface?.Value);
}
