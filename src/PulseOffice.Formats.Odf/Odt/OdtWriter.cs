using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Odt;

public sealed class OdtWriter : ITextDocumentWriter
{
    private readonly OdtDocumentWriter _writer;
    private readonly OdtFidelityRules _fidelity;

    public OdtWriter(OdtFlavour? flavour = null)
        : this(flavour, new SignatureLineImage())
    {
    }

    public OdtWriter(OdtFlavour? flavour, SignatureLineImage signatureImages)
    {
        Format = (flavour ?? OdtFlavour.Text).Format;

        var measure = new OdtMeasure();
        var names = new OdtStyleNames();
        var vocabulary = new OdtVocabulary();
        var direct = new DirectFormatting();
        var resolver = new StyleResolver();
        var characters = new OdtCharacterCodec(measure, new OdtLanguageTag());
        var borders = new OdtBorders(measure);
        var paragraphCodec = new OdtParagraphCodec(measure, borders, names);
        var frames = new OdtFrameCodec(measure);
        var columns = new OdtColumnsCodec(measure);
        var altText = new OdfAltTextMapper();
        var shapes = new OdfDeckShapeMapper();
        var drawings = new OdtDrawingWriter(new ObjectWriterChain(
        [
            new ChartObjectWriter(new OdfDeckChartMapper(), altText),
            new FormulaObjectWriter(shapes, new PulseOffice.Formulas.FormulaParser(), new PulseOffice.Formulas.MathMlSyntax(), altText),
            new TableObjectWriter(shapes, altText),
            new ShapeObjectWriter(shapes, altText),
            new SceneObjectWriter(shapes, altText),
            new FontworkObjectWriter(shapes, altText),
            new FreeformObjectWriter(shapes, altText),
            new ConnectorObjectWriter(shapes, altText),
            new ImageObjectWriter(shapes, new OdfUnits(), altText),
        ]), new OdfFormControlMapper());
        var marks = new OdtMarkWriter(new OdtCommentWriter(), new OdtNoteWriter(vocabulary),
            new OdtObjectWriter(measure, frames, names, new OdfImageTypes(), new OdfImageMapCodec(), drawings, new OdtContours(measure), new OdfSignatureLineCodec(signatureImages)));
        var tree = new PulseOffice.Documents.Blocks.BlockTree();
        var catalog = new PulseOffice.Documents.Controls.ContentControlCatalog(new PulseOffice.Documents.Editing.StoryCatalog(tree), tree);
        _fidelity = new OdtFidelityRules(catalog);
        var inline = new OdtInlineWriter(characters, new OdtFieldTable(new OdtPreservedFields(new PreservedFieldTable(), new OdfParagraphText()), new OdfParagraphText()), direct, names, new OdfParagraphText(), marks,
            new OdtContentControls(new PulseOffice.Documents.Controls.ContentControlLockTokens()),
            catalog,
            new PulseOffice.Documents.Marks.MarkOrder());
        var paragraphs = new OdtParagraphWriter(paragraphCodec, direct, resolver, names, inline);
        var blocks = new OdtBlockWriter(paragraphs, new OdtTableWriter(measure, names, vocabulary, borders, new TableAppearance(resolver), new OdtTableFormulas(new PulseOffice.Document.Tables.TableFormulaParser(), new PulseOffice.Document.Tables.WriterFormulaSyntax(), new PulseOffice.Document.Tables.CanonicalFormulaSyntax(), new PulseOffice.Text.Rich.TextCultures()), new OdtCellNumbers(new PulseOffice.Documents.Editing.Tables.CellValueText(), new PulseOffice.Text.Rich.TextCultures()), new OdtCellDirections()), new OdtSectionWriter(columns, new OdtIndexCodec(names, new PulseOffice.Documents.Indexes.IndexStyles())), resolver, names);

        var changeWriter = new OdtChangeWriter();
        _writer = new OdtDocumentWriter(
            blocks,
            marks,
            drawings,
            new OdtStylesWriter(characters, paragraphCodec, frames, columns, measure, names, new OdtNumberFormats(), vocabulary, borders, new OdtWatermarks(measure), changeWriter, new OdfListPictures(measure, new OdfImageTypes())),
            changeWriter,
            new PulseOffice.Documents.Editing.StoryCatalog(new PulseOffice.Documents.Blocks.BlockTree()));
    }

