using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Document.Tables;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Odt;

using TableRow = PulseOffice.Documents.Blocks.TableRow;
using TableCell = PulseOffice.Documents.Blocks.TableCell;

internal interface IOdtBlockWriter
{
    void Write(XElement container, IEnumerable<Block> blocks, OdtStyleSink sink, OdtWriteSession session);
}

internal sealed class OdtBlockWriter(
    OdtParagraphWriter paragraphs,
    OdtTableWriter tables,
    OdtSectionWriter sections,
    IStyleResolver resolver,
    OdtStyleNames names) : OdtXml, IOdtBlockWriter
{
    public void Write(XElement container, IEnumerable<Block> blocks, OdtStyleSink sink, OdtWriteSession session)
    {
        XElement? list = null;
        string? listStyle = null;
        string? listThread = null;

        foreach (var block in blocks)
        {
            if (block is ParagraphBlock paragraph && ListStyleOf(paragraph, session) is { } style)
            {
                if (list is null || listStyle != style || listThread != paragraph.Content.ListId)
                {
                    list = new XElement(Text + "list", new XAttribute(Text + "style-name", names.Encode(style)));
                    session.Lists.Write(list, style, paragraph.Content.ListId);
                    listStyle = style;
                    listThread = paragraph.Content.ListId;
                    container.Add(list);
                }
                list.Add(Entry(paragraphs.Write(paragraph, sink, session, this), paragraph.Content));
                continue;
            }

            list = null;
            listStyle = null;
            listThread = null;
            container.Add(block switch
            {
                ParagraphBlock p => paragraphs.Write(p, sink, session, this),
                TableBlock t => tables.Write(t, sink, session, this),
                SectionBlock s => sections.Write(s, sink, session, this),
                _ => null,
            });
        }
    }

    private string? ListStyleOf(ParagraphBlock paragraph, OdtWriteSession session)
    {
        var content = paragraph.Content;
        if (!content.IsListItem && !content.ListHeader && content.ListStyleName is null) return null;
        return content.ListStyleName ?? resolver.ResolveParagraphStyle(session.Document.Styles, content.StyleName).ListStyleName ?? DefaultStyleNames.List1;
    }

    private XElement Entry(XElement paragraph, TextParagraph model)
    {
        var item = new XElement(Text + (model.ListHeader ? "list-header" : "list-item"), paragraph);
        if (model.ListRestart is { } start && !model.ListHeader) item.Add(new XAttribute(Text + "start-value", start));
        for (var level = Math.Clamp(model.BulletLevel, 0, 9); level > 0; level--)
            item = new XElement(Text + "list-item", new XElement(Text + "list", item));
        return item;
    }
}

internal sealed class OdtParagraphWriter(
    OdtParagraphCodec codec,
    IDirectFormatting direct,
    IStyleResolver resolver,
    OdtStyleNames names,
    OdtInlineWriter inline) : OdtXml
{
    public XElement Write(ParagraphBlock block, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var content = block.Content;
        var properties = direct.Paragraph(content);
        var resolved = resolver.ResolveParagraphStyle(session.Document.Styles, content.StyleName);
        var outline = content.OutlineLevel ?? resolved.OutlineLevel;

        var implied = session.ImpliedInitialPage;
        session.ImpliedInitialPage = null;
        var master = properties.PageStyleName ?? implied;

        var paragraphElement = codec.Paragraph(properties);
        var textProperties = codec.Hyphenation(properties).Concat(inline.MarkAttributes(block, session)).ToList();
        var parent = content.StyleName is null ? null : names.Encode(content.StyleName);
        string? styleName = parent;
        if (paragraphElement is not null || textProperties.Count > 0 || master is not null)
        {
            styleName = sink.Register("paragraph", parent,
            [
                master is null ? null : new XAttribute(Style + "master-page-name", names.Encode(master)),
                implied is not null && properties.PageStyleName is null ? new XAttribute(Pt + "implied-page", "true") : null,
                paragraphElement,
                textProperties.Count > 0 ? new XElement(Style + "text-properties", textProperties) : null,
            ], "P");
        }

        var element = new XElement(Text + (outline is null ? "p" : "h"));
        if (styleName is not null) element.Add(new XAttribute(Text + "style-name", styleName));
        if (outline is { } level) element.Add(new XAttribute(Text + "outline-level", level));
        if (content.OutlineLevel is null && outline is not null) element.Add(new XAttribute(Pt + "outline-inherited", "true"));
        element.Add(new XAttribute(Xml + "id", block.Id));
        if (content.NextPageStyleName is { } deferred && properties.PageStyleName is null) element.Add(new XAttribute(Pt + "next-master-page", names.Encode(deferred)));
        if (content.IsListItem) element.Add(new XAttribute(Pt + "list-kind", content.Numbered ? "number" : "bullet"));
        if (content.ListStyleName is null && (content.IsListItem || content.BulletLevel > 0))
            element.Add(new XAttribute(Pt + "list-inherited", "true"));

        inline.Write(element, block, sink, session, blocks);
        return element;
    }
}

