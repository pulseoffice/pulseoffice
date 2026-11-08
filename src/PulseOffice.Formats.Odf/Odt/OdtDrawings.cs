using System.Text;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtDrawingSession(System.Globalization.CultureInfo culture)
{
    public OdpWriteSession Deck { get; } = new(new PulseOffice.Presentations.Presentation(), new VectorFallback(), culture);

    public List<(string Id, FormControlObject Control)> Controls { get; } = [];
}

internal sealed class OdtDrawingWriter(ObjectWriterChain chain, OdfFormControlMapper controls) : OdtXml
{
    private readonly OdpElementRotation _rotation = new();
    private readonly OdtLogicalViewBox _viewBox = new();

    private ShapeObject MirrorFolded(ShapeObject shape, AnchoredObject item) => shape with
    {
        FlipHorizontal = shape.FlipHorizontal != item.FlipHorizontal,
        FlipVertical = shape.FlipVertical != item.FlipVertical,
        RotationDegrees = item.FlipHorizontal != item.FlipVertical ? -shape.RotationDegrees : shape.RotationDegrees,
    };

    public bool CanWrite(EmbeddedObject content) =>
        content is ChartDefinition or FormulaObject or DataTableObject or ShapeObject or PulseOffice.Drawing.Fontwork.FontworkObject
            or FreeformObject or ConnectorObject or FormControlObject or VectorGraphicObject or ShapeGroupObject;

    public XElement? Write(AnchoredObject item, EmbeddedObject content, XElement graphic, string? parentStyle, OdtWriteSession session, Action<XElement, EmbeddedObject>? member = null)
    {
        if (content is ShapeObject { Dimension: not null } dimension && (item.FlipHorizontal || item.FlipVertical))
        {
            content = MirrorFolded(dimension, item);
            graphic.Attribute(Style + "mirror")?.Remove();
        }
        var element = new ObjectElement
        {
            Id = item.Id,
            Bounds = new DocumentRect(item.Placement.X, item.Placement.Y, item.Size.Width, item.Size.Height),
            ZOrder = item.ZOrder,
            RotationDegrees = item.RotationDegrees,
            Content = content,
        };

        XElement? built;
        if (content is FormControlObject control)
        {
            built = Control(item.Id, element.Bounds, control, session);
        }
        else if (content is VectorGraphicObject vector)
        {
            built = Vector(element, vector, session);
        }
        else if (content is ShapeGroupObject group)
        {
            built = Group(element, group, session, member);
            if (built is null) return null;
        }
        else
        {
            built = chain.Write(element, session.Drawing.Deck);
            if (built is null) return null;
            built = _rotation.Write(_viewBox.Write(built, element), element);
        }

        var styleName = (string?)built.Attribute(Draw + "style-name");
        var style = styleName is null ? null : session.Drawing.Deck.AutomaticStyles.Elements(Style + "style")
            .FirstOrDefault(candidate => (string?)candidate.Attribute(Style + "name") == styleName);
        if (style is null)
        {
            var name = session.Drawing.Deck.NextStyle("odr");
            style = new XElement(Style + "style", new XAttribute(Style + "name", name), new XAttribute(Style + "family", "graphic"));
            session.Drawing.Deck.AddStyle(style);
            built.SetAttributeValue(Draw + "style-name", name);
        }
        var properties = style.Element(Style + "graphic-properties");
        if (properties is null) style.Add(properties = new XElement(Style + "graphic-properties"));
        foreach (var attribute in graphic.Attributes()) properties.SetAttributeValue(attribute.Name, attribute.Value);
        if (parentStyle is not null) style.SetAttributeValue(Style + "parent-style-name", parentStyle);
        return built;
    }

    public XElement? Forms(PulseOffice.Document.Forms.FormDocumentSettings settings, OdtWriteSession session)
    {
        if (session.Drawing.Controls.Count == 0) return null;
        var form = new XElement(Form + "form", new XAttribute(Form + "name", "Form"), new XAttribute(Form + "apply-filter", "false"));
        foreach (var (id, control) in session.Drawing.Controls)
            form.Add(controls.BuildControlElement(id, control));
        return controls.Forms(settings, form);
    }

