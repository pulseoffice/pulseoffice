using System.Xml;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Formats.Packaging.Legacy;
using OdfListPictures = PulseOffice.Formats.Odf.TextCodecs.OdfListPictures;

namespace PulseOffice.Formats.Odf.Odt;

public sealed class OdtReader : ITextDocumentReader
{
    private const string LegacyWriter = "application/vnd.sun.xml.writer";
    private const string LegacyWriterTemplate = "application/vnd.sun.xml.writer.template";
    private const string LegacyWriterGlobal = "application/vnd.sun.xml.writer.global";

    private readonly OdfPackageEncryption _encryption = new();
    private readonly FlatOdfUnpacker _flat = new();
    private readonly LegacyOdfUpgrader _legacy = new();
    private readonly OdtDocumentReader _reader;

    public OdtReader()
    {
        var measure = new OdtMeasure();
        var names = new OdtStyleNames();
        var vocabulary = new OdtVocabulary();
        var direct = new DirectFormatting();
        var characters = new OdtCharacterCodec(measure, new OdtLanguageTag());
        var borders = new OdtBorders(measure);
        var paragraphCodec = new OdtParagraphCodec(measure, borders, names);
        var frames = new OdtFrameCodec(measure);
        var columns = new OdtColumnsCodec(measure);
        var lookup = new OdtStyleLookup(characters, paragraphCodec);
        var objects = new OdtObjectReader(measure, frames, new OdfImageTypes(), new OdfImageMapCodec(), new OdtDrawingReader(new PulseOffice.Documents.Objects.GroupStories()), new OdtContours(measure), new OdfSignatureLineCodec(new SignatureLineImage()));
        var inline = new OdtInlineReader(lookup, new OdtFieldTable(new OdtPreservedFields(new PreservedFieldTable(), new PulseOffice.Formats.Odf.TextCodecs.OdfParagraphText()), new PulseOffice.Formats.Odf.TextCodecs.OdfParagraphText()), direct, new OdtCommentReader(), new OdtNoteReader(vocabulary), objects,
            new OdtContentControls(new PulseOffice.Documents.Controls.ContentControlLockTokens()),
            new OdtLegacyFormFields(new PulseOffice.Documents.Controls.LegacyFormFieldConverter()));
        var tree = new PulseOffice.Documents.Blocks.BlockTree();
        var blocks = new OdtBlockReader(
            new OdtParagraphReader(lookup, inline, direct, new StyleResolver()),
            new OdtTableReader(measure, names, vocabulary, borders, new OdtTableFormulas(new PulseOffice.Document.Tables.TableFormulaParser(), new PulseOffice.Document.Tables.WriterFormulaSyntax(), new PulseOffice.Document.Tables.CanonicalFormulaSyntax(), new PulseOffice.Text.Rich.TextCultures()), new OdtCellNumbers(new PulseOffice.Documents.Editing.Tables.CellValueText(), new PulseOffice.Text.Rich.TextCultures()), new TableGridRepair(), new OdtCellDirections()),
            new OdtSectionReader(columns, new OdtIndexCodec(new OdtStyleNames(), new PulseOffice.Documents.Indexes.IndexStyles())),
            objects);

        var changeReader = new OdtChangeReader(new OdtCommentReader());
        _reader = new OdtDocumentReader(
            blocks,
            new OdtStylesReader(characters, paragraphCodec, frames, columns, measure, names, new OdtNumberFormats(), vocabulary, borders, new OdtWatermarks(measure), changeReader, new OdfListPictures(measure, new OdfImageTypes())),
            changeReader,
            new OdtFontFaceIndex(),
            names,
            new PulseOffice.Documents.Controls.ContentControlCatalog(new PulseOffice.Documents.Editing.StoryCatalog(tree), tree),
            new OdtReadLosses(),
            new PulseOffice.Documents.Editing.ListThreads(new PulseOffice.Documents.Editing.StoryCatalog(tree), tree, new StyleResolver()));
    }