internal sealed class OdtInlineWriter(
    OdtCharacterCodec characters,
    OdtFieldTable fields,
    IDirectFormatting direct,
    OdtStyleNames names,
    IOdfParagraphText text,
    OdtMarkWriter marks,
    OdtContentControls controls,
    PulseOffice.Documents.Controls.IContentControlCatalog catalog,
    IMarkOrder order) : OdtXml
{
    public void Write(XElement paragraph, ParagraphBlock block, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var runs = block.Content.Runs.Count > 0 || block.Text.Length == 0 ? block.Content.Runs : [new TextRun(block.Text)];
        var pending = order.Ordered(block.Marks).ToList();
        var markIndex = 0;
        var offset = 0;
        var open = new Stack<(string Id, XElement Element)>();
        XElement Target() => open.Count > 0 ? open.Peek().Element : paragraph;

        var marked = pending.OfType<ObjectMark>().Select(mark => mark.ObjectId).ToHashSet(StringComparer.Ordinal);
        foreach (var item in session.Document.Objects.AnchoredTo(block.Id).Where(item => !marked.Contains(item.Id)))
            paragraph.Add(marks.Unmarked(item, sink, session, blocks));

        var started = pending.OfType<ContentControlMark>().Where(mark => mark.Edge == MarkEdge.Start).Select(mark => mark.ControlId).ToHashSet(StringComparer.Ordinal);
        foreach (var continued in pending.OfType<ContentControlMark>().Where(mark => mark.Edge == MarkEdge.End && !started.Contains(mark.ControlId)))
        {
            if (catalog.Find(session.Document, continued.ControlId) is not { } span) continue;
            var element = controls.Element(continued.ControlId, span.Control);
            Target().Add(element);
            open.Push((continued.ControlId, element));
        }

        void Control(ContentControlMark mark)
        {
            if (mark.Edge == MarkEdge.Start)
            {
                var element = controls.Element(mark.ControlId, mark.Control ?? new PulseOffice.Documents.Controls.ContentControl());
                Target().Add(element);
                open.Push((mark.ControlId, element));
                return;
            }
            if (open.All(entry => entry.Id != mark.ControlId)) return;
            while (open.Count > 0 && open.Pop().Id != mark.ControlId) { }
        }

        void Flush(int upTo)
        {
            while (markIndex < pending.Count && pending[markIndex].Offset <= upTo)
            {
                if (pending[markIndex] is ContentControlMark control) Control(control);
                else foreach (var node in marks.Write(pending[markIndex], block.Id, sink, session, blocks)) Target().Add(node);
                markIndex++;
            }
        }

        foreach (var run in runs)
        {
            var start = offset;
            var end = offset + run.Text.Length;
            if (run.Field != TextFieldKind.None)
            {
                Flush(start);
                Target().Add(Segment(run, run.Text, sink, session, isField: true));
                offset = end;
                continue;
            }

            var cursor = start;
            while (cursor < end)
            {
                Flush(cursor);
                var next = markIndex < pending.Count ? Math.Min(end, pending[markIndex].Offset) : end;
                Target().Add(Segment(run, run.Text[(cursor - start)..(next - start)], sink, session, isField: false));
                cursor = next;
            }
            offset = end;
        }

        Flush(int.MaxValue);
    }

    public IReadOnlyList<XAttribute> MarkAttributes(ParagraphBlock block, OdtWriteSession session)
    {
        if (block.Text.Length > 0 || block.Content.Runs is not [{ Text.Length: 0 } run]) return [];
        var properties = direct.Run(run);
        if (properties.Font is { } font) session.Fonts.Add(font);
        return [.. characters.Write(properties)];
    }

    private object? Segment(TextRun run, string value, OdtStyleSink sink, OdtWriteSession session, bool isField)
    {
        if (value.Length == 0 && !isField) return null;
        var properties = direct.Run(run);
        if (properties.Font is { } font) session.Fonts.Add(font);
        var attributes = characters.Write(properties).ToList();
        var parent = run.StyleName is null ? null : names.Encode(run.StyleName);
        var styleName = attributes.Count > 0
            ? sink.Register("text", parent, [new XElement(Style + "text-properties", attributes)], "T")
            : parent;

        IEnumerable<XNode> body = isField && fields.Build(run, value, session) is { } field
            ? [field]
            : Literal(value);

        XElement? wrapper = styleName is null ? null : new XElement(Text + "span", new XAttribute(Text + "style-name", styleName), body);
        if (run.Ruby is { Length: > 0 } ruby && !isField)
            wrapper = new XElement(Text + "ruby", new XElement(Text + "ruby-base", wrapper is null ? body : [wrapper]), new XElement(Text + "ruby-text", ruby));
        if (run.Hyperlink is { } link)
        {
            var anchor = new XElement(Text + "a", new XAttribute(Xlink + "type", "simple"), new XAttribute(Xlink + "href", link));
            anchor.Add(wrapper is null ? body : [wrapper]);
            return anchor;
        }

        return wrapper is not null ? wrapper : body.ToList();
    }

    private IEnumerable<XNode> Literal(string value)
    {
        if (!value.EndsWith(' ')) return text.Paragraph(value).Nodes().ToList();
        var trimmed = value.TrimEnd(' ');
        var spaces = value.Length - trimmed.Length;
        List<XNode> nodes = trimmed.Length == 0 ? [] : [.. text.Paragraph(trimmed).Nodes()];
        var element = new XElement(Text + "s");
        if (spaces > 1) element.Add(new XAttribute(Text + "c", spaces));
        nodes.Add(element);
        return nodes;
    }
}

internal sealed class OdtMarkWriter(OdtCommentWriter comments, OdtNoteWriter notes, OdtObjectWriter objects) : OdtXml
{
    public XElement? Unmarked(AnchoredObject item, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks) =>
        objects.Write(item, sink, session, blocks);

    public IEnumerable<XNode> Write(InlineMark mark, string paragraphId, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        switch (mark)
        {
            case BookmarkMark bookmark:
                yield return new XElement(Text + (bookmark.Edge switch { MarkEdge.Start => "bookmark-start", MarkEdge.End => "bookmark-end", _ => "bookmark" }),
                    new XAttribute(Text + "name", bookmark.Name));
                break;
            case ChangeMark change:
                yield return new XElement(Text + (change.Edge switch { MarkEdge.Start => "change-start", MarkEdge.End => "change-end", _ => "change" }),
                    new XAttribute(Text + "change-id", change.ChangeId));
                break;
            case CommentMark comment:
                foreach (var node in comments.Write(comment, sink, session, blocks)) yield return node;
                break;
            case NoteMark note when notes.Write(note, sink, session, blocks) is { } element:
                yield return element;
                break;
            case ObjectMark item when objects.Write(item.ObjectId, sink, session, blocks) is { } frame:
                yield return frame;
                break;
            case IndexEntryMark index:
                yield return Index(index, session);
                break;
            case FieldCodeMark field:
                yield return FieldCode(field);
                break;
        }
    }