    private XElement Vector(ObjectElement element, VectorGraphicObject vector, OdtWriteSession session)
    {
        var path = $"Pictures/{new string([.. element.Id.Select(c => char.IsLetterOrDigit(c) ? c : '_')])}.svg";
        session.AddMedia(path, Encoding.UTF8.GetBytes(vector.Asset.SvgContent ?? "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"), "image/svg+xml");
        return new XElement(Draw + "frame",
            new XAttribute(Svg + "x", Pt0(element.Bounds.Left)), new XAttribute(Svg + "y", Pt0(element.Bounds.Top)),
            new XAttribute(Svg + "width", Pt0(element.Bounds.Width)), new XAttribute(Svg + "height", Pt0(element.Bounds.Height)),
            new XElement(Draw + "image",
                new XAttribute(Xlink + "href", path), new XAttribute(Xlink + "type", "simple"),
                new XAttribute(Xlink + "show", "embed"), new XAttribute(Xlink + "actuate", "onLoad"),
                new XAttribute(Draw + "mime-type", "image/svg+xml"),
                new XAttribute(Pt + "object-id", vector.Id),
                new XAttribute(Pt + "asset-id", vector.Asset.Id),
                new XAttribute(Pt + "asset-name", vector.Asset.Name),
                vector.Name is { } name ? new XAttribute(Pt + "object-name", name) : null,
                vector.RecolorHex is { } recolor ? new XAttribute(Pt + "recolor", recolor) : null,
                vector.FlipHorizontal ? new XAttribute(Pt + "flip-horizontal", "true") : null,
                vector.FlipVertical ? new XAttribute(Pt + "flip-vertical", "true") : null,
                Math.Abs(vector.RotationDegrees) > 1e-9 ? new XAttribute(Pt + "vector-rotation", vector.RotationDegrees.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)) : null));
    }

    private XElement? Group(ObjectElement element, ShapeGroupObject group, OdtWriteSession session, Action<XElement, EmbeddedObject>? member)
    {
        var width = element.Bounds.Width.Value / 20.0;
        var height = element.Bounds.Height.Value / 20.0;
        var left = element.Bounds.Left.Value / 20.0;
        var top = element.Bounds.Top.Value / 20.0;
        var built = new XElement(Draw + "g", new XAttribute(Draw + "name", element.Id));
        foreach (var child in group.Children)
        {
            var (x, y, w, h) = child.Scaled(width, height);
            var part = new ObjectElement
            {
                Id = child.Content.Id,
                Bounds = new DocumentRect(Twip.FromPoints(left + x), Twip.FromPoints(top + y), Twip.FromPoints(Math.Max(0.5, w)), Twip.FromPoints(Math.Max(0.5, h))),
                RotationDegrees = child.RotationDegrees,
                Content = child.Content,
            };
            var written = child.Content switch
            {
                ShapeGroupObject nested => Group(part, nested, session, member),
                FormControlObject control => Control(part.Id, part.Bounds, control, session),
                _ => chain.Write(part, session.Drawing.Deck),
            };
            if (written is null) continue;
            if (child.Content is not (ShapeGroupObject or FormControlObject)) member?.Invoke(written, child.Content);
            var placed = child.Content is ShapeGroupObject or FormControlObject ? written : _rotation.Write(_viewBox.Write(written, part), part);
            built.Add(child.Hyperlink is { Length: > 0 } link
                ? new XElement(Draw + "a", new XAttribute(Xlink + "type", "simple"), new XAttribute(Xlink + "href", link), placed)
                : placed);
        }
        return built.HasElements ? built : null;
    }

    private XElement Control(string id, DocumentRect bounds, FormControlObject control, OdtWriteSession session)
    {
        session.Drawing.Controls.Add((id, control));
        return new XElement(Draw + "control",
            new XAttribute(Draw + "control", System.Xml.XmlConvert.EncodeLocalName(id)),
            new XAttribute(Svg + "x", Pt0(bounds.Left)), new XAttribute(Svg + "y", Pt0(bounds.Top)),
            new XAttribute(Svg + "width", Pt0(bounds.Width)), new XAttribute(Svg + "height", Pt0(bounds.Height)));
    }

    private string Pt0(Twip value) => (value.Value / 20.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "pt";
}

