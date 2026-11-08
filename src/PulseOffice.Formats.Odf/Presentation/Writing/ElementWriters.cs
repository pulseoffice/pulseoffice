using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class TextFrameWriter(
    OdfUnits units,
    IOdfRichTextWriter text,
    InteractionListenerWriter interactions,
    OdfTextFrameWriter frames,
    OdfFreeTextShape freeText) : OdpXml
{
    private readonly OdfTextBackdrops _backdrops = new(new OdfShapeStyleReader());

    public XElement Write(TextElement element, Slide slide, OdpWriteSession session)
    {
        var style = session.NextStyle("gr");
        var graphic = new XElement(Style + "graphic-properties",
            new XAttribute(Draw + "fill", "none"),
            new XAttribute(Draw + "stroke", "none"),
            frames.Attributes(element.Text.Frame, element.Bounds.Height));
        if (element.Backdrop is { } backdrop) _backdrops.Write(graphic, backdrop, style, session.ShapeStyles);
        session.AddStyle(new XElement(Style + "style",
            new XAttribute(Style + "name", style),
            new XAttribute(Style + "family", "graphic"),
            graphic));

        var shape = freeText.Applies(element) || freeText.Outlined(element);
        var frame = new XElement(Draw + (shape ? "custom-shape" : "frame"),
            new XAttribute(Draw + "style-name", style),
            new XAttribute(Xml + "id", element.Id),
            new XAttribute(Draw + "id", element.Id),
            shape || ClassOf(element.Text.Role) is not { } presentationClass ? null : new XAttribute(Presentation + "class", presentationClass),
            units.Bounds(element.Bounds, Svg));

        if (shape)
        {
            frame.Add(Box(element, session).Nodes(), freeText.Geometry(element));
        }
        else
        {
            frame.Add(Box(element, session));
        }

        interactions.Attach(frame, slide, element.Id, session);
        return frame;
    }

    private XElement Box(TextElement element, OdpWriteSession session)
    {
        var box = new XElement(Draw + "text-box");
        text.Write(box, element.Text.Paragraphs, session.Deck.Theme, session);
        return box;
    }

    private string? ClassOf(PlaceholderRole role) => role switch
    {
        PlaceholderRole.Title => "title",
        PlaceholderRole.Subtitle => "subtitle",
        PlaceholderRole.Body => "outline",
        PlaceholderRole.Free => null,
        _ => "text",
    };

}

internal sealed class InteractionListenerWriter(OdfInteractionMapper interactions) : OdpXml
{
    public void Attach(XElement shape, Slide slide, string elementId, OdpWriteSession session)
    {
        if (interactions.Build(Named(slide.Interaction(elementId), session)) is not { } listeners) return;

        if (shape.Name == Draw + "frame")
        {
            if (shape.Elements().FirstOrDefault(child => child.Name == Draw + "image-map" || child.Name == Svg + "title" || child.Name == Svg + "desc") is { } before)
                before.AddBeforeSelf(listeners);
            else
                shape.Add(listeners);
            return;
        }

        if (shape.Elements().LastOrDefault(child => child.Name == Svg + "title" || child.Name == Svg + "desc") is { } after)
            after.AddAfterSelf(listeners);
        else
            shape.AddFirst(listeners);
    }

    private InteractiveAction? Named(InteractiveAction? action, OdpWriteSession session) =>
        action is { Kind: InteractionKind.GoToSlide, Reference: { } target }
            ? action with { Reference = session.PageName(target) }
            : action;
}

internal sealed class MediaFrameWriter(OdfUnits units, OdfMediaMapper media)
{
    public XElement Write(MediaElement element, OdpWriteSession session)
    {
        var path = element.IsLinked ? media.LinkedHref(element)
            : session.AddOwnMedia($"Media/{units.FileSafe(element.Id)}.{units.MediaExtension(element.MimeType)}", element.Data, element.MimeType);
        var poster = element.PosterData is { Length: > 0 } ? session.AddOwnMedia($"Media/{units.FileSafe(element.Id)}-poster.png", element.PosterData!, "image/png") : null;
        return media.BuildFrame(element, path, poster);
    }
}

internal interface IOdpObjectWriter
{
    bool CanWrite(EmbeddedObject content);