    public TextDocumentFormat Format { get; } = new()
    {
        Id = "odt",
        DisplayName = "OpenDocument Text",
        Extensions = [".odt", ".fodt", ".ott", ".odm", ".sxw", ".stw", ".sxg"],
        MimeType = OdfPackage.MimeTypes.Text,
        IsNative = true,
    };

    public bool CanRead(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (_flat.IsFlat(stream))
            return _flat.MimeTypeOf(stream) is OdfPackage.MimeTypes.Text or OdtMimeTypes.TextTemplate or OdtMimeTypes.TextMaster;

        var start = stream.Position;
        try
        {
            if (_legacy.IsLegacy(stream))
            {
                using var upgraded = _legacy.Upgrade(stream);
                using var modern = OdfPackage.OpenRead(upgraded);
                return MimeType(modern) is OdfPackage.MimeTypes.Text or OdtMimeTypes.TextTemplate or OdtMimeTypes.TextMaster;
            }

            using var package = OdfPackage.OpenRead(stream);
            return MimeType(package) is OdfPackage.MimeTypes.Text or OdtMimeTypes.TextTemplate or OdtMimeTypes.TextMaster or LegacyWriter or LegacyWriterTemplate or LegacyWriterGlobal;
        }
        catch (XmlException)
        {
            return false;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return false;
        }
        finally
        {
            stream.Position = start;
        }
    }

    private string? MimeType(OdfPackage package)
    {
        if (package.ReadMimeType() is { Length: > 0 } declared) return declared;
        XNamespace manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        return package.ReadXml("META-INF/manifest.xml")?.Descendants(manifest + "file-entry")
            .Where(entry => (string?)entry.Attribute(manifest + "full-path") == "/")
            .Select(entry => (string?)entry.Attribute(manifest + "media-type"))
            .FirstOrDefault();
    }

    public OperationResult<TextDocument> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (_legacy.IsLegacy(stream))
        {
            MemoryStream upgraded;
            try
            {
                upgraded = _legacy.Upgrade(stream);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException)
            {
                return OperationResult<TextDocument>.Fail(Diagnostic.Error("ODT101", $"Unreadable ODT package: {ex.Message}").With(ex.Message));
            }

            using (upgraded) return Read(upgraded);
        }

        if (_encryption.RequiresOpenPgp(stream))
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("ODF030", "The document is encrypted for OpenPGP recipients: it opens only with the secret key of a recipient."));
        if (_encryption.IsEncrypted(stream))
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("ODF004", "The document is protected by a password."));

        if (_flat.IsFlat(stream))
        {
            MemoryStream unpacked;
            try
            {
                unpacked = _flat.Unpack(stream);
            }
            catch (Exception ex) when (ex is InvalidDataException or XmlException)
            {
                return OperationResult<TextDocument>.Fail(Diagnostic.Error("ODT101", $"Unreadable ODT package: {ex.Message}").With(ex.Message));
            }

            using (unpacked) return Read(unpacked);
        }

        try
        {
            using var package = OdfPackage.OpenRead(stream);
            var read = _reader.Read(package);
            if (read.Value is { } document && MimeType(package) == OdtMimeTypes.TextMaster) document.IsMaster = true;
            return read;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException
            or FormatException or ArgumentException or InvalidOperationException or ArithmeticException or KeyNotFoundException)
        {
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("ODT101", $"Unreadable ODT package: {ex.Message}").With(ex.Message));
        }
    }
}