internal sealed record OdtDrawingRead(EmbeddedObject Content, DocumentRect Bounds, double Rotation)
{
    public IReadOnlyDictionary<string, PulseOffice.Documents.Blocks.BlockContainer> Stories { get; init; } =
        new Dictionary<string, PulseOffice.Documents.Blocks.BlockContainer>(StringComparer.Ordinal);

    public string? Hyperlink { get; init; }
}

internal delegate PulseOffice.Documents.Blocks.BlockContainer OdtStorySource(XElement holder);

internal sealed class OdtDrawingReader(IGroupStories groupStories) : OdtXml
{
    private readonly OdfDeckShapeMapper _shapes = new();
    private readonly OdfDeckChartMapper _charts = new();
    private readonly OdfAltTextMapper _altText = new();
    private readonly OdpElementRotation _rotation = new();
    private readonly OdfFormControlMapper _controls = new();
    private readonly OdfImageTypes _images = new();
    private readonly OdfLiteralEnhancedPath _literal = new(new PulseOffice.Drawing.Shapes.ShapePresets());

    public bool Handles(XElement node) =>
        node.Name == Draw + "g" || node.Name == Draw + "custom-shape" || node.Name == Draw + "path" || node.Name == Draw + "polygon" || node.Name == Draw + "polyline"
        || node.Name == Draw + "control" || node.Name == _shapes.Scene || _shapes.Primitives.Contains(node.Name);

    public OdtDrawingRead? Read(XElement node, string id, OdtReadContext context, OdtStorySource? story = null)
    {
        if (node.Name == Draw + "g") return Group(node, id, context, story);
        ObjectElement? element;
        if (node.Name == Draw + "control")
        {
            var reference = (string?)node.Attribute(Draw + "control");
            if (reference is null || !context.FormDeclarations.TryGetValue(reference, out var declaration)) return null;
            if (_controls.ReadControl(declaration) is not { } control) return null;
            var measure = new OdtMeasure();
            return new OdtDrawingRead(control with { Id = (string?)declaration.Attribute(Form + "id") ?? reference },
                new DocumentRect(
                    measure.Twips((string?)node.Attribute(Svg + "x")) ?? Twip.Zero, measure.Twips((string?)node.Attribute(Svg + "y")) ?? Twip.Zero,
                    measure.Twips((string?)node.Attribute(Svg + "width")) ?? Twip.Zero, measure.Twips((string?)node.Attribute(Svg + "height")) ?? Twip.Zero), 0);
        }

        if (node.Name == Draw + "frame" && node.Element(Draw + "object") is null)
            return GroupedImage(node, id, context);
        if (node.Name == Draw + "frame")
        {
            element = _charts.Read(node, id, context.ReadXml) ?? _shapes.ReadFormula(node, id, context.ReadXml) ?? Formula(node, id, context);
        }
        else if (node.Name == Draw + "custom-shape")
            element = _shapes.IsFontwork(node) ? _shapes.ReadFontwork(node, id, context.Drawings) : _shapes.ReadShape(node, id, context.Drawings);
        else if (node.Name == _shapes.Scene)
            element = _shapes.ReadScene(node, id, context.Drawings);
        else if (node.Name == Draw + "path" || node.Name == Draw + "polygon" || node.Name == Draw + "polyline")
            element = _shapes.ReadPath(node, id, context.Drawings);
        else element = _shapes.ReadPrimitive(node, id, context.Drawings);
        if (_literal.Approximated(node)) context.Losses.Add(OdtReadLosses.ApproximatedShapes);

        if (element is null) return null;
        element = _rotation.Read(node, element);
        var content = new OdfObjectNameMapper().Read(_altText.Read(element.Content, node), node);
        var own = content switch
        {
            ShapeObject shape => shape.RotationDegrees,
            FreeformObject freeform => freeform.RotationDegrees,
            _ => 0,
        };
        return new OdtDrawingRead(content, element.Bounds, Math.Abs(element.RotationDegrees - own) < 1e-6 ? 0 : element.RotationDegrees - own);
    }