    private XElement FieldCode(FieldCodeMark mark)
    {
        var element = new XElement(Pt + "field-code",
            new XAttribute(Pt + "field-id", mark.FieldId),
            new XAttribute(Pt + "part", mark.Part.ToString().ToLowerInvariant()));
        if (mark.Code.Length > 0) element.Add(new XAttribute(Pt + "code", mark.Code));
        if (mark.Data is { Length: > 0 } data) element.Add(new XAttribute(Pt + "data", data));
        if (mark.Locked) element.Add(new XAttribute(Pt + "locked", "true"));
        return element;
    }

    private XElement Index(IndexEntryMark mark, OdtWriteSession session)
    {
        var kind = mark.Index switch
        {
            "alphabetical" => "alphabetical-index-mark",
            "toc" or "table-of-contents" => "toc-mark",
            _ => "user-index-mark",
        };
        var element = mark.Edge switch
        {
            MarkEdge.Start => new XElement(Text + (kind + "-start"), new XAttribute(Text + "id", session.IndexId(mark.Key, true)), new XAttribute(Pt + "entry", mark.Entry)),
            MarkEdge.End => new XElement(Text + (kind + "-end"), new XAttribute(Text + "id", session.IndexId(mark.Key, false))),
            _ => new XElement(Text + kind, new XAttribute(Text + "string-value", mark.Entry)),
        };
        if (kind == "alphabetical-index-mark" && mark.Edge != MarkEdge.End)
        {
            if (mark.Key1 is { } key1) element.Add(new XAttribute(Text + "key1", key1));
            if (mark.Key2 is { } key2) element.Add(new XAttribute(Text + "key2", key2));
        }
        if (kind == "user-index-mark" && mark.Edge != MarkEdge.End) element.Add(new XAttribute(Text + "index-name", mark.Index));
        if (kind == "toc-mark" && mark.Index != "toc") element.Add(new XAttribute(Pt + "index", mark.Index));
        return element;
    }
}

internal sealed class OdtCommentWriter : OdtXml
{
    public IEnumerable<XNode> Write(CommentMark mark, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        if (mark.Edge == MarkEdge.End)
        {
            yield return new XElement(Office + "annotation-end", new XAttribute(Office + "name", mark.CommentId));
            yield break;
        }

        if (session.Document.Comments.Find(mark.CommentId) is not { } comment) yield break;
        var annotation = Annotation(comment, sink, session, blocks);
        if (mark.Edge == MarkEdge.Point) annotation.Add(new XAttribute(Pt + "point", "true"));
        yield return annotation;
        foreach (var reply in session.Document.Comments.Thread(comment.Id).Skip(1))
            yield return Annotation(reply, sink, session, blocks);
    }

    private XElement Annotation(Comment comment, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var annotation = new XElement(Office + "annotation", new XAttribute(Office + "name", comment.Id));
        if (comment.ParentId is { } parent) annotation.Add(new XAttribute(Loext + "parent-name", parent));
        annotation.Add(new XAttribute(Loext + "resolved", comment.Resolved ? "true" : "false"));
        annotation.Add(new XElement(Dc + "creator", comment.Author));
        annotation.Add(new XElement(Dc + "date", comment.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)));
        if (comment.Initials is { } initials) annotation.Add(new XElement(Meta + "creator-initials", initials));
        blocks.Write(annotation, comment.Content.Blocks, sink, session);
        return annotation;
    }
}

internal sealed class OdtNoteWriter(OdtVocabulary vocabulary) : OdtXml
{
    public XElement? Write(NoteMark mark, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        if (session.Document.Notes.Find(mark.NoteId) is not { } note) return null;
        var number = session.NextNote();
        var citation = new XElement(Text + "note-citation", note.Citation ?? number.ToString(CultureInfo.InvariantCulture));
        if (note.Citation is { } label) citation.Add(new XAttribute(Text + "label", label));
        var body = new XElement(Text + "note-body");
        blocks.Write(body, note.Content.Blocks, sink, session);
        return new XElement(Text + "note",
            new XAttribute(Text + "id", note.Id),
            new XAttribute(Text + "note-class", vocabulary.NoteClass.Token(note.Kind)),
            citation,
            body);
    }
}

internal sealed class OdtObjectWriter(OdtMeasure measure, OdtFrameCodec frames, OdtStyleNames names, OdfImageTypes images, OdfImageMapCodec imageMaps, OdtDrawingWriter drawings, OdtContours contours, OdfSignatureLineCodec signatures) : OdtXml
{
    private XElement? SignatureFallback(AnchoredObject item, OdtWriteSession session)
    {
        if (item.Content is not EmbeddedContent { Value: PulseOffice.Document.Forms.SignatureLineObject line }) return null;
        if (signatures.UnsignedRaster(line, item.Size) is not { } raster) return null;

        var path = $"Pictures/{Safe(item.Id)}.png";
        session.AddMedia(path, raster, signatures.RasterMediaType);
        return signatures.Fallback(path);
    }

