using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Diagnostics;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxReader : ITextDocumentReader
{
    private readonly DocxFlavour _flavour;
    private readonly IDefaultStyleSheetFactory _defaults;
    private readonly DocxStylesCodec _styles;
    private readonly DocxNumberingCodec _numbering;
    private readonly DocxBlockReader _blocks;
    private readonly DocxSectionCodec _sections;
    private readonly DocxAnnotationsCodec _annotations;
    private readonly OoxmlAttributeSanitizer _sanitizer = new();
    private readonly OoxmlReadFailures _failures = new();
    private readonly DocxRangeMarkPlacement _rangeMarks = new();
    private readonly DocxTablesInParagraphs _misplacedTables = new();
    private readonly DocxSmartTags _smartTags = new();
    private readonly UnknownPresetShapes _unknownPresets = new(new PulseOffice.Drawing.Shapes.ShapePresets());
    private readonly DocxParagraphMarkChanges _paragraphMarks = new();
    private readonly DocxEmbeddedFonts _fonts;
    private readonly DocxValues _values;
    private readonly DocxBibliography _bibliography;
    private readonly DocxStoredPageStyles _storedPages;
    private readonly DocxBlockProperties _blockProperties;
    private readonly IUnanchoredAnnotations _unanchored;
    private readonly DocxBookmarkReferences _references = new();
    private readonly DocxIndexRanges _indexRanges = new(new BlockTree());
    private readonly PulseOffice.Documents.Editing.ListThreads _threads = new PulseOffice.Documents.Editing.ListThreads(new PulseOffice.Documents.Editing.StoryCatalog(new BlockTree()), new BlockTree(), new PulseOffice.Documents.Styles.StyleResolver());
    private readonly DocxThemeFonts _themeFonts = new();
    private readonly DocxThemeColours _themeColours = new();

    private readonly IReadOnlyDictionary<string, Diagnostic> _losses = new Dictionary<string, Diagnostic>(StringComparer.Ordinal)
    {
        ["DOCX102"] = Diagnostic.Warning("DOCX102", "Some drawings or embedded objects in the DOCX file could not be imported."),
        ["DOCX108"] = Diagnostic.Warning("DOCX108", "Some content controls around tables are imported as their content, without the control."),
        ["DOCX104"] = Diagnostic.Warning("DOCX104", "VBA macros in the file are not imported."),
        ["DOCX113"] = Diagnostic.Info("DOCX113", "Comments and notes that are not anchored anywhere in the text, which Word does not show, are not imported."),
        ["DOCX117"] = Diagnostic.Info("DOCX117", "Tracked changes inside field instructions are not imported: each field keeps its current instruction."),
        ["DOCX120"] = Diagnostic.Warning("DOCX120", "Tracked changes in the text of grouped shapes are imported as accepted."),
        ["DOCX121"] = Diagnostic.Warning("DOCX121", "Tracked insertions, deletions and merges of single table cells are imported as accepted: the table keeps its current cells."),
        ["DOCX160"] = Diagnostic.Info("DOCX160", "Fields nested inside the instruction of a Word field are imported as the value they last showed, except merge fields, document variables and references compared by an IF field, which become names in its condition."),
        ["DOCX161"] = Diagnostic.Info("DOCX161", "Fields nested inside the result of a Word form field or hyperlink field are imported as part of that result: the inner field is not kept on its own."),
        ["DOCX151"] = Diagnostic.Info("DOCX151", "Word form, hyperlink, index-entry and table-of-contents fields whose instruction or result spans more than one paragraph or a table are imported as the text they last showed."),
        ["DOCX140"] = Diagnostic.Info("DOCX140", "Some VML shapes have a geometry PulseOffice does not recognise (a preset or a path computed from formulas): they are imported as rectangles."),
        ["DOCX141"] = Diagnostic.Info("DOCX141", "Elbow and curved VML connectors inside groups are imported as straight lines."),
        ["DOCX184"] = Diagnostic.Warning("DOCX184", "Gradient, picture and pattern fills of text boxes are not imported: the text box keeps its outline and, when it has one, a plain colour fill."),
        ["DOCX124"] = Diagnostic.Warning("DOCX124", "Shapes other than rectangles holding tables in their text are imported as rectangular text boxes with those tables: their geometry is lost."),
        ["DOCX152"] = Diagnostic.Warning("DOCX152", "The vertical alignment of text on the page (centered, bottom or justified) of Word sections is not imported: the text starts at the top of the page."),
        ["DOCX125"] = Diagnostic.Warning("DOCX125", "Headers and footers of a continuous section that starts in the middle of a page and holds only tables are not imported: the page keeps the headers and footers of the section before."),
        ["DOCX150"] = Diagnostic.Warning("DOCX150", "Tracked deletions of a paragraph mark before a table, at the end of a content control or on a section break are imported as rejected: the paragraph stays separate."),
        ["DOCX170"] = Diagnostic.Info("DOCX170", "Hyperlinks without any text, which Word does not show, are not imported."),
        ["DOCX182"] = Diagnostic.Warning("DOCX182", "Footnote and endnote marks whose note is missing from the file are not imported: the note text cannot be found."),
        ["DOCX183"] = Diagnostic.Warning("DOCX183", "Shapes with a preset geometry name that is not part of the DrawingML standard are imported as rectangles."),
        ["DOCX173"] = Diagnostic.Info("DOCX173", "Picture bullets of Word lists whose picture is missing are imported as the character bullet Word keeps beside them."),
        ["DOCX172"] = Diagnostic.Warning("DOCX172", "Floating tables longer than a page are imported as ordinary tables in the text, not as floating frames: their position and the text wrapping around them are lost."),
        ["DOCX222"] = Diagnostic.Warning("DOCX222", "Tables wider than 1,024 columns, larger than 1,000,000 cells, or beyond 2,000,000 table cells in the whole document are cut: the excess rows and columns are not imported."),
        ["DOCX221"] = Diagnostic.Warning("DOCX221", "Some fonts embedded in the DOCX file are damaged or in an unknown format and are not imported: the text uses an installed font instead."),
    };

    public DocxReader(DocxFlavour flavour, IDefaultStyleSheetFactory defaults, DocxStylesCodec styles, DocxNumberingCodec numbering,
        DocxBlockReader blocks, DocxSectionCodec sections, DocxAnnotationsCodec annotations, DocxEmbeddedFonts fonts, DocxValues values, DocxBibliography bibliography, IUnanchoredAnnotations unanchored, DocxStoredPageStyles storedPages, DocxBlockProperties blockProperties)
    {
        _storedPages = storedPages;
        _blockProperties = blockProperties;
        _unanchored = unanchored;
        _bibliography = bibliography;
        _values = values;
        _fonts = fonts;
        _flavour = flavour;
        _defaults = defaults;
        _styles = styles;
        _numbering = numbering;
        _blocks = blocks;
        _sections = sections;
        _annotations = annotations;
    }

    public TextDocumentFormat Format => _flavour.Format;

    public bool CanRead(Stream stream)
    {
        var start = stream.Position;
        try
        {
            using var repaired = new OoxmlPackageRepair().Repair(stream, new List<PartDamage>());
            using var document = WordprocessingDocument.Open(repaired, false);
            return document.MainDocumentPart?.Document is not null;
        }
        catch (Exception ex) when (_failures.IsMalformedInput(ex))
        {
            return false;
        }
        finally
        {
            stream.Position = start;
        }
    }

    public OperationResult<TextDocument> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            var damage = new List<PartDamage>();
            using var repaired = new OoxmlPackageRepair().Repair(stream, damage);
            using var package = WordprocessingDocument.Open(repaired, isEditable: true, new OpenSettings { AutoSave = false });
            _sanitizer.Sanitize(package);
            if (package.MainDocumentPart is not { Document.Body: { } body } main)
                return OperationResult<TextDocument>.Fail(Diagnostic.Error("DOCX100", "word/document.xml is missing."));

            _themeFonts.Resolve(main);
            _themeColours.Resolve(main);
            _smartTags.Unwrap(main);
            _misplacedTables.Lift(main);
            _rangeMarks.Place(main);
            var document = new TextDocument(_defaults.Create());
            var context = new DocxReadContext(document, main);
            foreach (var list in _numbering.Read(main.NumberingDefinitionsPart?.Numbering, context.Lists)) document.Styles.List.Replace(list);
            if (_numbering.LosesPictures(main.NumberingDefinitionsPart?.Numbering)) context.Losses.Add("DOCX173");
            if (_unknownPresets.Present(main.Document) || main.HeaderParts.Any(part => _unknownPresets.Present(part.Header)) || main.FooterParts.Any(part => _unknownPresets.Present(part.Footer)))
                context.Losses.Add("DOCX183");
            var styles = main.StyleDefinitionsPart?.Styles;
            _styles.Adopt(styles, document.Styles, context.Styles);
            _styles.ReadStyles(styles, document.Styles, context.Styles, context.Lists);
            var referenced = _references.Targets(main).ToHashSet(StringComparer.Ordinal);
            context.ReferencedBookmarks.UnionWith(referenced);
            context.ReferencedBookmarks.UnionWith(_references.IndexRanges(main));
            _annotations.ReadComments(main, context);
            _annotations.ReadNotes(main, context);
            context.Part = main;
            var spans = new List<DocxSectionSpan>();
            var blocks = _blocks.Read(body.ChildElements, context, spans);
            document.Body.Replace(blocks);
            Link(document, context);
            ResolveReferences(document);
            _indexRanges.Join(document, context.IndexRanges, referenced);
            _bibliography.Read(main, document);
            Sections(document, spans, main, context);
            _storedPages.Read(main, context);
            _blockProperties.Read(main, context);
            _threads.Settle(document);
            if (document.Body.Count == 0)
                document.Body.Add(new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.Standard));
            Settings(main.DocumentSettingsPart?.Settings, document);
            document.Notes.ContinuousEndnotes = true;
            if (_unanchored.Remove(document).Any) context.Losses.Add("DOCX113");
            if (body.Descendants<W.DeletedFieldCode>().Any(code => !WholeDeletedField(code)) || body.Descendants<W.InsertedRun>().Any(run => run.Descendants<W.FieldCode>().Any())) context.Losses.Add("DOCX117");
            if (body.Descendants<DocumentFormat.OpenXml.Office2010.Word.DrawingGroup.WordprocessingGroup>().Concat<OpenXmlElement>(body.Descendants<DocumentFormat.OpenXml.Vml.Group>())
                .Any(group => group.Descendants<W.InsertedRun>().Any() || group.Descendants<W.DeletedRun>().Any())) context.Losses.Add("DOCX120");
            if (body.Descendants<W.CellInsertion>().Any() || body.Descendants<W.CellDeletion>().Any() || body.Descendants<W.CellMerge>().Any()) context.Losses.Add("DOCX121");
            if (body.Descendants<W.Paragraph>().Any(paragraph => _paragraphMarks.Deletion(paragraph) is not null && paragraph.ParagraphProperties?.SectionProperties is not null)) context.Losses.Add("DOCX150");
            document.Fonts = _fonts.Read(main, main.DocumentSettingsPart?.Settings?.GetFirstChild<W.EmbedTrueTypeFonts>() is { } embed && (embed.Val?.Value ?? true), context.Losses);
            new OoxmlCorePropertiesMapper().Read(package, document.Properties);
            if (main.VbaProjectPart is not null) context.Losses.Add("DOCX104");
            var diagnostics = context.Losses.Select(code => _losses[code]).ToList();
            if (damage.Any(part => part.Kind != PartDamageKind.Trimmed)) diagnostics.Add(Diagnostic.Warning("DOCX105", "Some damaged parts of the DOCX file were repaired or skipped."));
            if (damage.Any(part => part.Kind == PartDamageKind.Trimmed))
                diagnostics.Add(Diagnostic.Warning("DOCX220", "Content nested deeper than PulseOffice can read, such as tables, controls or shapes inside one another hundreds of times, is not imported below that depth."));
            return OperationResult<TextDocument>.Ok(document, diagnostics);
        }
        catch (Exception ex) when (_failures.IsMalformedInput(ex))
        {
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("DOCX107", $"The DOCX file could not be read: {ex.Message}").With(ex.Message));
        }
    }

    private void ResolveReferences(TextDocument document)
    {
        var paragraphs = Paragraphs(document.Body).Concat(document.Notes.All.SelectMany(note => Paragraphs(note.Content))).ToList();
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var paragraph in paragraphs)
        {
            foreach (var bookmark in paragraph.Marks.OfType<BookmarkMark>().Where(mark => mark.Edge != MarkEdge.End))
            {
                if (paragraph.Marks.OfType<NoteMark>().FirstOrDefault(note => note.Offset == bookmark.Offset) is { } note && bookmark.Name.StartsWith("_Ref", StringComparison.Ordinal))
                    notes[bookmark.Name] = note.NoteId;
            }
        }
        var settable = paragraphs.SelectMany(paragraph => paragraph.Content.Runs)
            .Where(run => run.Field == TextFieldKind.VariableSet && run.FieldArgument is not null)
            .Select(run => run.FieldArgument!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var paragraph in paragraphs)
        {
            var runs = paragraph.Content.Runs;
            var resolved = runs.Select(run => Resolve(run, notes, settable)).ToList();
            if (!resolved.SequenceEqual(runs)) paragraph.Content = paragraph.Content.WithRuns(resolved);
            if (paragraph.Marks.Any(mark => mark is BookmarkMark bookmark && (notes.ContainsKey(bookmark.Name) || settable.Contains(bookmark.Name))))
                paragraph.Marks = [.. paragraph.Marks.Where(mark => mark is not BookmarkMark bookmark || !notes.ContainsKey(bookmark.Name) && !settable.Contains(bookmark.Name))];
        }
    }

    private bool WholeDeletedField(W.DeletedFieldCode code) =>
        code.Ancestors<W.DeletedRun>().FirstOrDefault() is { } deleted
        && deleted.Descendants<W.FieldChar>().Select(item => item.FieldCharType?.Value).ToList() is var types
        && types.Count(type => type == W.FieldCharValues.Begin) == types.Count(type => type == W.FieldCharValues.End) && types.Count > 0;

    private TextRun Resolve(TextRun run, Dictionary<string, string> notes, HashSet<string> settable)
    {
        if (run.FieldArgument is not { } name) return run;
        if (run.Field is TextFieldKind.NoteReference or TextFieldKind.Bookmark && notes.TryGetValue(name, out var id))
            return run with { Field = TextFieldKind.NoteReference, FieldArgument = id, FieldDetail = null };
        if (run.Field == TextFieldKind.Bookmark && run.FieldDetail is null && settable.Contains(name))
            return run with { Field = TextFieldKind.Variable };
        return run;
    }

    private IEnumerable<ParagraphBlock> Paragraphs(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        ParagraphBlock paragraph => [paragraph],
        SectionBlock section => Paragraphs(section.Content),
        TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Paragraphs(cell.Content)),
        _ => [],
    });

    private void Link(TextDocument document, DocxReadContext context)
    {
        var linkedCopies = new Dictionary<PulseOffice.Documents.Objects.AnchoredObject, PulseOffice.Documents.Objects.AnchoredObject>(ReferenceEqualityComparer.Instance);
        foreach (var chain in context.TextBoxLinks.GroupBy(link => link.Chain))
        {
            var members = chain.OrderBy(link => link.Sequence).Select(link => document.Objects.Find(link.ObjectId)).OfType<PulseOffice.Documents.Objects.AnchoredObject>().ToList();
            for (var index = 0; index + 1 < members.Count; index++)
            {
                var member = linkedCopies.GetValueOrDefault(members[index], members[index]);
                if (member.Content is not PulseOffice.Documents.Objects.TextBoxContent box) continue;
                var next = members[index + 1];
                var linked = new PulseOffice.Documents.Objects.TextBoxContent(box.Content) { AutoGrowHeight = false, NextFrameName = next.Name ?? next.Id, Look = box.Look };
                linkedCopies[members[index]] = member.CopyAs(member.Id, member.Anchor, linked);
            }
        }
        if (linkedCopies.Count > 0)
            document.Objects.Replace([.. document.Objects.All.Select(other => linkedCopies.GetValueOrDefault(other, other))]);
    }

    private void Sections(TextDocument document, List<DocxSectionSpan> spans, MainDocumentPart main, DocxReadContext context)
    {
        uint? background = _values.Color(main.Document?.DocumentBackground?.Color?.Value);
        var wraps = new List<(int First, int End, SectionColumns Columns)>();
        PageStyle? current = null;
        var converted = 0;
        var inheritance = new DocxSectionInheritance();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            var resolved = inheritance.Resolve(span.Properties);
            var end = index + 1 < spans.Count ? spans[index + 1].FirstBlock : document.Body.Count;
            var continuous = index > 0 && _sections.Continuous(span.Properties);
            var start = span.FirstBlock;
            var deferred = false;
            if (continuous && current is not null && inheritance.Changed)
            {
                var breaking = Enumerable.Range(span.FirstBlock, Math.Max(0, end - span.FirstBlock)).FirstOrDefault(position => BreaksPage(document, document.Body[position]), -1);
                if (breaking < 0) deferred = true;
                else
                {
                    start = breaking;
                    continuous = false;
                }
            }

            if (continuous && current is not null && !deferred)
            {
                var columns = _sections.Columns(span.Properties);
                if (columns != current.Columns && end > span.FirstBlock) wraps.Add((span.FirstBlock, end, columns));
                continue;
            }

            var name = index == 0 ? DefaultStyleNames.Standard : "Converted" + (++converted).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var style = _sections.Read(name, resolved, main, context, out var restart);
            style.Background ??= background;
            var previous = current;
            current = style;
            document.Styles.Page.Replace(style);
            if (index == 0) document.InitialPageStyle = name;
            else if (_sections.Starts.Usage(span.Properties) is { } usage)
            {
                var starter = _sections.Read(_sections.Starts.StarterName(name, usage), resolved, main, context, out _);
                starter.Background ??= background;
                starter.Usage = usage;
                starter.NextStyleName = name;
                document.Styles.Page.Replace(starter);
                name = starter.Name;
            }
            if (deferred)
            {
                var columns = _sections.Columns(span.Properties);
                if (columns != previous!.Columns && end > span.FirstBlock) wraps.Add((span.FirstBlock, end, columns));
                Defer(document, span.FirstBlock, end, name, restart, context);
                continue;
            }
            var first = start;
            if (first < document.Body.Count && document.Body[first] is TableBlock table && index > 0) table.PageStyleName = name;
            if (first >= document.Body.Count || document.Body[first] is not ParagraphBlock paragraph) continue;
            if (index == 0 && restart is null) continue;
            paragraph.Content = paragraph.Content with
            {
                PageStyleName = index == 0 ? paragraph.Content.PageStyleName : name,
                PageNumberRestart = restart,
            };
        }

        for (var position = wraps.Count - 1; position >= 0; position--)
        {
            var (first, end, columns) = wraps[position];
            var content = document.Body.Blocks.Skip(first).Take(end - first).ToList();
            document.Body.RemoveRange(first, end - first);
            document.Body.Insert(first, new SectionBlock(document.NextId("s"), "Section" + (position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), new BlockContainer(content)) { Columns = columns });
        }
    }

    private void Defer(TextDocument document, int first, int end, string name, int? restart, DocxReadContext context)
    {
        var position = Enumerable.Range(first, Math.Max(0, end - first)).FirstOrDefault(index => document.Body[index] is ParagraphBlock, -1);
        if (position < 0 || document.Body[position] is not ParagraphBlock paragraph)
        {
            context.Losses.Add("DOCX125");
            return;
        }
        paragraph.Content = paragraph.Content with { NextPageStyleName = name, PageNumberRestart = restart ?? paragraph.Content.PageNumberRestart };
    }

    private bool BreaksPage(TextDocument document, Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Content.PageBreakBefore == true
            || paragraph.StyleName is { } style && document.Styles.Paragraph.Find(style)?.Paragraph.PageBreakBefore == true,
        TableBlock table => table.PageBreakBefore,
        _ => false,
    };

    private const double WordDefaultTabInterval = 36;

    private void Settings(W.Settings? settings, TextDocument document)
    {
        document.Styles.TabsRelativeToIndent = false;
        document.Styles.DefaultTabInterval = settings?.GetFirstChild<W.DefaultTabStop>()?.Val?.Value is { } tabStop && tabStop >= 0 ? tabStop / 20.0 : WordDefaultTabInterval;
        if (settings is null) return;
        new DocxProtectionCodec().Read(settings.GetFirstChild<W.DocumentProtection>(), document);
        if (settings.GetFirstChild<W.TrackRevisions>() is { } track && (track.Val?.Value ?? true)) document.Changes.IsRecording = true;
        if (settings.GetFirstChild<W.RevisionView>() is { DisplayRevision.Value: false }) document.Changes.ShowChanges = false;
        if (settings.GetFirstChild<W.FormsDesign>() is { } design && (design.Val?.Value ?? true))
            document.FormSettings = document.FormSettings with { OpenInDesignMode = true };
        if (settings.GetFirstChild<W.EvenAndOddHeaders>() is { } even && (even.Val?.Value ?? true))
            foreach (var style in document.Styles.Page.All)
            {
                style.EvenPagesDifferent = true;
                BlankEvenPages(style.Header, document, DefaultStyleNames.Header);
                BlankEvenPages(style.Footer, document, DefaultStyleNames.Footer);
            }
        foreach (var variable in settings.GetFirstChild<W.DocumentVariables>()?.Elements<W.DocumentVariable>() ?? [])
            if (variable.Name?.Value is { } name) document.Variables[name] = variable.Val?.Value ?? string.Empty;
    }

    private void BlankEvenPages(HeaderFooterContent content, TextDocument document, string styleName)
    {
        if (content.Even is not null) content.Enabled = true;
        else if (content.Enabled)
            content.Even = new BlockContainer([new ParagraphBlock(document.NextId("p"), string.Empty,
                document.Styles.Paragraph.Contains(styleName) ? styleName : DefaultStyleNames.Standard)]);
    }
}