    XElement Write(ObjectElement element, OdpWriteSession session);
}

internal abstract class OdpObjectWriter<TContent>(OdfAltTextMapper altText) : OdpXml, IOdpObjectWriter
    where TContent : EmbeddedObject
{
    public bool CanWrite(EmbeddedObject content) => content is TContent;

    public XElement Write(ObjectElement element, OdpWriteSession session) =>
        new OdfObjectNameMapper().Write(altText.Write(Build(element, (TContent)element.Content, session), element.Content), element.Content);

    protected abstract XElement Build(ObjectElement element, TContent content, OdpWriteSession session);
}

internal sealed class ChartObjectWriter(OdfDeckChartMapper charts, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Charts.ChartDefinition>(altText)
{
    protected override XElement Build(ObjectElement element, PulseOffice.Charts.ChartDefinition chart, OdpWriteSession session) =>
        charts.BuildFrame(element, session.AddDocument("Object",
            charts.BuildDocument(chart, chart.InlineData ?? PulseOffice.Charts.ChartData.Empty,
                new PulseOffice.Foundation.Units.DocumentSize(element.Bounds.Width, element.Bounds.Height), session.Culture),
            "application/vnd.oasis.opendocument.chart"));
}

internal sealed class FormulaObjectWriter(
    OdfDeckShapeMapper shapes,
    PulseOffice.Formulas.IFormulaParser parser,
    PulseOffice.Formulas.IMathMlSyntax mathml,
    OdfAltTextMapper altText) : OdpObjectWriter<FormulaObject>(altText)
{
    protected override XElement Build(ObjectElement element, FormulaObject formula, OdpWriteSession session) =>
        shapes.BuildFormula(element, formula, session.AddDocument("Formula",
            new XDocument(mathml.Write(parser.Parse(formula.Expression))),
            "application/vnd.oasis.opendocument.formula"));
}

internal sealed class TableObjectWriter(OdfDeckShapeMapper shapes, OdfAltTextMapper altText)
    : OdpObjectWriter<DataTableObject>(altText)
{
    protected override XElement Build(ObjectElement element, DataTableObject table, OdpWriteSession session) =>
        shapes.BuildTable(element, table, session.AutomaticStyles);
}

internal sealed class ShapeObjectWriter(OdfDeckShapeMapper shapes, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Drawing.Shapes.ShapeObject>(altText)
{
    private readonly OdfDimensionLines _dimensions = new();

    protected override XElement Build(ObjectElement element, PulseOffice.Drawing.Shapes.ShapeObject shape, OdpWriteSession session)
    {
        var style = session.NextStyle("gr");
        var built = session.ShapeStyles.Build(style, shape.Style);
        if (new OdfGraphicStyles().Encode(session.Deck.GraphicStyles, shape.StyleName) is { } parent)
            built.Add(new XAttribute(Style + "parent-style-name", parent));
        if (shape.Dimension is { } dimension) _dimensions.WriteStyle(built, _dimensions.Placed(element, shape, dimension));
        session.AddStyle(built);
        return shapes.BuildShape(element, shape, style, session.TextStyles);
    }
}

internal sealed class SceneObjectWriter(OdfDeckShapeMapper shapes, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Drawing.Scenes.Scene3DObject>(altText)
{
    protected override XElement Build(ObjectElement element, PulseOffice.Drawing.Scenes.Scene3DObject scene, OdpWriteSession session) =>
        shapes.BuildScene(element, scene, () => session.NextStyle("gr"), session.AddStyle)
        ?? new XElement(Draw + "rect",
            new XAttribute(OdfPackage.Namespaces.Xml + "id", element.Id),
            new XAttribute(Draw + "id", element.Id),
            new XAttribute(Svg + "x", Length(element.Bounds.Left)),
            new XAttribute(Svg + "y", Length(element.Bounds.Top)),
            new XAttribute(Svg + "width", Length(element.Bounds.Width)),
            new XAttribute(Svg + "height", Length(element.Bounds.Height)));

    private string Length(PulseOffice.Foundation.Units.Twip value) =>
        (value.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "cm";
}

internal sealed class FontworkObjectWriter(OdfDeckShapeMapper shapes, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Drawing.Fontwork.FontworkObject>(altText)
{
    private readonly OdfFontworkGeometry _geometry = new();

    protected override XElement Build(ObjectElement element, PulseOffice.Drawing.Fontwork.FontworkObject fontwork, OdpWriteSession session)
    {
        var style = session.NextStyle("gr");
        session.AddStyle(_geometry.Lettering(session.ShapeStyles.Build(style, fontwork.Style), element.Bounds.Height.Value));
        return shapes.BuildFontwork(element, fontwork, style);
    }
}

internal sealed class FreeformObjectWriter(OdfDeckShapeMapper shapes, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Drawing.Freeform.FreeformObject>(altText)
{
    protected override XElement Build(ObjectElement element, PulseOffice.Drawing.Freeform.FreeformObject freeform, OdpWriteSession session)
    {
        var style = session.NextStyle("gr");
        var built = session.ShapeStyles.Build(style, freeform.Style);
        if (new OdfGraphicStyles().Encode(session.Deck.GraphicStyles, freeform.StyleName) is { } parent)
            built.Add(new XAttribute(Style + "parent-style-name", parent));
        session.AddStyle(built);
        return shapes.BuildPath(element, freeform.Path.ToOutline(), style);
    }
}

internal sealed class ConnectorObjectWriter(OdfDeckShapeMapper shapes, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Drawing.Shapes.ConnectorObject>(altText)
{
    protected override XElement Build(ObjectElement element, PulseOffice.Drawing.Shapes.ConnectorObject connector, OdpWriteSession session)
    {
        var style = session.NextStyle("gr");
        session.AddStyle(session.ShapeStyles.Build(style, connector.Style));
        return shapes.BuildPath(element, connector.Path, style);
    }
}

internal sealed class ImageObjectWriter(OdfDeckShapeMapper shapes, OdfUnits units, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Drawing.Assets.RasterImageObject>(altText)
{
    protected override XElement Build(ObjectElement element, PulseOffice.Drawing.Assets.RasterImageObject image, OdpWriteSession session)
    {
        if (image.LinkPath is { Length: > 0 } linked)
            return shapes.BuildImage(element, new PulseOffice.Formats.LinkedFiles().ToPackageHref(new PulseOffice.Formats.LinkedFiles().ToHref(linked)), GraphicStyle(element, image, session));

        var path = session.AddOwnMedia($"Pictures/{units.FileSafe(element.Id)}.{units.Images.Extension(image.MediaType)}", image.Data.ToArray(), image.MediaType);
        return shapes.BuildImage(element, path, GraphicStyle(element, image, session));
    }

    private string? GraphicStyle(ObjectElement element, PulseOffice.Drawing.Assets.RasterImageObject image, OdpWriteSession session)
    {
        var grayscale = image.Filter == PulseOffice.Drawing.Assets.ImageFilter.Grayscale;
        if (!grayscale && image.Crop == default) return null;

        var properties = new XElement(Style + "graphic-properties");
        if (grayscale) properties.Add(new XAttribute(Draw + "color-mode", "greyscale"));
        if (image.Crop != default) properties.Add(new XAttribute(Fo + "clip", units.Clip(element.Bounds, image.Crop)));

        var name = session.NextStyle("gr");
        session.AddStyle(new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "graphic"),
            properties));
        return name;
    }
}

internal sealed class VectorObjectWriter(OdpVectorFrame frames, OdfAltTextMapper altText)
    : OdpObjectWriter<PulseOffice.Drawing.Assets.VectorGraphicObject>(altText)
{
    protected override XElement Build(ObjectElement element, PulseOffice.Drawing.Assets.VectorGraphicObject vector, OdpWriteSession session) =>
        frames.Build(element, vector, session, session.AddStyle);
}

internal sealed class OdpVectorFrame(OdfDeckShapeMapper shapes) : OdpXml
{
    public XElement Build(ObjectElement element, PulseOffice.Drawing.Assets.VectorGraphicObject vector, OdpWriteSession session, Action<XElement> addStyle)
    {
        var svg = session.Vectors.Svg(vector);
        var stem = $"Pictures/{new string([.. element.Id.Select(c => char.IsLetterOrDigit(c) ? c : '_')])}";
        var svgPath = session.AddOwnMedia(stem + ".svg", svg, session.Vectors.MediaType);
        var pngPath = session.AddOwnMedia(stem + ".png", session.Vectors.Png(svg, new PulseOffice.Foundation.Units.DocumentSize(element.Bounds.Width, element.Bounds.Height)), session.Vectors.FallbackMediaType);
        return shapes.BuildVector(element, svgPath, pngPath, Mirror(vector, session, addStyle));
    }

    private string? Mirror(PulseOffice.Drawing.Assets.VectorGraphicObject vector, OdpWriteSession session, Action<XElement> addStyle)
    {
        var token = (vector.FlipHorizontal, vector.FlipVertical) switch
        {
            (true, true) => "horizontal vertical",
            (true, false) => "horizontal",
            (false, true) => "vertical",
            _ => null,
        };
        if (token is null) return null;
        var name = session.NextStyle("gr");
        addStyle(new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "graphic"),
            new XElement(Style + "graphic-properties", new XAttribute(Style + "mirror", token))));
        return name;
    }
}

internal sealed class PackagedObjectWriter(OdfDeckShapeMapper shapes, OdfUnits units, OdfAltTextMapper altText)
    : OdpObjectWriter<PackagedObject>(altText)
{
    protected override XElement Build(ObjectElement element, PackagedObject packaged, OdpWriteSession session)
    {
        var path = session.AddOwnMedia($"Objects/{units.FileSafe(element.Id)}/{units.PackageFileName(packaged.FileName)}", packaged.Data.ToArray(), packaged.MediaType);
        var replacement = packaged.Replacement is { Length: > 0 } picture
            ? session.AddOwnMedia($"ObjectReplacements/{units.FileSafe(element.Id)}.png", picture.ToArray(), "image/png")
            : null;
        return shapes.BuildOle(element, packaged, path, replacement);
    }
}

internal sealed class ObjectWriterChain(IEnumerable<IOdpObjectWriter> writers)
{
    private readonly IReadOnlyList<IOdpObjectWriter> _writers = [.. writers];

    public XElement? Write(ObjectElement element, OdpWriteSession session) =>
        _writers.FirstOrDefault(writer => writer.CanWrite(element.Content))?.Write(element, session);
}

internal sealed class FormControlsWriter(OdfUnits units, OdfFormControlMapper controls) : OdpXml
{
    public void Write(XElement page, Slide slide, OdpWriteSession session)
    {
        var placed = slide.Elements.OfType<ObjectElement>()
            .Where(o => o.Content is PulseOffice.Document.Forms.FormControlObject).ToList();
        if (placed.Count == 0)
        {
            if (session.Deck.IndexOf(slide) == 0
                && controls.SettingsOnly(session.Deck.FormSettings, "DeckForm0") is { } settings)
                page.AddFirst(settings);
            return;
        }

        var form = new XElement(Form + "form",
            new XAttribute(Form + "name", $"DeckForm{session.Deck.IndexOf(slide)}"),
            new XAttribute(Form + "apply-filter", "false"));
        foreach (var control in placed)
            form.Add(controls.BuildControlElement(control.Id, (PulseOffice.Document.Forms.FormControlObject)control.Content));

        page.AddFirst(controls.Forms(session.Deck.FormSettings, form));

        foreach (var control in placed)
            page.Add(new XElement(Draw + "control",
                new XAttribute(Draw + "control", System.Xml.XmlConvert.EncodeLocalName(control.Id)),
                new XAttribute(Draw + "id", control.Id),
                units.Bounds(control.Bounds, Svg)));
    }
}

internal sealed class CommentsWriter(OdfUnits units, IOdfParagraphText text) : OdpXml
{
    public IEnumerable<XElement> Write(Slide slide) =>
        slide.Comments.Select(comment => new XElement(Office + "annotation",
            new XAttribute(Draw + "id", comment.Id),
            new XAttribute(Svg + "x", units.Cm(comment.X)),
            new XAttribute(Svg + "y", units.Cm(comment.Y)),
            new XElement(Dc + "creator", comment.Author),
            new XElement(Dc + "date", comment.CreatedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture)),
            text.Paragraphs(comment.Text)));
}