    private XElement? OleReplacement(AnchoredObject item, OdtWriteSession session)
    {
        if (item.Content is not EmbeddedContent { Value: PackagedObject { Replacement: { Length: > 0 } picture } }) return null;

        var path = $"ObjectReplacements/{Safe(item.Id)}.png";
        session.AddMedia(path, picture.ToArray(), "image/png");
        return new XElement(Draw + "image",
            new XAttribute(Xlink + "href", path),
            new XAttribute(Xlink + "type", "simple"),
            new XAttribute(Xlink + "show", "embed"),
            new XAttribute(Xlink + "actuate", "onLoad"),
            new XAttribute(Draw + "mime-type", "image/png"));
    }

    private string Clip(DocumentSize size, (double Left, double Top, double Right, double Bottom) crop)
    {
        string Side(double fraction, Twip length) => measure.Points(fraction * length.ToPoints());
        return $"rect({Side(crop.Top, size.Height)}, {Side(crop.Right, size.Width)}, {Side(crop.Bottom, size.Height)}, {Side(crop.Left, size.Width)})";
    }

    public XElement? Write(string objectId, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks) =>
        session.Document.Objects.Find(objectId) is { } item ? Write(item, sink, session, blocks) : null;

    public XElement? Write(AnchoredObject item, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks) =>
        Linked(item, Placed(item, sink, session, blocks));

    private XElement? Linked(AnchoredObject item, XElement? placed) =>
        placed is null || item.Hyperlink is not { Length: > 0 } link || item.Anchor is PageAnchor
            ? placed
            : new XElement(Draw + "a", new XAttribute(Xlink + "type", "simple"), new XAttribute(Xlink + "href", link), placed);

    private XElement? Placed(AnchoredObject item, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var drawing = item.Content is EmbeddedContent { Value: var drawn } && drawings.CanWrite(drawn) ? drawn : null;
        var content = drawing is null ? Content(item, sink, session, blocks) : null;
        if (content is null && drawing is null) return null;

        var anchorType = item.Anchor switch
        {
            PageAnchor => "page",
            CharacterAnchor => "char",
            AsCharacterAnchor => "as-char",
            _ => "paragraph",
        };

        var graphic = new XElement(Style + "graphic-properties",
            new XAttribute(Style + "wrap", frames.Wrap(item.Wrap)),
            new XAttribute(Style + "horizontal-pos", "from-left"),
            new XAttribute(Style + "horizontal-rel", frames.Horizontal(item.Placement.Horizontal)),
            new XAttribute(Style + "vertical-pos", "from-top"),
            new XAttribute(Style + "vertical-rel", frames.Vertical(item.Placement.Vertical)),
            new XAttribute(Fo + "margin-left", measure.Length(item.WrapSpacing.Left)),
            new XAttribute(Fo + "margin-right", measure.Length(item.WrapSpacing.Right)),
            new XAttribute(Fo + "margin-top", measure.Length(item.WrapSpacing.Top)),
            new XAttribute(Fo + "margin-bottom", measure.Length(item.WrapSpacing.Bottom)));
        if (item.Wrap == WrapMode.TopAndBottom) graphic.Add(new XAttribute(Pt + "wrap", "top-and-bottom"));
        if (item.InBackground) graphic.Add(new XAttribute(Style + "run-through", "background"));
        if (item.WrapContour && item.Content is EmbeddedContent && item.Anchor is not AsCharacterAnchor) graphic.Add(new XAttribute(Style + "wrap-contour", "true"), new XAttribute(Style + "wrap-contour-mode", "full"));
        if (item.WrapFirstParagraph) graphic.Add(new XAttribute(Style + "number-wrapped-paragraphs", "1"));
        if (contours.Mirror(item) is { } mirror) graphic.Add(new XAttribute(Style + "mirror", mirror));
        if (item.Content is TextBoxContent { Look: { } look }) frames.WriteLook(graphic, look);
        if (item.Content is EmbeddedContent { Value: RasterImageObject picture })
        {
            if (picture.Filter == ImageFilter.Grayscale) graphic.Add(new XAttribute(Draw + "color-mode", "greyscale"));
            if (picture.Crop != default) graphic.Add(new XAttribute(Fo + "clip", Clip(item.Size, picture.Crop)));
        }
        var parentStyle = item.StyleName is null ? null : names.Encode(item.StyleName);
        if (drawing is not null)
        {
            var storyFrame = parentStyle is null && item.Content.Story is not null;
            var members = (item.Content as EmbeddedContent)?.MemberStories;
            if (drawings.Write(item, drawing, graphic, storyFrame ? names.Encode(DefaultStyleNames.Frame) : parentStyle, session,
                    (written, value) => { if (members is not null && members.TryGetValue(value.Id, out var memberStory)) Member(written, memberStory, sink, session, blocks); }) is not { } shape) return null;
            if (storyFrame) shape.SetAttributeValue(Pt + "implied-style", "true");
            shape.SetAttributeValue(Text + "anchor-type", anchorType);
            if (item.Anchor is PageAnchor anchoredPage) shape.SetAttributeValue(Text + "anchor-page-number", anchoredPage.PageNumber);
            if (item.Anchor is AsCharacterAnchor)
            {
                shape.Attribute(Svg + "x")?.Remove();
                shape.Attribute(Svg + "y")?.Remove();
            }
            shape.SetAttributeValue(Draw + "z-index", item.ZOrder);
            if (item.Content.Story is { } story) Story(shape, story, sink, session, blocks);
            if (drawing is FormControlObject) shape.SetAttributeValue(Pt + "item-id", item.Id);
            else shape.SetAttributeValue(Xml + "id", item.Id);
            if (item.Name is { } itemName) shape.SetAttributeValue(Pt + "item-name", itemName);
            return shape;
        }

        var implied = parentStyle is null;
        var drawnByShapeLayer = item.Content is EmbeddedContent { Value: PulseOffice.Document.Forms.SignatureLineObject };
        var styleName = sink.Register("graphic", parentStyle ?? (drawnByShapeLayer ? null : names.Encode(DefaultStyleNames.Graphics)), [graphic], drawnByShapeLayer ? "gr" : "fr");

        var frame = new XElement(Draw + "frame",
            new XAttribute(Draw + "style-name", styleName),
            new XAttribute(Draw + "name", item.Name ?? item.Id),
            item.Name is null ? new XAttribute(Pt + "unnamed", "true") : null,
            implied ? new XAttribute(Pt + "implied-style", "true") : null,
            new XAttribute(Text + "anchor-type", anchorType),
            item.Anchor is PageAnchor page ? new XAttribute(Text + "anchor-page-number", page.PageNumber) : null,
            item.Anchor is AsCharacterAnchor ? null : new XAttribute(Svg + "x", measure.Length(item.Placement.X)),
            item.Anchor is AsCharacterAnchor ? null : new XAttribute(Svg + "y", measure.Length(item.Placement.Y)),
            new XAttribute(Svg + "width", measure.Length(item.Size.Width)),
            new XAttribute(Svg + "height", measure.Length(item.Size.Height)),
            new XAttribute(Draw + "z-index", item.ZOrder),
            Math.Abs(item.RotationDegrees) > 1e-9 ? new XAttribute(Pt + "rotation", item.RotationDegrees.ToString("0.####", CultureInfo.InvariantCulture)) : null,
            new XAttribute(Xml + "id", item.Id),
            content,
            SignatureFallback(item, session),
            OleReplacement(item, session));

        if (item.Content is EmbeddedContent { Value: RasterImageObject mapped } && imageMaps.Build(mapped.Map, item.Size) is { } map)
            frame.Add(map);
        if (item.Content is EmbeddedContent { Value: var embedded })
        {
            if (embedded.Title is { } title) frame.Add(new XElement(Svg + "title", title));
            if (embedded.Description is { } description) frame.Add(new XElement(Svg + "desc", description));
            if (embedded is PulseOffice.Document.Forms.SignatureLineObject signatureLine) frame.Add(signatures.Element(signatureLine));
        }
        if (contours.Write(item) is { } polygon) frame.Add(polygon);
        return frame;
    }