    public TextDocumentFormat Format { get; }

    public FidelityReport CheckFidelity(TextDocument document) => _fidelity.Check(document);

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var package = OdfPackage.Create(stream, Format.MimeType);
            _writer.Write(package, document, Format.MimeType);
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Xml.XmlException or ArgumentException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("ODT100", $"Writing the ODT failed: {ex.Message}").With(ex.Message));
        }
    }
}

public sealed class FlatOdtWriter(OdtWriter package) : ITextDocumentWriter
{
    private readonly FlatOdfPacker _packer = new();

    public FlatOdtWriter() : this(new OdtWriter())
    {
    }

    public TextDocumentFormat Format { get; } = new()
    {
        Id = "fodt",
        DisplayName = "Flat OpenDocument Text",
        Extensions = [".fodt"],
        MimeType = OdtMimeTypes.FlatText,
        IsNative = false,
    };

    public FidelityReport CheckFidelity(TextDocument document)
    {
        var losses = package.CheckFidelity(document).Losses.ToList();
        if (document.Macros.All.Count > 0 || document.Versions.Count > 0)
            losses.Add(Diagnostic.Info("ODT004", "A flat ODT is a single XML file: macros and saved versions are not written in it."));
        return new FidelityReport(losses);
    }

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        var result = package.Write(document, buffer);
        if (!result.Succeeded) return result;

        buffer.Position = 0;
        try
        {
            using var flat = _packer.Pack(buffer);
            flat.CopyTo(stream);
            return result;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or IOException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("ODT100", $"Writing the ODT failed: {ex.Message}").With(ex.Message));
        }
    }
}

public sealed record OdtFlavour(TextDocumentFormat Format)
{
    public static readonly OdtFlavour Text = new(new TextDocumentFormat
    {
        Id = "odt",
        DisplayName = "OpenDocument Text",
        Extensions = [".odt"],
        MimeType = OdfPackage.MimeTypes.Text,
        IsNative = true,
    });

    public static readonly OdtFlavour Template = new(new TextDocumentFormat
    {
        Id = "ott",
        DisplayName = "OpenDocument Text Template",
        Extensions = [".ott"],
        MimeType = OdtMimeTypes.TextTemplate,
        IsNative = false,
    });

    public static readonly OdtFlavour Master = new(new TextDocumentFormat
    {
        Id = "odm",
        DisplayName = "OpenDocument Master Document",
        Extensions = [".odm"],
        MimeType = OdtMimeTypes.TextMaster,
        IsNative = false,
    });
}

public static class OdtMimeTypes
{
    public const string TextTemplate = "application/vnd.oasis.opendocument.text-template";
    public const string TextMaster = "application/vnd.oasis.opendocument.text-master";
    public const string FlatText = "application/vnd.oasis.opendocument.text-flat-xml";
}