internal sealed class OdtDocumentReader(
    OdtBlockReader blocks,
    OdtStylesReader styles,
    OdtChangeReader changes,
    OdtFontFaceIndex fonts,
    OdtStyleNames names,
    PulseOffice.Documents.Controls.IContentControlCatalog controls,
    OdtReadLosses losses,
    PulseOffice.Documents.Editing.IListThreads threads) : OdtXml
{
    private static readonly XNamespace Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";

    public OperationResult<TextDocument> Read(OdfPackage package)
    {
        using var content = package.OpenEntry("content.xml");
        if (content is null)
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("ODT101", "Unreadable ODT package: content.xml is missing.").With("content.xml"));

        XDocument? stylesDocument = package.ReadXml("styles.xml");
        var stylesRoot = stylesDocument?.Root;
        var document = new TextDocument();

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = true, IgnoreProcessingInstructions = true };
        var reader = XmlReader.Create(content, settings);
        reader.MoveToContent();
        if (reader.NamespaceURI == Office.NamespaceName && reader.LocalName == "document")
        {
            var single = XDocument.Load(reader);
            reader.Dispose();
            stylesDocument = single;
            stylesRoot = single.Root;
            reader = single.CreateReader();
            reader.MoveToContent();
        }

        using var owned = reader;
        if (reader.NamespaceURI != Office.NamespaceName || reader.LocalName is not ("document-content" or "document"))
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("ODT101", "Unreadable ODT package: content.xml is not an ODF document.").With("content.xml"));

        XElement? contentFonts = null;
        XElement? contentAutomatic = null;
        OdtReadContext? context = null;
        var bodyBlocks = new List<Block>();

        var bodyDepth = -1;
        var unnamedBody = false;
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            var unnamed = bodyDepth >= 0 && reader.Depth == bodyDepth + 1 && reader.NamespaceURI == Office.NamespaceName
                && reader.LocalName is not ("text" or "spreadsheet" or "presentation" or "drawing" or "chart" or "image" or "database");

            if (reader.NamespaceURI == Office.NamespaceName && reader.LocalName == "font-face-decls")
                contentFonts = (XElement)XNode.ReadFrom(reader);
            else if (reader.NamespaceURI == Office.NamespaceName && reader.LocalName == "automatic-styles")
                contentAutomatic = (XElement)XNode.ReadFrom(reader);
            else if (reader.NamespaceURI == Office.NamespaceName && (reader.LocalName == "body" || reader.LocalName == "text" || unnamed))
            {
                if (reader.LocalName == "body") bodyDepth = reader.Depth;
                if (reader.LocalName == "text" || unnamed)
                {
                    unnamedBody |= unnamed;
                    context ??= Prepare(document, package, stylesRoot, contentFonts, contentAutomatic, blocks);
                    document.IsMaster = reader.GetAttribute("global", Text.NamespaceName) == "true";
                    var initial = reader.GetAttribute("initial-page-style", Pt.NamespaceName);
                    if (initial is not null)
                    {
                        context.Ours = true;
                        document.InitialPageStyle = initial;
                    }
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        continue;
                    }
                    reader.Read();
                    ReadText(reader, context, bodyBlocks);
                    continue;
                }
                reader.Read();
            }
            else reader.Skip();
        }

        context ??= Prepare(document, package, stylesRoot, contentFonts, contentAutomatic, blocks);
        if (unnamedBody) context.Losses.Add(OdtReadLosses.UnnamedBody);
        document.Body.Replace(bodyBlocks);
        Finish(document, context);
        controls.Join(document);
        threads.Settle(document);

        new OdfMetaMapper().Read(package.ReadXml("meta.xml"), document.Properties);
        document.Fonts = new OdfFontFaceReader().Read(package, stylesDocument, new XDocument(new XElement(Office + "document-content", contentFonts)));
        new OdfVersionMapper().Read(package, document.Versions);
        var macroMapper = new OdfMacroMapper();
        document.MacroSignature = macroMapper.Read(package, document.Macros);
        Settings(package.ReadXml("settings.xml"), document);
        return OperationResult<TextDocument>.Ok(document, [.. losses.Describe(context.Losses), .. macroMapper.BasicLoss(package) is { } basicLoss ? [basicLoss] : Array.Empty<Diagnostic>()]);
    }

    private OdtReadContext Prepare(TextDocument document, OdfPackage package, XElement? stylesRoot, XElement? contentFonts, XElement? contentAutomatic, OdtBlockReader reader)
    {
        var faces = fonts.Read(stylesRoot?.Element(Office + "font-face-decls"), contentFonts);
        var context = new OdtReadContext(document, faces, package.ReadBytes, new OdtNameMap(names), new OdtNameMap(names));
        context.ReadXml = path => package.ReadXml(path.TrimStart('.', '/'));
        context.Drawings = new DrawingDefinitionTable().Read(new XElement(Office + "document-content", contentAutomatic is null ? null : new XElement(contentAutomatic)), stylesRoot);
        var officeStyles = stylesRoot?.Element(Office + "styles");
        var masters = stylesRoot?.Element(Office + "master-styles");
        styles.RegisterNames(officeStyles, masters, contentAutomatic, stylesRoot?.Element(Office + "automatic-styles"), context);
        styles.Read(officeStyles, contentAutomatic, stylesRoot?.Element(Office + "automatic-styles"), context);

        context.Automatic = new OdtAutomaticStyleIndex().Read(stylesRoot?.Element(Office + "automatic-styles"));
        styles.ReadPages(stylesRoot?.Element(Office + "automatic-styles"), masters, context, reader);
        context.Automatic = new OdtAutomaticStyleIndex().Read(contentAutomatic, officeStyles);
        return context;
    }

    private void ReadText(XmlReader reader, OdtReadContext context, List<Block> body)
    {
        var firstParagraph = true;
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.EndElement)
            {
                reader.Read();
                return;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            var element = (XElement)XNode.ReadFrom(reader);
            if (element.Name == Text + "tracked-changes") changes.Read(element, context, blocks);
            else if (element.Name == Text + "user-field-decls") Variables(element, context.Document);
            else if (element.Name == Text + "variable-decls") { }
            else if (element.Name == Office + "forms")
            {
                if (new OdfFormControlMapper().ReadSettings(element) is { } settings) context.Document.FormSettings = settings;
                foreach (var declaration in element.Descendants().Where(e => e.Attribute(Form + "id") is not null || e.Attribute(Xml + "id") is not null))
                    context.FormDeclarations.TryAdd((string?)declaration.Attribute(Xml + "id") ?? (string)declaration.Attribute(Form + "id")!, declaration);
            }
            else
            {
                var start = body.Count;
                blocks.Read(element, context, null, body);
                if (firstParagraph && body.Count > start)
                {
                    firstParagraph = false;
                    if (!context.Ours) InitialPage(body[start], element, context);
                }
            }
        }
    }

    private void InitialPage(Block first, XElement element, OdtReadContext context)
    {
        if (first is not ParagraphBlock paragraph || paragraph.Content.PageStyleName is not { } master) return;
        if (paragraph.Content.PageNumberRestart is null && paragraph.Content.PageBreakBefore != true)
        {
            context.Document.InitialPageStyle = master;
            paragraph.Content = paragraph.Content with { PageStyleName = null };
        }
    }

    private void Variables(XElement element, TextDocument document)
    {
        foreach (var declaration in element.Elements())
        {
            if ((string?)declaration.Attribute(Text + "name") is not { } name) continue;
            document.Variables[name] = (string?)declaration.Attribute(Office + "string-value") ?? (string?)declaration.Attribute(Office + "value") ?? string.Empty;
        }
    }

    private void Finish(TextDocument document, OdtReadContext context)
    {
        Thread(document, context);
        foreach (var paragraph in Paragraphs(document))
        {
            if (!paragraph.Marks.OfType<CommentMark>().Any(mark => mark.Edge == MarkEdge.Start && !context.EndedComments.Contains(mark.CommentId))) continue;
            paragraph.Marks = [.. paragraph.Marks.Select(mark => mark is CommentMark { Edge: MarkEdge.Start } comment && !context.EndedComments.Contains(comment.CommentId)
                ? comment with { Edge = MarkEdge.Point }
                : mark)];
        }
    }

    private void Thread(TextDocument document, OdtReadContext context)
    {
        if (context.CommentParents.Count == 0) return;
        foreach (var (id, parentName) in context.CommentParents)
            if (document.Comments.Find(id) is { } comment && context.CommentNames.TryGetValue(parentName, out var parent)) comment.ParentId = parent;
        document.Comments.DetachBrokenReplies();
        var replies = document.Comments.All.Where(comment => comment.IsReply).Select(comment => comment.Id).ToHashSet(StringComparer.Ordinal);
        if (replies.Count == 0) return;
        foreach (var paragraph in Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => mark is CommentMark found && replies.Contains(found.CommentId))).ToList())
            paragraph.Marks = [.. paragraph.Marks.Where(mark => mark is not CommentMark found || !replies.Contains(found.CommentId))];
    }

    private IEnumerable<ParagraphBlock> Paragraphs(TextDocument document)
    {
        var containers = new Stack<BlockContainer>();
        containers.Push(document.Body);
        foreach (var note in document.Notes.All) containers.Push(note.Content);
        foreach (var comment in document.Comments.All) containers.Push(comment.Content);
        foreach (var story in document.Objects.All.SelectMany(item => item.Content.Stories)) containers.Push(story);
        foreach (var page in document.Styles.Page.All)
        {
            containers.Push(page.Header.Default);
            containers.Push(page.Footer.Default);
            if (page.Header.First is { } hf) containers.Push(hf);
            if (page.Header.Even is { } he) containers.Push(he);
            if (page.Footer.First is { } ff) containers.Push(ff);
            if (page.Footer.Even is { } fe) containers.Push(fe);
        }

        while (containers.Count > 0)
        {
            foreach (var block in containers.Pop().Blocks)
            {
                switch (block)
                {
                    case ParagraphBlock paragraph:
                        yield return paragraph;
                        break;
                    case SectionBlock section:
                        containers.Push(section.Content);
                        break;
                    case TableBlock table:
                        foreach (var cell in table.Rows.SelectMany(row => row.Cells)) containers.Push(cell.Content);
                        break;
                }
            }
        }
    }

    private string? Item(XDocument? settings, string name) =>
        settings?.Descendants(Config + "config-item").FirstOrDefault(item => (string?)item.Attribute(Config + "name") == name)?.Value;

    private void Merge(XDocument? settings, TextDocument document)
    {
        var items = settings?.Descendants(Config + "config-item").GroupBy(item => (string?)item.Attribute(Config + "name") ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
        if (items is null || document.MergeSource is null && !items.ContainsKey("PulseMergeConnection")) return;
        if (!items.TryGetValue("CurrentDatabaseDataSource", out var database) || database.Length == 0) return;
        var type = int.TryParse(items.GetValueOrDefault("CurrentDatabaseCommandType"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number)
            && Enum.IsDefined(typeof(PulseOffice.Documents.Fields.MergeCommandType), number) ? (PulseOffice.Documents.Fields.MergeCommandType)number : PulseOffice.Documents.Fields.MergeCommandType.Table;
        document.MergeSource = new PulseOffice.Documents.Fields.MailMergeSource(database, items.GetValueOrDefault("CurrentDatabaseCommand") ?? string.Empty, type)
        {
            ConnectionId = items.GetValueOrDefault("PulseMergeConnection"),
        };
    }

    private void Protection(XDocument? settings, TextDocument document)
    {
        var items = settings?.Descendants(Config + "config-item").GroupBy(item => (string?)item.Attribute(Config + "name") ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        document.Marks = new PulseOffice.Documents.Protection.MarkProtection(
            items.GetValueOrDefault("ProtectFields") == "true", items.GetValueOrDefault("ProtectBookmarks") == "true");
        var restriction = items.TryGetValue("PulseDocumentRestriction", out var named) && Enum.TryParse<PulseOffice.Documents.Protection.DocumentRestriction>(named, out var parsed)
            ? parsed
            : items.GetValueOrDefault("ProtectForm") == "true" ? PulseOffice.Documents.Protection.DocumentRestriction.Forms
            : items.GetValueOrDefault("LoadReadonly") == "true" ? PulseOffice.Documents.Protection.DocumentRestriction.ReadOnly
            : PulseOffice.Documents.Protection.DocumentRestriction.None;
        if (restriction == PulseOffice.Documents.Protection.DocumentRestriction.None) return;
        PulseOffice.Documents.Protection.ProtectionKey? key = null;
        if (items.TryGetValue("PulseDocumentProtectionKey", out var encoded) && encoded.Split('|') is [var algorithm, var spin, var salt, var hash])
        {
            try
            {
                key = new PulseOffice.Documents.Protection.ProtectionKey(algorithm, Convert.FromBase64String(hash))
                {
                    Salt = Convert.FromBase64String(salt),
                    SpinCount = int.TryParse(spin, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var count) ? count : 0,
                };
            }
            catch (FormatException)
            {
                key = null;
            }
        }
        document.Protection = new PulseOffice.Documents.Protection.DocumentProtection(restriction, key);
    }

    private void Settings(XDocument? settings, TextDocument document)
    {
        Protection(settings, document);
        Merge(settings, document);
        document.Notes.ContinuousEndnotes = Item(settings, "ContinuousEndnotes") == "true";
        if (Item(settings, "ShowRedlineChanges") == "false") document.Changes.ShowChanges = false;
        document.Styles.TabsRelativeToIndent = Item(settings, "TabsRelativeToIndent") != "false";
        var key = settings?.Descendants(Config + "config-item")
            .FirstOrDefault(item => (string?)item.Attribute(Config + "name") == "RedlineProtectionKey")?.Value;
        if (string.IsNullOrWhiteSpace(key)) return;
        try
        {
            document.Changes.ProtectionKey = Convert.FromBase64String(key.Trim());
        }
        catch (FormatException)
        {
            document.Changes.ProtectionKey = null;
        }
    }
}

internal sealed class OdtChangeReader(OdtCommentReader comments) : OdtXml
{
    public void Read(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var log = context.Document.Changes;
        log.IsRecording = (string?)element.Attribute(Text + "track-changes") != "false";
        log.ShowChanges = (string?)element.Attribute(Pt + "show-changes") != "false";
        Regions(element, context, blocks);
    }

    public void Regions(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var log = context.Document.Changes;
        foreach (var region in element.Elements(Text + "changed-region"))
        {
            var id = (string?)region.Attribute(Text + "id") ?? (string?)region.Attribute(Xml + "id");
            if (id is null) continue;
            var body = region.Elements().FirstOrDefault();
            if (body is null) continue;

            var info = body.Element(Office + "change-info");
            var author = (string?)info?.Element(Dc + "creator") ?? string.Empty;
            var date = comments.Date((string?)info?.Element(Dc + "date"));
            var note = info?.Elements(Text + "p").Select(p => p.Value).ToList() is { Count: > 0 } lines ? string.Join("\n", lines) : null;

            TrackedChange? change = body.Name.LocalName switch
            {
                "insertion" => new Insertion(id, author, date),
                "deletion" => new Deletion(id, author, date, new BlockContainer(body.Elements().Where(child => child.Name != Office + "change-info")
                    .SelectMany(child =>
                    {
                        var list = new List<Block>();
                        blocks.Read(child, context, null, list);
                        return list;
                    })))
                { WithinInsertion = (string?)body.Attribute(Pt + "within-insertion") },
                "format-change" when (string?)body.Attribute(Pt + "kind") == "paragraph" => new ParagraphFormatChange(id, author, date, new PulseOffice.Text.Rich.TextParagraph(string.Empty)) { PreviousKnown = false },
                "format-change" => new FormatChange(id, author, date, []),
                _ => null,
            };
            if (change is null) continue;
            change.Comment = note;
            log.Add(change);
        }
    }
}