    private XElement? Content(AnchoredObject item, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        switch (item.Content)
        {
            case TextBoxContent box:
                var textBox = new XElement(Draw + "text-box");
                if (box.AutoGrowHeight) textBox.Add(new XAttribute(Fo + "min-height", measure.Length(item.Size.Height)));
                if (box.NextFrameName is { } next) textBox.Add(new XAttribute(Draw + "chain-next-name", next));
                blocks.Write(textBox, box.Content.Blocks, sink, session);
                return textBox;
            case EmbeddedContent { Value: RasterImageObject image }:
                string href;
                if (image.LinkPath is { Length: > 0 } linked) href = new LinkedFiles().ToPackageHref(new LinkedFiles().ToHref(linked));
                else
                {
                    href = $"Pictures/{Safe(item.Id)}.{images.Extension(image.MediaType)}";
                    session.AddMedia(href, image.Data.ToArray(), image.MediaType);
                }
                return new XElement(Draw + "image",
                    new XAttribute(Xlink + "href", href),
                    new XAttribute(Xlink + "type", "simple"),
                    new XAttribute(Xlink + "show", "embed"),
                    new XAttribute(Xlink + "actuate", "onLoad"),
                    new XAttribute(Draw + "mime-type", image.MediaType),
                    new XAttribute(Pt + "object-id", image.Id),
                    image.PixelWidth > 0 ? new XAttribute(Pt + "pixel-width", image.PixelWidth) : null,
                    image.PixelHeight > 0 ? new XAttribute(Pt + "pixel-height", image.PixelHeight) : null,
                    image.Name is { } name ? new XAttribute(Pt + "object-name", name) : null,
                    new XAttribute(Pt + "natural-width", image.NaturalSize.Width.Value),
                    new XAttribute(Pt + "natural-height", image.NaturalSize.Height.Value));
            case EmbeddedContent { Value: PulseOffice.Document.Forms.SignatureLineObject line }:
                var signaturePath = $"Pictures/{Safe(item.Id)}.svg";
                session.AddMedia(signaturePath, signatures.UnsignedImage(line, item.Size), OdfSignatureLineCodec.MediaType);
                return new XElement(Draw + "image",
                    new XAttribute(Xlink + "href", signaturePath),
                    new XAttribute(Xlink + "type", "simple"),
                    new XAttribute(Xlink + "show", "embed"),
                    new XAttribute(Xlink + "actuate", "onLoad"),
                    new XAttribute(Draw + "mime-type", OdfSignatureLineCodec.MediaType),
                    new XAttribute(Pt + "object-id", line.Id));
            case EmbeddedContent { Value: PackagedObject packaged }:
                var path = $"Objects/{Safe(item.Id)}/{packaged.FileName}";
                session.AddMedia(path, packaged.Data.ToArray(), packaged.MediaType);
                return new XElement(Draw + "object-ole",
                    new XAttribute(Xlink + "href", path),
                    new XAttribute(Xlink + "type", "simple"),
                    new XAttribute(Draw + "mime-type", packaged.MediaType),
                    new XAttribute(Pt + "object-id", packaged.Id),
                    new XAttribute(Pt + "file-name", packaged.FileName),
                    new XAttribute(Pt + "packaged", packaged.Packaged == PackagedObjectKind.Media ? "media" : "file"));
            default:
                return null;
        }
    }

    private void Member(XElement shape, BlockContainer story, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        if (story.Blocks.All(block => block is ParagraphBlock))
        {
            Story(shape, story, sink, session, blocks);
            return;
        }
        shape.Elements().Where(child => child.Name.Namespace != Svg).Remove();
        var box = new XElement(Draw + "text-box");
        blocks.Write(box, story.Blocks, sink, session);
        shape.Name = Draw + "frame";
        shape.Add(box);
    }