    private OdtDrawingRead? Group(XElement node, string id, OdtReadContext context, OdtStorySource? story)
    {
        if (context.Depth > OdtReadContext.MaximumDepth) return null;
        context.Depth++;
        try
        {
            return Members(node, id, context, story);
        }
        finally
        {
            context.Depth--;
        }
    }

    private OdtDrawingRead? Members(XElement node, string id, OdtReadContext context, OdtStorySource? story)
    {
        var stories = new Dictionary<string, PulseOffice.Documents.Blocks.BlockContainer>(StringComparer.Ordinal);
        var parts = node.Elements().Select(child => child.Name == Draw + "a" && child.Elements().FirstOrDefault() is { } linked ? (Node: linked, Link: (string?)child.Attribute(Xlink + "href")) : (Node: child, Link: null))
            .Where(child => Handles(child.Node) || child.Node.Name == Draw + "frame")
            .Select(child => Member(child.Node, (string?)child.Node.Attribute(Draw + "name") ?? context.Document.NextId("drw_"), context, story) is { } read ? read with { Hyperlink = child.Link } : null)
            .OfType<OdtDrawingRead>().ToList();
        if (parts.Count == 0) return null;
        foreach (var part in parts)
        {
            foreach (var (member, text) in part.Stories) stories[member] = text;
        }
        var left = parts.Min(part => part.Bounds.Left.Value);
        var top = parts.Min(part => part.Bounds.Top.Value);
        var right = parts.Max(part => part.Bounds.Left.Value + part.Bounds.Width.Value);
        var bottom = parts.Max(part => part.Bounds.Top.Value + part.Bounds.Height.Value);
        var width = Math.Max(1, right - left);
        var height = Math.Max(1, bottom - top);
        var children = parts.Select(part => new GroupChild(part.Content,
            (part.Bounds.Left.Value - left) / (double)width, (part.Bounds.Top.Value - top) / (double)height,
            part.Bounds.Width.Value / (double)width, part.Bounds.Height.Value / (double)height) { RotationDegrees = part.Rotation, Hyperlink = part.Hyperlink }).ToList();
        return new OdtDrawingRead(new ShapeGroupObject { Id = id, Children = children },
            new DocumentRect(new Twip(left), new Twip(top), new Twip(width), new Twip(height)), 0) { Stories = stories };
    }

    private OdtDrawingRead? Member(XElement child, string id, OdtReadContext context, OdtStorySource? story)
    {
        if (story is not null && child.Name == Draw + "frame" && child.Element(Draw + "text-box") is { } box)
        {
            var measure = new OdtMeasure();
            var member = groupStories.Member(id, new PulseOffice.Documents.Objects.TextBoxContent(story(box)));
            if (member.Value is ShapeObject plain && _shapes.ReadPrimitive(new XElement(Draw + "rect", child.Attributes()), id, context.Drawings)?.Content is ShapeObject { Style: { } look })
                member = member.With(plain with { Style = look with { Text = plain.Style.Text } });
            return new OdtDrawingRead(member.Value, new DocumentRect(
                measure.Twips((string?)child.Attribute(Svg + "x")) ?? Twip.Zero, measure.Twips((string?)child.Attribute(Svg + "y")) ?? Twip.Zero,
                measure.Twips((string?)child.Attribute(Svg + "width")) ?? Twip.Zero, measure.Twips((string?)child.Attribute(Svg + "height")) ?? Twip.Zero), 0)
            {
                Stories = new Dictionary<string, PulseOffice.Documents.Blocks.BlockContainer>(StringComparer.Ordinal) { [member.Value.Id] = member.Story! },
            };
        }
        if (Read(child, id, context, story) is not { } read) return null;
        if (story is null || read.Content is not (ShapeObject or FreeformObject) || !child.Elements().Any(element => element.Name == Text + "p" || element.Name == Text + "list" || element.Name == Text + "h"))
            return read;
        return read with
        {
            Stories = new Dictionary<string, PulseOffice.Documents.Blocks.BlockContainer>(StringComparer.Ordinal) { [read.Content.Id] = story(child) },
        };
    }

