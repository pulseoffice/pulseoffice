using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class PlaceholderRoles(OdfFreeTextShape freeText)
{
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;

    public IReadOnlyList<PlaceholderRole> Assign(IReadOnlyList<XElement> frames) => [.. frames.Select(Assign)];

    public SlideLayout Infer(IReadOnlyList<PlaceholderRole> roles)
    {
        var bodies = roles.Count(r => r == PlaceholderRole.Body);
        if (bodies >= 2) return SlideLayout.TwoContent;
        if (bodies == 1) return SlideLayout.TitleAndContent;
        return roles.Any(role => role != PlaceholderRole.Free) ? SlideLayout.TitleSlide : SlideLayout.Blank;
    }

    private PlaceholderRole Assign(XElement frame) =>
        !freeText.Is(frame) && (string?)frame.Attribute(Presentation + "class") is { } declared ? FromClass(declared) : PlaceholderRole.Free;

    private PlaceholderRole FromClass(string value) => value switch
    {
        "title" => PlaceholderRole.Title,
        "subtitle" => PlaceholderRole.Subtitle,
        "outline" or "notes" => PlaceholderRole.Body,
        _ => PlaceholderRole.Free,
    };
}

internal sealed class TextFrameReader(OdfInteractionMapper interactions, OdfFreeTextShape freeText, IOdfParagraphReader paragraphs) : OdpPageReader
{
    private readonly OdfTextBackdrops _backdrops = new(new OdfShapeStyleReader());

    private readonly OdpElementRotation _rotation = new();

    public IReadOnlyList<XElement> Frames(XElement page) =>
        [.. page.Elements()
            .Where(frame => (frame.Name == Draw + "frame" && frame.Element(Draw + "text-box") is not null) || freeText.Is(frame))
            .Where(frame => (string?)frame.Attribute(Presentation + "class") is not ("date-time" or "footer" or "page-number" or "header"))];

    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        var frames = Frames(page);

        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            var role = assembly.Roles[index];
            var id = assembly.NextId(role.ToString().ToLowerInvariant());

            assembly.Remember((string?)frame.Attribute(XmlId) ?? (string?)frame.Attribute(Draw + "id"), id);
            assembly.Add(_rotation.Read(frame, new TextElement
            {
                Id = id,
                Bounds = context.Values.Bounds(frame),
                ZOrder = Position(frame),
                Text = new RichText(role, paragraphs.Read(frame, context.Styles.Text, context.Styles.Presentation.Levels(frame)))
                {
                    Frame = context.Styles.Frames.Of(frame).ToFrame(TextFrame.Default),
                },
                Backdrop = _backdrops.Read(frame, context.Styles.Drawings, context.Styles.Tables.FrameStyles) is { } backdrop
                    ? backdrop with { Outline = freeText.Outline(frame) }
                    : null,
            }));

            if (interactions.Read(frame, id) is { } action) assembly.Slide.AddInteraction(action);
        }
    }
}