    private void Story(XElement shape, BlockContainer story, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        shape.Elements().Where(child => child.Name == Text + "p" || child.Name == Text + "list" || child.Name == Text + "h").Remove();
        var holder = new XElement(shape.Name);
        blocks.Write(holder, story.Blocks, sink, session);
        if (shape.Element(Draw + "enhanced-geometry") is { } geometry) geometry.AddBeforeSelf(holder.Elements());
        else shape.Add(holder.Elements());
    }

    private string Safe(string id) => new([.. id.Select(c => char.IsLetterOrDigit(c) ? c : '_')]);
}

internal sealed class OdtTableWriter(OdtMeasure measure, OdtStyleNames names, OdtVocabulary vocabulary, OdtBorders borders, ITableAppearance appearance, OdtTableFormulas formulas, OdtCellNumbers numbers, OdtCellDirections directions) : OdtXml
{
    public const string OwnPadding = "cell-padding";

    public XElement Write(TableBlock table, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var width = new PulseOffice.Foundation.Units.Twip(table.Columns.Sum(column => column.Value));
        var tableStyle = sink.Register("table", table.StyleName is null ? null : names.Encode(table.StyleName),
            [new XElement(Style + "table-properties", new XAttribute(Style + "width", measure.Length(width)), new XAttribute(Table + "align", Align(table.Alignment)),
                table.Alignment == TableAlignment.Left && table.Indent.Value != 0 ? new XAttribute(Fo + "margin-left", measure.Length(table.Indent)) : null,
                table.PageBreakBefore && table.PageStyleName is null ? new XAttribute(Fo + "break-before", "page") : null),
                table.PageStyleName is { } master ? new XAttribute(Style + "master-page-name", names.Encode(master)) : null], "Tb");

        var element = new XElement(Table + "table",
            new XAttribute(Table + "name", table.Name ?? table.Id),
            table.Name is null ? new XAttribute(Pt + "unnamed", "true") : null,
            new XAttribute(Table + "style-name", tableStyle),
            new XAttribute(Xml + "id", table.Id),
            table.RepeatHeaderRows ? null : new XAttribute(Pt + "repeat-header", "false"));

        foreach (var column in table.Columns)
        {
            var columnStyle = sink.Register("table-column", null,
                [new XElement(Style + "table-column-properties", new XAttribute(Style + "column-width", measure.Length(column)))], "Col");
            element.Add(new XElement(Table + "table-column", new XAttribute(Table + "style-name", columnStyle)));
        }

        var look = appearance.Style(session.Document.Styles, table);
        var header = Math.Clamp(table.HeaderRows, 0, table.Rows.Count);
        if (header > 0)
            element.Add(new XElement(Table + "table-header-rows", Enumerable.Range(0, header).Select(r => Row(table, look, r, sink, session, blocks))));
        element.Add(Enumerable.Range(header, table.Rows.Count - header).Select(r => Row(table, look, r, sink, session, blocks)));
        return element;
    }

    private string Align(TableAlignment alignment) => alignment switch
    {
        TableAlignment.Center => "center",
        TableAlignment.Right => "right",
        _ => "left",
    };

    private XElement Row(TableBlock table, TableProperties look, int index, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var row = table.Rows[index];
        var properties = new XElement(Style + "table-row-properties");
        if (row.Height is { } height)
            properties.Add(new XAttribute(Style + (row.MinimumHeight ? "min-row-height" : "row-height"), measure.Length(height)));
        properties.Add(new XAttribute(Fo + "keep-together", row.AllowBreakAcrossPages ? "auto" : "always"));
        var element = new XElement(Table + "table-row", new XAttribute(Table + "style-name", sink.Register("table-row", null, [properties], "Row")));
        if (row.ChangeId is { } change && session.Document.Changes.Find(change) is Insertion or Deletion) element.Add(new XAttribute(Pt + "change-id", change));
        for (var column = 0; column < row.Cells.Count; column++) element.Add(Cell(table, look, index, column, sink, session, blocks));
        return element;
    }