    private OdtDrawingRead? GroupedImage(XElement frame, string id, OdtReadContext context)
    {
        if (frame.Element(Draw + "image") is not { } image) return null;
        if ((string?)image.Attribute(Xlink + "href") is not { Length: > 0 } href || context.ReadBytes(href.TrimStart('.', '/')) is not { } data)
        {
            context.Losses.Add(OdtReadLosses.MissingPictures);
            return null;
        }
        var measure = new OdtMeasure();
        var raster = new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = (string?)image.Attribute(Pt + "object-id") ?? id,
            Data = data,
            MediaType = (string?)image.Attribute(Draw + "mime-type") is { Length: > 0 } mime && mime.StartsWith("image/", StringComparison.Ordinal) ? mime : _images.MediaTypeOf(href),
        };
        return new OdtDrawingRead(raster, new DocumentRect(
            measure.Twips((string?)frame.Attribute(Svg + "x")) ?? Twip.Zero, measure.Twips((string?)frame.Attribute(Svg + "y")) ?? Twip.Zero,
            measure.Twips((string?)frame.Attribute(Svg + "width")) ?? Twip.Zero, measure.Twips((string?)frame.Attribute(Svg + "height")) ?? Twip.Zero), 0);
    }

    private ObjectElement? Formula(XElement frame, string id, OdtReadContext context)
    {
        if ((string?)frame.Element(Draw + "object")?.Attribute(Xlink + "href") is not { } href) return null;
        XNamespace math = "http://www.w3.org/1998/Math/MathML";
        var annotation = context.ReadXml(href.TrimStart('.', '/') + "/content.xml")?.Descendants(math + "annotation")
            .FirstOrDefault(a => ((string?)a.Attribute("encoding") ?? string.Empty).Contains("StarMath", StringComparison.OrdinalIgnoreCase));
        if (annotation is null || annotation.Value.Trim().Length == 0) return null;
        var measure = new OdtMeasure();
        return new ObjectElement
        {
            Id = id,
            Bounds = new DocumentRect(measure.Twips((string?)frame.Attribute(Svg + "x")) ?? Twip.Zero, measure.Twips((string?)frame.Attribute(Svg + "y")) ?? Twip.Zero,
                measure.Twips((string?)frame.Attribute(Svg + "width")) ?? Twip.Zero, measure.Twips((string?)frame.Attribute(Svg + "height")) ?? Twip.Zero),
            Content = new FormulaObject { Id = id, Expression = annotation.Value.Trim() },
        };
    }

    public VectorGraphicObject? Vector(XElement image, byte[] data, string? title, string? description, OdtReadContext context)
    {
        var href = (string?)image.Attribute(Xlink + "href") ?? string.Empty;
        var head = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 512));
        if (!href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) && (string?)image.Attribute(Draw + "mime-type") != "image/svg+xml" && !head.Contains("<svg", StringComparison.Ordinal))
            return null;
        var id = (string?)image.Attribute(Pt + "object-id") ?? context.Document.NextId("svg_");
        return new VectorGraphicObject
        {
            Id = id,
            Asset = new VectorAsset
            {
                Id = (string?)image.Attribute(Pt + "asset-id") ?? id,
                Name = (string?)image.Attribute(Pt + "asset-name") ?? id,
                SourceKind = VectorSourceKind.InlineSvg,
                SvgContent = Encoding.UTF8.GetString(data),
            },
            Name = (string?)image.Attribute(Pt + "object-name"),
            RecolorHex = (string?)image.Attribute(Pt + "recolor"),
            FlipHorizontal = (string?)image.Attribute(Pt + "flip-horizontal") == "true",
            FlipVertical = (string?)image.Attribute(Pt + "flip-vertical") == "true",
            RotationDegrees = double.TryParse((string?)image.Attribute(Pt + "vector-rotation"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0,
            Title = title,
            Description = description,
        };
    }
}