internal sealed class OdtDocumentWriter(
    OdtBlockWriter blocks,
    OdtMarkWriter marks,
    OdtDrawingWriter drawings,
    OdtStylesWriter styles,
    OdtChangeWriter changes,
    PulseOffice.Documents.Editing.IStoryCatalog stories) : OdtXml
{
    private static readonly XNamespace Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";

    public void Write(OdfPackage package, TextDocument document, string mimeType)
    {
        var session = new OdtWriteSession(document);
        var text = new XElement(Office + "text");
        if (document.IsMaster || mimeType == OdtMimeTypes.TextMaster) text.Add(new XAttribute(Text + "global", "true"));
        text.Add(new XAttribute(Pt + "initial-page-style", document.InitialPageStyle));
        if (document.InitialPageStyle != DefaultStyleNames.Standard) session.ImpliedInitialPage = document.InitialPageStyle;

        var declarations = new List<XElement>();
        if (document.Variables.Count > 0)
            declarations.Add(new XElement(Text + "user-field-decls", document.Variables.Select(variable =>
                new XElement(Text + "user-field-decl",
                    new XAttribute(Text + "name", variable.Key),
                    new XAttribute(Office + "value-type", "string"),
                    new XAttribute(Office + "string-value", variable.Value)))));

        var settable = stories.Paragraphs(document)
            .SelectMany(paragraph => paragraph.Content.Runs)
            .Where(run => run.Field is TextFieldKind.VariableSet or TextFieldKind.Variable && run.FieldArgument is { Length: > 0 } name && !document.Variables.ContainsKey(name))
            .Select(run => run.FieldArgument!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (settable.Count > 0)
            declarations.Add(new XElement(Text + "variable-decls", settable.Select(name =>
                new XElement(Text + "variable-decl", new XAttribute(Office + "value-type", "string"), new XAttribute(Text + "name", name)))));

        var sequences = stories.Paragraphs(document)
            .SelectMany(paragraph => paragraph.Content.Runs)
            .Where(run => run.Field == TextFieldKind.Sequence && run.FieldArgument is { Length: > 0 })
            .Select(run => run.FieldArgument!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (sequences.Count > 0)
            declarations.Add(new XElement(Text + "sequence-decls", sequences.Select(name =>
                new XElement(Text + "sequence-decl", new XAttribute(Text + "display-outline-level", "0"), new XAttribute(Text + "name", name)))));

        var content = new XElement(Office + "text");
        foreach (var item in document.Objects.All.Where(item => item.Anchor is PageAnchor))
            content.Add(marks.Unmarked(item, session.ContentStyles, session, blocks));
        blocks.Write(content, document.Body.Blocks, session.ContentStyles, session);
        session.ImpliedInitialPage = null;

        if ((drawings.Forms(document.FormSettings, session)
            ?? new OdfFormControlMapper().SettingsOnly(document.FormSettings, "Form")) is { } forms)
            text.Add(forms);
        var (automatic, masters) = styles.Pages(document, session, blocks);
        var elsewhere = session.RegionChanges.Except(changes.References(content), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (changes.Write(document, session, blocks, elsewhere) is { } tracked) text.Add(tracked);
        text.Add(declarations);
        text.Add(content.Nodes());

        var officeStyles = styles.Styles(document, session);
        officeStyles.Add(session.Drawing.Deck.ShapeStyles.Definitions, session.Drawing.Deck.FillImages());
        var (fonts, fontFiles) = new OdfFontFaceWriter().Build(session.Fonts.OrderBy(font => font, StringComparer.OrdinalIgnoreCase), document.Fonts);

        package.WriteXml("content.xml", new XDocument(new XElement(Office + "document-content",
            Declarations(),
            new XAttribute(Office + "version", "1.3"),
            new XElement(fonts),
            new XElement(Office + "automatic-styles", session.ContentStyles.Styles, session.Drawing.Deck.AutomaticStyles.Elements()),
            new XElement(Office + "body", text))));

        package.WriteXml("styles.xml", new XDocument(new XElement(Office + "document-styles",
            Declarations(),
            new XAttribute(Office + "version", "1.3"),
            new XElement(fonts),
            officeStyles,
            automatic,
            masters)));

        package.WriteXml("meta.xml", new OdfMetaMapper().Build(document.Properties));

        var manifest = new List<(string, string)> { ("content.xml", "text/xml"), ("styles.xml", "text/xml"), ("meta.xml", "text/xml") };
        foreach (var entry in session.Media)
        {
            package.WriteBytes(entry.Path, entry.Data);
            manifest.Add((entry.Path, entry.MediaType));
        }
        foreach (var entry in session.Drawing.Deck.Media)
        {
            package.WriteBytes(entry.Path, entry.Data);
            manifest.Add((entry.Path, entry.MediaType));
        }
        foreach (var subdocument in session.Drawing.Deck.Documents)
        {
            package.WriteXml($"{subdocument.Folder}/content.xml", subdocument.Document);
            manifest.Add(($"{subdocument.Folder}/", subdocument.MediaType));
            manifest.Add(($"{subdocument.Folder}/content.xml", "text/xml"));
        }
        foreach (var file in fontFiles)
        {
            package.WriteBytes(file.Path, file.Data);
            manifest.Add((file.Path, OdfFontFile.MediaType));
        }

        if (Settings(document, fontFiles.Count > 0) is { } settings)
        {
            package.WriteXml("settings.xml", settings);
            manifest.Add(("settings.xml", "text/xml"));
        }

        manifest.AddRange(new OdfVersionMapper().Write(package, document.Versions, OdfPackage.MimeTypes.Text));
        manifest.AddRange(new OdfMacroMapper().Write(package, document.Macros, document.MacroSignature));
        package.WriteManifest(mimeType, manifest);
    }

    private XDocument? Settings(TextDocument document, bool hasFonts)
    {
        var key = document.Changes.ProtectionKey;
        var protection = document.Protection;
        if (!hasFonts && !document.Fonts.EmbedSystemFonts && key is null && !protection.IsActive && document.MergeSource is null
            && document.Marks == PulseOffice.Documents.Protection.MarkProtection.None && document.LineNumbering is null && !document.Notes.ContinuousEndnotes && document.Changes.ShowChanges && document.Styles.TabsRelativeToIndent) return null;
        var settings = new OdfFontFaceWriter().Settings(document.Fonts);
        var set = settings.Descendants(Config + "config-item-set").First();
        if (key is not null) set.Add(Item("RedlineProtectionKey", "base64Binary", Convert.ToBase64String(key)));
        if (document.Notes.ContinuousEndnotes) set.Add(Item("ContinuousEndnotes", "boolean", "true"));
        if (!document.Changes.ShowChanges) set.Add(Item("ShowRedlineChanges", "boolean", "false"));
        if (!document.Styles.TabsRelativeToIndent) set.Add(Item("TabsRelativeToIndent", "boolean", "false"));
        if (document.Marks.Fields) set.Add(Item("ProtectFields", "boolean", "true"));
        if (document.Marks.Bookmarks) set.Add(Item("ProtectBookmarks", "boolean", "true"));
        if (document.MergeSource is { } merge)
        {
            set.Add(Item("CurrentDatabaseDataSource", "string", merge.Database));
            set.Add(Item("CurrentDatabaseCommand", "string", merge.Command));
            set.Add(Item("CurrentDatabaseCommandType", "int", ((int)merge.Type).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (merge.ConnectionId is { Length: > 0 } connection) set.Add(Item("PulseMergeConnection", "string", connection));
        }
        if (!protection.IsActive) return settings;
        set.Add(Item("PulseDocumentRestriction", "string", protection.Restriction.ToString()));
        if (protection.Key is { } lockKey)
            set.Add(Item("PulseDocumentProtectionKey", "string", string.Join('|', lockKey.Algorithm, lockKey.SpinCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToBase64String(lockKey.Salt), Convert.ToBase64String(lockKey.Hash))));
        if (protection.Restriction == PulseOffice.Documents.Protection.DocumentRestriction.ReadOnly) set.Add(Item("LoadReadonly", "boolean", "true"));
        if (protection.Restriction == PulseOffice.Documents.Protection.DocumentRestriction.Forms) set.Add(Item("ProtectForm", "boolean", "true"));
        return settings;
    }

    private XElement Item(string name, string type, string value) =>
        new(Config + "config-item", new XAttribute(Config + "name", name), new XAttribute(Config + "type", type), value);

    private IEnumerable<XAttribute> Declarations() =>
    [
        new(XNamespace.Xmlns + "office", Office.NamespaceName),
        new(XNamespace.Xmlns + "style", Style.NamespaceName),
        new(XNamespace.Xmlns + "text", Text.NamespaceName),
        new(XNamespace.Xmlns + "table", Table.NamespaceName),
        new(XNamespace.Xmlns + "draw", Draw.NamespaceName),
        new(XNamespace.Xmlns + "fo", Fo.NamespaceName),
        new(XNamespace.Xmlns + "xlink", Xlink.NamespaceName),
        new(XNamespace.Xmlns + "dc", Dc.NamespaceName),
        new(XNamespace.Xmlns + "meta", Meta.NamespaceName),
        new(XNamespace.Xmlns + "svg", Svg.NamespaceName),
        new(XNamespace.Xmlns + "form", Form.NamespaceName),
        new(XNamespace.Xmlns + "script", OdfPackage.Namespaces.Script.NamespaceName),
        new(XNamespace.Xmlns + "dom", OdfPackage.Namespaces.Dom.NamespaceName),
        new(XNamespace.Xmlns + "loext", Loext.NamespaceName),
        new(XNamespace.Xmlns + "officeooo", Officeooo.NamespaceName),
        new(XNamespace.Xmlns + "pt", Pt.NamespaceName),
        new(XNamespace.Xmlns + "ooow", "http://openoffice.org/2004/writer"),
        new(XNamespace.Xmlns + "ooo", OdfPackage.Namespaces.Ooo.NamespaceName),
        new(XNamespace.Xmlns + "chart", OdfPackage.Namespaces.Chart.NamespaceName),
    ];
}

internal sealed class OdtChangeWriter : OdtXml
{
    public XElement? Write(TextDocument document, OdtWriteSession session, IOdtBlockWriter blocks, IReadOnlySet<string> elsewhere)
    {
        var log = document.Changes;
        var owned = log.All.Where(change => !elsewhere.Contains(change.Id)).ToList();
        if (owned.Count == 0 && !log.IsRecording && log.ShowChanges) return null;

        var tracked = new XElement(Text + "tracked-changes", new XAttribute(Text + "track-changes", log.IsRecording ? "true" : "false"));
        if (!log.ShowChanges) tracked.Add(new XAttribute(Pt + "show-changes", "false"));
        foreach (var change in owned) tracked.Add(Region(change, session.ContentStyles, session, blocks));
        return tracked;
    }

    public void Embed(XElement region, TextDocument document, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var referenced = References(region);
        var owned = document.Changes.All.Where(change => referenced.Contains(change.Id)).ToList();
        if (owned.Count == 0) return;
        region.AddFirst(new XElement(Text + "tracked-changes", owned.Select(change => Region(change, session.HeaderStyles, session, blocks))));
        foreach (var change in owned) session.RegionChanges.Add(change.Id);
    }

    public IReadOnlySet<string> References(XElement container) => container.Descendants()
        .Where(mark => mark.Name == Text + "change" || mark.Name == Text + "change-start" || mark.Name == Text + "change-end")
        .Select(mark => (string?)mark.Attribute(Text + "change-id"))
        .Concat(container.Descendants(Table + "table-row").Select(row => (string?)row.Attribute(Pt + "change-id")))
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);

    private XElement Region(TrackedChange change, OdtStyleSink styles, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var info = new XElement(Office + "change-info",
            new XElement(Dc + "creator", change.Author),
            new XElement(Dc + "date", change.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", System.Globalization.CultureInfo.InvariantCulture)));
        if (change.Comment is { } comment) info.Add(new XElement(Text + "p", comment));

        XElement body = change switch
        {
            Insertion => new XElement(Text + "insertion", info),
            Deletion deletion => Deletion(deletion, info, styles, session, blocks),
            ParagraphFormatChange => new XElement(Text + "format-change", new XAttribute(Pt + "kind", "paragraph"), info),
            _ => new XElement(Text + "format-change", info),
        };
        return new XElement(Text + "changed-region", new XAttribute(Text + "id", change.Id), new XAttribute(Xml + "id", change.Id), body);
    }

    private XElement Deletion(Deletion deletion, XElement info, OdtStyleSink styles, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var element = new XElement(Text + "deletion", info);
        if (deletion.WithinInsertion is { } within) element.Add(new XAttribute(Pt + "within-insertion", within));
        blocks.Write(element, deletion.Removed.Blocks, styles, session);
        return element;
    }
}

internal sealed class OdtFidelityRules
{
    private readonly FidelityRuleSet<TextDocument> _rules;

    private readonly PulseOffice.Documents.Controls.IContentControlCatalog _controls;

    public OdtFidelityRules(PulseOffice.Documents.Controls.IContentControlCatalog controls)
    {
        _controls = controls;
        _rules = Create();
    }

    private FidelityRuleSet<TextDocument> Create() => new(
    [
        new FidelityRule<TextDocument>(
            document => document.Changes.All.Any(change => change is FormatChange or ParagraphFormatChange),
            Diagnostic.Info("ODT002", "ODF does not keep the previous formatting of a tracked format change: the change is kept, the former formatting is not.")),
        new FidelityRule<TextDocument>(
            document => document.Objects.All.Any(item => item.Content is EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject image }
                && image.Filter is not (PulseOffice.Drawing.Assets.ImageFilter.None or PulseOffice.Drawing.Assets.ImageFilter.Grayscale)),
            Diagnostic.Info("ODT003", "Image filters other than greyscale have no ODF equivalent: the original image is kept and the filter is lost.")),
        new FidelityRule<TextDocument>(
            document => document.Objects.All.Any(item => item.Hyperlink is { Length: > 0 } && item.Anchor is PageAnchor),
            Diagnostic.Warning("ODT009", "Hyperlinks on page-anchored objects have no ODF form: the object is kept and its hyperlink is lost in ODT. DOCX keeps it.")),
        new FidelityRule<TextDocument>(
            document => document.Objects.All.Any(item => item.Content is EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject { Duotone: not null } }),
            Diagnostic.Warning("ODT008", "Duotone images have no ODF equivalent: the original image is written and the two-colour recolouring is lost in ODT. DOCX keeps it.")),
        new FidelityRule<TextDocument>(
            document => document.Protection.Restriction is PulseOffice.Documents.Protection.DocumentRestriction.Comments or PulseOffice.Documents.Protection.DocumentRestriction.TrackedChanges
                        || document.Protection.Key is not null,
            Diagnostic.Info("ODT005", "Document protection for comments or tracked changes and its password are kept as PulseOffice settings: other ODF applications open the document without them.")),
        new FidelityRule<TextDocument>(
            document => SpanningControls(document),
            Diagnostic.Info("ODT006", "ODF content controls live inside one paragraph: a control over several paragraphs is written as one control per paragraph. PulseOffice joins them again, other ODF applications show them as separate controls.")),
        new FidelityRule<TextDocument>(
            document => document.Objects.All.Select(item => item.Content).OfType<PulseOffice.Documents.Objects.EmbeddedContent>()
                .Select(content => content.Value).OfType<PulseOffice.Charts.ChartDefinition>().Any(chart => _charts.KeptOnlyByExtension(chart.Type)),
            Diagnostic.Info("ODT182", "Chart types that ODF does not define, such as waterfall, statistical and other special charts, keep their type in a PulseOffice extension: other applications show them as the nearest standard chart, usually columns.")),
    ]);

    private readonly OdfChartVocabulary _charts = new();

    private bool SpanningControls(TextDocument document) => _controls.All(document).Any(span => !span.InOneParagraph);

    public FidelityReport Check(TextDocument document) => _rules.Check(document);
}