    private XElement Cell(TableBlock table, TableProperties look, int row, int column, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var cell = table.Cell(row, column);
        if (cell.Covered)
        {
            var covered = new XElement(Table + "covered-table-cell");
            blocks.Write(covered, cell.Content.Blocks, sink, session);
            return covered;
        }

        var resolved = appearance.Cell(table, look, row, column);
        var inherited = new List<string>();
        var properties = new XElement(Style + "table-cell-properties",
            new XAttribute(Style + "vertical-align", vocabulary.VerticalAlign.Token(cell.VerticalAlign)));
        if (resolved.Background is { } background) properties.Add(new XAttribute(Fo + "background-color", measure.Hex(background)));
        if (resolved.Background is not null && !resolved.OwnBackground) inherited.Add("background");
        var padding = look.CellPadding ?? 2.8;
        var vertical = look.VerticalCellPadding ?? padding;
        var own = cell.Padding;
        borders.Write(properties, resolved.Borders with { PaddingTop = own?.Top ?? vertical, PaddingBottom = own?.Bottom ?? vertical, PaddingLeft = own?.Left ?? padding, PaddingRight = own?.Right ?? padding });
        if (table.Direct.CellPadding is null && table.Direct.VerticalCellPadding is null) inherited.Add("padding");
        if (own is not null) inherited.Add(OwnPadding);
        properties.Add(directions.Write(cell.TextDirection));
        if (!resolved.OwnBorders) inherited.Add("borders");
        var content = cell.Content.Blocks;
        if (resolved.Ink is not null || resolved.Bold)
        {
            content = [.. content.Select(block => block is ParagraphBlock paragraph ? Inked(paragraph, resolved) : block)];
            inherited.Add("ink");
        }
        var formula = cell.Formula is { } source ? formulas.Write(table, row, column, source, session.Document) : null;
        var data = cell.NumberFormat is { } format ? numbers.Register(sink, format, session.DataStyles, session.Document) : null;
        var values = formula is null && cell.NumberFormat is { } valued ? numbers.Values(cell, valued, session.Document).ToList() : [];
        var element = new XElement(Table + "table-cell",
            new XAttribute(Table + "style-name", sink.Register("table-cell", cell.StyleName is null ? null : names.Encode(cell.StyleName),
                [properties, data is null ? null : new XAttribute(Style + "data-style-name", data)], "Cell")),
            formula is null && values.Count == 0 ? new XAttribute(Office + "value-type", "string") : null,
            values,
            formula,
            cell.Protected ? new XAttribute(Table + "protected", "true") : null,
            cell.ColumnSpan > 1 ? new XAttribute(Table + "number-columns-spanned", cell.ColumnSpan) : null,
            cell.RowSpan > 1 ? new XAttribute(Table + "number-rows-spanned", cell.RowSpan) : null,
            new XAttribute(Pt + "from-table-style", string.Join(' ', inherited)),
            resolved.Ink is { } ink && inherited.Contains("ink") ? new XAttribute(Pt + "header-ink", measure.Hex(ink)) : null,
            resolved.Bold && inherited.Contains("ink") ? new XAttribute(Pt + "header-bold", "true") : null,
            cell.Formula is null ? null : new XAttribute(Pt + "formula", cell.Formula));
        blocks.Write(element, content, sink, session);
        return element;
    }

    private ParagraphBlock Inked(ParagraphBlock paragraph, CellAppearance look)
    {
        var content = paragraph.Content;
        var runs = content.Runs.Count == 0 ? [new TextRun(content.Text)] : content.Runs;
        return new ParagraphBlock(paragraph.Id, content.WithRuns([.. runs.Select(run => run with { Bold = run.Bold || look.Bold, Color = run.Color ?? look.Ink })])) { Marks = paragraph.Marks };
    }
}

internal sealed class OdtTableFormulas(ITableFormulaParser parser, ITableFormulaSyntax writer, ITableFormulaSyntax canonical, ITextCultures cultures) : OdtXml
{
    private sealed class Shape(TableBlock table, int row, int column) : ITableFormulaContext
    {
        public int Row => row;

        public int Column => column;

        public int RowCount => table.Rows.Count;

        public int ColumnCount => table.ColumnCount;

        public double? Value(int r, int c) => null;
    }

    public IReadOnlyList<XAttribute>? Write(TableBlock table, int row, int column, string formula, TextDocument document)
    {
        if (!parser.TryParse(formula, out var node) || node is null) return null;
        var text = string.Concat(table.Cell(row, column).Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)).Trim();
        var value = double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, cultures.For(CellLanguage(table.Cell(row, column)), document.Language), out var parsed) ? parsed : 0;
        return
        [
            new XAttribute(Table + "formula", "ooow:" + writer.Print(node, new Shape(table, row, column))),
            new XAttribute(Office + "value-type", "float"),
            new XAttribute(Office + "value", value.ToString("R", CultureInfo.InvariantCulture)),
        ];
    }

    public string? Read(string? attribute) =>
        attribute is not null && parser.TryParse(attribute, out var node) && node is not null ? canonical.Print(node, null) : null;

    private string? CellLanguage(TableCell cell) =>
        cell.Content.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Select(run => run.Language).FirstOrDefault(language => !string.IsNullOrEmpty(language));
}

internal sealed class OdtSectionWriter(OdtColumnsCodec columns, OdtIndexCodec indexes) : OdtXml
{
    public XElement Write(SectionBlock section, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var properties = new XElement(Style + "section-properties", columns.Write(section.Columns));
        if (!section.Columns.Balanced) properties.Add(new XAttribute(Text + "dont-balance-text-columns", "true"));
        if (section.Index is { } index) return indexes.Write(section, index, sink.Register("section", null, [properties], "Sect"), sink, session, blocks);
        var element = new XElement(Text + "section",
            new XAttribute(Text + "style-name", sink.Register("section", null, [properties], "Sect")),
            new XAttribute(Text + "name", section.Name),
            new XAttribute(Xml + "id", section.Id),
            section.Protected ? new XAttribute(Text + "protected", "true") : null,
            section.ProtectionKey is { } key ? new XAttribute(Text + "protection-key", Convert.ToBase64String(key)) : null,
            section.ProtectionKey is { Length: 32 } ? new XAttribute(Text + "protection-key-digest-algorithm", "http://www.w3.org/2000/09/xmldsig#sha256") : null,
            section.Condition is { } condition ? new XAttribute(Text + "condition", condition) : null,
            section.Hidden ? new XAttribute(Text + "display", "none") : section.Condition is not null ? new XAttribute(Text + "display", "condition") : null);
        if (section.LinkedFile is { } linked)
            element.Add(new XElement(Text + "section-source", new XAttribute(Xlink + "href", new LinkedFiles().ToPackageHref(linked)), new XAttribute(Xlink + "type", "simple")));
        blocks.Write(element, section.Content.Blocks, sink, session);
        return element;
    }
}

internal sealed class OdtColumnsCodec(OdtMeasure measure) : OdtXml
{
    public XElement Write(SectionColumns columns)
    {
        var element = new XElement(Style + "columns",
            new XAttribute(Fo + "column-count", Math.Max(1, columns.Count)),
            new XAttribute(Fo + "column-gap", measure.Length(columns.Gap)));
        if (columns.Separator)
            element.Add(new XElement(Style + "column-sep", new XAttribute(Style + "style", "solid"), new XAttribute(Style + "width", "0.5pt"), new XAttribute(Style + "color", "#000000")));
        return element;
    }