public sealed class DocxComposer
{
    private readonly DocxValues _values = new();
    private readonly IDirectFormatting _formatting;
    private readonly IDefaultStyleSheetFactory _defaults;

    private readonly SignatureLineImage _signatureImages;
    private readonly VectorFallback _vectors;

    public DocxComposer(IDirectFormatting formatting, IDefaultStyleSheetFactory defaults)
        : this(formatting, defaults, new SignatureLineImage())
    {
    }

    public DocxComposer(IDirectFormatting formatting, IDefaultStyleSheetFactory defaults, SignatureLineImage signatureImages)
        : this(formatting, defaults, signatureImages, new VectorFallback())
    {
    }

    public DocxComposer(IDirectFormatting formatting, IDefaultStyleSheetFactory defaults, SignatureLineImage signatureImages, VectorFallback vectors)
    {
        _formatting = formatting;
        _defaults = defaults;
        _signatureImages = signatureImages;
        _vectors = vectors;
    }

    public DocxWriter Writer(DocxFlavour flavour)
    {
        var parts = Parts();
        return new DocxWriter(flavour, parts.Styles, parts.Numbering, parts.Writer, parts.Sections, parts.Annotations, parts.Fonts, new DocxFidelityRules(parts.Drawings, new DocxBibliography()), _values, new DocxBibliography(),
            new DocxStoredPageStyles(parts.Sections, _defaults, new DocxCustomXmlParts()), BlockProperties());
    }