    public SectionColumns Read(XElement? properties)
    {
        var element = properties?.Element(Style + "columns");
        if (element is null) return SectionColumns.Single;
        var count = Math.Clamp(measure.Integer((string?)element.Attribute(Fo + "column-count")) ?? 1, 1, SectionColumns.LargestCount);
        var gap = measure.Twips((string?)element.Attribute(Fo + "column-gap"));
        if (gap is null && element.Elements(Style + "column").Skip(1).FirstOrDefault() is { } second)
            gap = (measure.Twips((string?)second.Attribute(Fo + "start-indent")) ?? PulseOffice.Foundation.Units.Twip.Zero) * 2;
        var balanced = (string?)properties!.Attribute(Text + "dont-balance-text-columns") != "true";
        if (count <= 1 && balanced && element.Element(Style + "column-sep") is null) return SectionColumns.Single;
        return new SectionColumns(Math.Max(1, count), gap ?? PulseOffice.Foundation.Units.Twip.Zero)
        {
            Separator = element.Element(Style + "column-sep") is { } sep && (string?)sep.Attribute(Style + "style") != "none",
            Balanced = balanced,
        };
    }
}

internal sealed class OdtCellNumbers(PulseOffice.Documents.Editing.Tables.ICellValueText values, ITextCultures cultures) : OdtXml
{
    private const double OldestDate = -657435.0;
    private const double NewestDate = 2958466.0;

    private readonly XNamespace Number = OdfPackage.Namespaces.Number;

    public string Register(OdtStyleSink sink, CellNumberFormat format, Dictionary<CellNumberFormat, string> known, TextDocument document)
    {
        if (known.TryGetValue(format, out var name)) return name;
        name = "PtN" + (known.Count + 1).ToString(CultureInfo.InvariantCulture);
        known[format] = name;
        sink.Add(DataStyle(format, name, cultures.For(document.Language)));
        return name;
    }

    public IEnumerable<XAttribute> Values(TableCell cell, CellNumberFormat format, TextDocument document)
    {
        var paragraph = cell.Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault();
        var text = paragraph?.Text ?? string.Empty;
        var culture = cultures.For(paragraph?.Content.Runs.Select(run => run.Language).FirstOrDefault(language => !string.IsNullOrEmpty(language)), document.Language);
        if (!values.TryValue(text, format, culture, out var value) && !values.TryValue(text, format, CultureInfo.InvariantCulture, out value)) return [];
        return format.Kind switch
        {
            CellValueKind.Percent => [new XAttribute(Office + "value-type", "percentage"), new XAttribute(Office + "value", Invariant(value))],
            CellValueKind.Currency => [new XAttribute(Office + "value-type", "currency"), new XAttribute(Office + "value", Invariant(value))],
            CellValueKind.Date when !Calendar(value) => [],
            CellValueKind.Date => [new XAttribute(Office + "value-type", "date"), new XAttribute(Office + "date-value", DateTime.FromOADate(value).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture))],
            _ => [new XAttribute(Office + "value-type", "float"), new XAttribute(Office + "value", Invariant(value))],
        };
    }

    public CellNumberFormat? Read(XElement? style)
    {
        if (style is null) return null;
        var number = style.Element(Number + "number");
        var decimals = int.TryParse((string?)number?.Attribute(Number + "decimal-places"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var places) ? places : 2;
        var grouping = (string?)number?.Attribute(Number + "grouping") == "true";
        return style.Name.LocalName switch
        {
            "percentage-style" => new CellNumberFormat(CellValueKind.Percent, decimals, grouping),
            "currency-style" => new CellNumberFormat(CellValueKind.Currency, decimals, grouping) { Symbol = (string?)style.Element(Number + "currency-symbol") },
            "date-style" => new CellNumberFormat(CellValueKind.Date, 0, false) { LongDate = (string?)style.Element(Number + "month")?.Attribute(Number + "textual") == "true" },
            "number-style" => new CellNumberFormat(CellValueKind.Number, decimals, grouping),
            _ => null,
        };
    }

    private XElement DataStyle(CellNumberFormat format, string name, CultureInfo culture)
    {
        var number = new XElement(Number + "number",
            new XAttribute(Number + "decimal-places", format.Decimals),
            new XAttribute(Number + "min-decimal-places", format.Decimals),
            new XAttribute(Number + "min-integer-digits", 1),
            format.Grouping ? new XAttribute(Number + "grouping", "true") : null);
        return format.Kind switch
        {
            CellValueKind.Percent => new XElement(Number + "percentage-style", new XAttribute(Style + "name", name), number, new XElement(Number + "text", "%")),
            CellValueKind.Currency => new XElement(Number + "currency-style", new XAttribute(Style + "name", name), number,
                new XElement(Number + "text", " "), new XElement(Number + "currency-symbol", format.Symbol ?? culture.NumberFormat.CurrencySymbol)),
            CellValueKind.Date => new XElement(Number + "date-style", new XAttribute(Style + "name", name),
                new XElement(Number + "day", new XAttribute(Number + "style", "long")), new XElement(Number + "text", format.LongDate ? " " : "/"),
                new XElement(Number + "month", new XAttribute(Number + "style", "long"), format.LongDate ? new XAttribute(Number + "textual", "true") : null),
                new XElement(Number + "text", format.LongDate ? " " : "/"), new XElement(Number + "year", new XAttribute(Number + "style", "long"))),
            _ => new XElement(Number + "number-style", new XAttribute(Style + "name", name), number),
        };
    }

    private bool Calendar(double value) => double.IsFinite(value) && value >= OldestDate && value < NewestDate;

    private string Invariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