    public DocxReader Reader(DocxFlavour flavour)
    {
        var parts = Parts();
        return new DocxReader(flavour, _defaults, parts.Styles, parts.Numbering, parts.Reader, parts.Sections, parts.Annotations, parts.Fonts, _values, new DocxBibliography(),
            new UnanchoredAnnotations(new StoryCatalog(new BlockTree()), new BlockTree()), new DocxStoredPageStyles(parts.Sections, _defaults, new DocxCustomXmlParts()), BlockProperties());
    }

    private DocxBlockProperties BlockProperties() => new(new DocxCustomXmlParts(), new DocxListHeaderIndent(new StyleResolver()), new DocxBlockCarriers());

    private sealed record Composition(DocxStylesCodec Styles, DocxNumberingCodec Numbering, DocxBlockWriter Writer, DocxBlockReader Reader,
        DocxSectionCodec Sections, DocxAnnotationsCodec Annotations, DocxDrawingCodec Drawings, DocxEmbeddedFonts Fonts);

    private Composition Parts()
    {
        var characters = new DocxCharacterCodec(_values);
        var borders = new DocxBorders(_values);
        var paragraphs = new DocxParagraphCodec(_values, borders);
        var fields = new DocxFieldInstructions(new PreservedFieldTable(), new WordDatePictures());
        var shapeStyles = new XlsxShapeStyleMapper();
        var vectorPaths = new OoxmlVectorPaths();
        var memberLinks = new DocxGroupMemberLinks();
        IReadOnlyList<DocxGraphicCodec> leaves =
        [
            new DocxPictureCodec(new OoxmlImageTypes(), new PulseOffice.Drawing.Assets.ImageHeader(), _vectors),
            new DocxFontworkCodec(new FontworkWarps()),
            new DocxFreeformCodec(shapeStyles, vectorPaths),
            new DocxShapeCodec(shapeStyles, new PulseOffice.Drawing.Shapes.ShapePresets(), new PulseOffice.Documents.Objects.FrameChains(), new DocxTextBoxLook(shapeStyles)),
            new DocxChartCodec(new InlineChartParts()),
        ];
        var vmlStyles = new VmlStyle();
        var objects = new DocxObjectCodecs(
            new DocxFormulaCodec(new PulseOffice.Formulas.OmmlSyntax(), new PulseOffice.Formulas.FormulaParser(), new PulseOffice.Formulas.LinearFormulaWriter()),
            new DocxOleCodec(_values),
            new DocxFormControlCodec(),
            new DocxSignatureLineCodec(new VmlSignatureLineCodec(_signatureImages), vmlStyles, _values));
        var drawings = new DocxDrawingCodec([.. leaves, new DocxGroupCodec(leaves, vectorPaths, new PulseOffice.Documents.Objects.PortableObjects(), new PulseOffice.Documents.Objects.GroupStories(), memberLinks)], objects.All, _values);
        var revisions = new DocxRevisionWriter(_formatting, characters, paragraphs);
        var dropCaps = new DocxDropCaps(new PulseOffice.Documents.Editing.ParagraphText());
        var formulas = new DocxCellFormulas(new PulseOffice.Document.Tables.TableFormulaParser(), new PulseOffice.Document.Tables.CanonicalFormulaSyntax());
        var indexes = new DocxIndexCodec(new PulseOffice.Documents.Indexes.IndexStyles(), new DocxIndexSettings());
        var controls = new DocxContentControls(new PulseOffice.Documents.Controls.ContentControlLockTokens());
        var tree = new PulseOffice.Documents.Blocks.BlockTree();
        var catalog = new PulseOffice.Documents.Controls.ContentControlCatalog(new PulseOffice.Documents.Editing.StoryCatalog(tree), tree);
        var inlineWriter = new DocxInlineWriter(_formatting, characters, fields, drawings, revisions, controls, catalog, new PulseOffice.Documents.Marks.MarkOrder(), new PulseOffice.Documents.Blocks.BlockTree(), new DocxFieldCodeWriter(new PulseOffice.Documents.Editing.StoryCatalog(tree), tree));
        var tableStyles = new DocxTableStyleCodec(_values);
        var floatingTables = new DocxFloatingTables();
        var cellGeometry = new DocxCellGeometry();
        var tabPositions = new DocxTabPositions(new StyleResolver());
        var writer = new DocxBlockWriter(_formatting, paragraphs, inlineWriter, revisions, _values, dropCaps, borders, formulas, indexes, controls, tableStyles, floatingTables, new DocxListHeaderIndent(new StyleResolver()), tabPositions, new DocxParagraphMarkSplits(new PulseOffice.Documents.Editing.ParagraphText()), new DocxRowChanges(), cellGeometry);
        var vmlPictures = new DocxVmlPictures(new PulseOffice.Drawing.Assets.ImageHeader());
        var reader = new DocxBlockReader(_formatting, paragraphs, new DocxInlineReader(_formatting, characters, fields, drawings, new DocxVmlReader(vmlStyles, objects.Signatures, vmlPictures, new DocxVmlShapes(vmlStyles, new VmlColors(), new PulseOffice.Documents.Objects.GroupStories(), vmlPictures, new VmlPathParser(), new VmlShapePresets(), new FontworkWarps())), objects, controls, new DocxLegacyFormFields(new PulseOffice.Documents.Controls.LegacyFormFieldConverter()), new DocxParagraphMarkChanges(), new DocxFieldRangeReader(new DocxFieldRangeScan(fields))), _values, dropCaps, new DocxPageBreaks(), borders, formulas, new TableAppearance(new StyleResolver()), indexes, controls, new DocxTableStyleParagraphs(), tableStyles, floatingTables, new DocxParagraphMarkJoins(new DocxParagraphMarkChanges(), new PulseOffice.Documents.Editing.ParagraphText()), new DocxParagraphFrames(), new DocxRowChanges(), cellGeometry);
        var fonts = new DocxEmbeddedFonts(new PulseOffice.Typography.OpenType.OpenTypeReader(), new PulseOffice.Typography.OpenType.EmbeddedOpenTypeCodec(), new DocxFontObfuscation());
        return new Composition(new DocxStylesCodec(characters, paragraphs, tableStyles, tabPositions), new DocxNumberingCodec(_values, new DocxPictureBullets(new OoxmlImageTypes(), new VmlStyle())), writer, reader,
            new DocxSectionCodec(writer, reader, _values, borders, new DocxWatermarks(_values), new DocxHeaderFooterExtent(new StyleResolver()), new DocxSectionStarts(), new UnusedHeaderVariants(new BlockTree())), new DocxAnnotationsCodec(writer, reader), drawings, fonts);
    }
}
