using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxFidelityRules(DocxDrawingCodec drawings, DocxBibliography bibliography, IUnusedHeaderVariants? unusedHeaders = null, PulseOffice.Documents.Editing.IStoryCatalog? stories = null, DocxFieldInstructions? fields = null)
{
    private readonly DocxFieldInstructions _fields = fields ?? new DocxFieldInstructions(new PreservedFieldTable(), new WordDatePictures());

    private readonly DrawingStyleFidelity _drawing = new();

    private readonly DocxSectionStarts _starts = new();

    private readonly DocxBlockCarriers _carriers = new();

    private readonly IUnusedHeaderVariants _unusedHeaders = unusedHeaders ?? new UnusedHeaderVariants(new BlockTree());

    private readonly PulseOffice.Documents.Editing.IStoryCatalog _stories = stories ?? new PulseOffice.Documents.Editing.StoryCatalog(new BlockTree());

    private IEnumerable<PulseOffice.Drawing.Shapes.ShapeStyle> DrawingStyles(TextDocument document) =>
        document.Objects.All.Select(item => item.Content).OfType<PulseOffice.Documents.Objects.EmbeddedContent>()
            .Select(content => content.Value).OfType<PulseOffice.Drawing.Shapes.ShapeObject>().Select(shape => shape.Style);

    private readonly Lazy<StyleCollection<FrameStyle>> _frames = new(() => new DefaultStyleSheetFactory().Create().Frame);

    private readonly FormulaShapeFidelity _formulas = new();

    public FidelityRuleSet<TextDocument> Create() => new(
    [
        new(document => document.Objects.All.Select(item => item.Content).OfType<PulseOffice.Documents.Objects.EmbeddedContent>().Any(content => _formulas.LosesFormulas(content.Value)),
            Diagnostic.Warning("DOCX190", "Shapes whose geometry is computed from ODF equations are written to DOCX with their current outline: the equations, modifiers and handles are lost. ODT keeps them.")),
        new(document => document.Changes.All.OfType<PulseOffice.Documents.Annotations.Deletion>().Any(deletion => deletion.Removed.Blocks.Any(block => block is not ParagraphBlock)),
            Diagnostic.Warning("DOCX001", "Tracked deletions holding tables or sections are written to DOCX as deleted text on one line, without the tables and sections. ODT keeps them.")),
        new(document => DrawingStyles(document).Any(_drawing.LosesGradient),
            Diagnostic.Info("DOCX110", "DrawingML has no ellipsoid or square gradients and no gradient border: shapes take radial or rectangular gradients without border in DOCX. ODT keeps them.")),
        new(document => DrawingStyles(document).Any(_drawing.LosesHatch),
            Diagnostic.Info("DOCX111", "DrawingML hatches are fixed patterns: the angle and spacing of a shape's hatch are lost in DOCX. ODT keeps them.")),
        new(document => DrawingStyles(document).Any(_drawing.LosesBitmapTile),
            Diagnostic.Info("DOCX118", "DrawingML tiles a bitmap fill at the picture's own size: the tile width and height chosen for a shape are lost in DOCX. ODT keeps them.")),
        new(document => DrawingStyles(document).Any(_drawing.LosesDashShape),
            Diagnostic.Info("DOCX119", "DrawingML custom dashes are measured in line widths and have square ends: rounded dashes and dash lengths given in centimetres become relative lengths in DOCX. ODT keeps them.")),
        new(document => DrawingStyles(document).Any(_drawing.LosesArrowSize),
            Diagnostic.Info("DOCX112", "DrawingML arrowheads come in three sizes: other arrow scales become small, medium or large in DOCX. ODT keeps them.")),
        new(document => document.Objects.All.Any(item => !drawings.Writes(item)),
            Diagnostic.Warning("DOCX002", "Connectors, linked files, form controls without a Word content control, groups holding charts and 3D scenes are not written to DOCX; they are kept in ODT.")),
        new(document => document.Objects.All.Any(item => drawings.Inline(item) && item.Anchor is not PulseOffice.Documents.Objects.AsCharacterAnchor),
            Diagnostic.Info("DOCX011", "Formulas, embedded files and form controls are written inline in DOCX; their floating position is lost.")),
        new(document => Blocks(document.Body).OfType<SectionBlock>().Any(section => section.Index is { Kind: PulseOffice.Documents.Indexes.IndexKind.Objects }),
            Diagnostic.Warning("DOCX127", "Word has no table of objects: it is written as the text it shows now, without the index that updates it. ODT keeps it.")),
        new(document => Blocks(document.Body).OfType<SectionBlock>().Any(section => section.Index is null && !_carriers.Carries(section)),
            Diagnostic.Warning("DOCX003", "Sections that begin or end with a table become continuous Word sections with their columns: their names, protection, hidden state, conditions, links and nesting are lost in DOCX. ODT keeps them.")),
        new(document => document.Changes.All.Any(change => change.Comment is { Length: > 0 }),
            Diagnostic.Info("DOCX027", "Word has no comments on tracked changes: the notes attached to changes are lost in DOCX and kept in ODT.")),
        new(document => Blocks(document.Body).OfType<TableBlock>().Any(table => table.Rows.Any(row => row.Cells.Any(cell => cell.Protected))),
            Diagnostic.Warning("DOCX030", "Word has no protection for single table cells: protected cells become editable in DOCX. ODT keeps the protection.")),
        new(document => document.LineNumbering is { } lines && (lines.Side == PulseOffice.Documents.Styles.LineNumberSide.Right || !lines.CountBlankLines),
            Diagnostic.Info("DOCX032", "Word numbers lines only in the left margin and counts blank lines: numbering on the right or that skips blank lines is not kept in DOCX. ODT keeps it.")),
        new(document => document.Marks.Fields || document.Marks.Bookmarks,
            Diagnostic.Info("DOCX033", "Word has no protection of fields and bookmarks: it is lost in DOCX and kept in ODT.")),
        new(document => SplitControls(document),
            Diagnostic.Info("DOCX043", "Word content controls hold whole paragraphs or text inside one paragraph: a control starting or ending inside a paragraph and spanning several is written as one control per paragraph. ODT keeps it whole.")),
        new(document => Marks(document).OfType<ContentControlMark>().Any(mark => mark.Control is { ShowingPlaceholder: false, PlaceholderText.Length: > 0 }),
            Diagnostic.Info("DOCX044", "Word keeps the placeholder text of a filled content control in its glossary: it is not written to DOCX, and an emptied control shows the standard placeholder. ODT keeps it.")),
        new(document => document.IsMaster,
            Diagnostic.Warning("DOCX029", "Word has no master documents: the document is written as an ordinary one and its sub-documents keep their current text without the link. ODT and ODM keep them.")),
        new(document => document.MergeSource is not null,
            Diagnostic.Warning("DOCX028", "The mail merge data source is not written to DOCX: the merge fields stay as MERGEFIELD, and in Word the source has to be chosen again. ODT keeps it.")),
        new(document => document.Protection.Key is { IsWord: false } || document.Changes.ProtectionKey is not null,
            Diagnostic.Warning("DOCX026", "The password of document or tracked-change protection is not written to DOCX: the protection is kept, Word removes it without a password.")),
        new(document => document.Macros.All.Any(),
            Diagnostic.Warning("DOCX004", "JavaScript and Python macros and their signature cannot be stored in DOCX, which only accepts VBA in .docm; they are kept in ODT.")),
        new(document => document.FormSettings with { OpenInDesignMode = false } != new PulseOffice.Document.Forms.FormDocumentSettings(),
            Diagnostic.Warning("DOCX013", "Automatic focus and the data source of forms have no Word equivalent; they are dropped in DOCX and kept in ODT. Open in design mode is kept.")),
        new(document => document.Versions.Count > 0,
            Diagnostic.Warning("DOCX005", "Document versions have no OOXML equivalent; they are dropped in DOCX and kept in ODT.")),
        new(document => Runs(document).Any(run => run.Overline) || document.Styles.Character.All.Any(style => style.Character.Overline == true),
            Diagnostic.Warning("DOCX007", "Overline has no Word equivalent and is dropped in DOCX.")),
        new(document => Runs(document).Any(run => run.Case is TextCase.Lower or TextCase.Capitalize)
                        || document.Styles.Character.All.Any(style => style.Character.Case is TextCase.Lower or TextCase.Capitalize)
                        || document.Styles.Paragraph.All.Any(style => style.Character.Case is TextCase.Lower or TextCase.Capitalize),
            Diagnostic.Warning("DOCX014", "Word only knows the uppercase transformation: lowercase and capitalized text keep their typed letters in DOCX.")),
        new(document => document.Styles.Paragraph.All.Any(style => style.Paragraph.DropCap is { IsActive: true })
                        || Paragraphs(document).Any(paragraph => paragraph.DropCap is { IsActive: true, CharacterStyleName: not null }),
            Diagnostic.Warning("DOCX015", "Word keeps drop caps only as a framed first paragraph: drop caps set in paragraph styles and their character styles are lost in DOCX.")),
        new(document => document.Styles.Page.All.Select(style => style.Background is { } fill && fill >> 24 != 0 ? fill : (uint?)null).Distinct().Count() > 1,
            Diagnostic.Info("DOCX017", "Word has a single page background for the whole document: page styles with a different background take the first page's in DOCX.")),
        new(document => document.Styles.Paragraph.All.Any(style => style.Paragraph.ColumnBreakBefore == true),
            Diagnostic.Warning("DOCX018", "Word has no column break in paragraph styles: a style's column break is lost in DOCX and kept on directly formatted paragraphs.")),
        new(document => Blocks(document.Body).OfType<TableBlock>().Any(table => table.HeaderRows > 0 && !table.RepeatHeaderRows),
            Diagnostic.Info("DOCX019", "Word's heading rows always repeat on every page: a table heading set not to repeat repeats in DOCX.")),
        new(document => document.Bibliography.All.Any(bibliography.Loses),
            Diagnostic.Info("DOCX024", "Word's sources know fewer kinds and fields than the bibliography database: unmatched kinds become the nearest Word kind and unmatched fields are lost in DOCX.")),
        new(document => document.Objects.All.Any(item => item.Content is PulseOffice.Documents.Objects.EmbeddedContent { Value: PulseOffice.Document.Forms.SignatureLineObject { IsSigned: true } }),
            Diagnostic.Info("DOCX042", "Signature lines are kept in DOCX, but the signer and date shown on an already signed line are not: Word reads them from the digital signature. ODT keeps them.")),
        new(document => _stories.Paragraphs(document).SelectMany(paragraph => paragraph.Content.Runs).Any(run => run.Field == TextFieldKind.Sender),
            Diagnostic.Info("DOCX128", "Word has no sender fields for the user's address, company or contact details: they become the text they show in DOCX. ODT keeps them.")),
        new(document => Runs(document).Any(run => run.Field == TextFieldKind.HiddenText),
            Diagnostic.Warning("DOCX021", "Word has no conditional hidden text: the text is written with its current hidden state and the condition is lost in DOCX.")),
        new(document => Runs(document).Any(run => run.Field is TextFieldKind.ParagraphCount or TextFieldKind.TableCount or TextFieldKind.ImageCount or TextFieldKind.ObjectCount),
            Diagnostic.Info("DOCX022", "Word has no fields counting paragraphs, tables, images or objects: they become plain text in DOCX.")),
        new(document => Runs(document).Any(run => run.Field is TextFieldKind.Bookmark or TextFieldKind.Reference && run.FieldDetail?.Format is "chapter" or "caption" or "category-and-value" or "value"
                                                  || run.Field == TextFieldKind.Chapter && run.FieldArgument == "number-and-name"),
            Diagnostic.Info("DOCX023", "Word references show text, number, page or direction only: references by chapter, caption or value, and chapter fields with number and name, show the referenced text in DOCX.")),
        new(document => Runs(document).Any(run => run.Field == TextFieldKind.VariableSet && run.Text.Length > 0 && run.Hidden != true),
            Diagnostic.Info("DOCX223", "Word SET fields never show their value: variables set with a visible value keep the value in DOCX but no longer show it in the text.")),
        new(document => PageStyleNames(document).Any(name => name != DefaultStyleNames.Standard),
            Diagnostic.Info("DOCX009", "Page styles become Word sections: their names are not preserved.")),
        new(document => Paragraphs(document).Any(paragraph => paragraph.Widows is { } widows && widows != 0 && widows != 2
                                                               || paragraph.Orphans is { } orphans && orphans != 0 && orphans != 2),
            Diagnostic.Info("DOCX010", "Word only knows widow and orphan control on or off; other line counts become two lines.")),
        new(document => Paragraphs(document).Select(paragraph => paragraph.Borders).Concat(document.Styles.Paragraph.All.Select(style => style.Paragraph.Borders))
                .Any(borders => borders is not null && new[] { borders.PaddingTop, borders.PaddingBottom, borders.PaddingLeft, borders.PaddingRight }.Any(padding => Math.Abs(padding - Math.Round(padding)) > 0.001)),
            Diagnostic.Info("DOCX109", "Word measures the space between a paragraph border and its text in whole points: fractional distances are rounded in DOCX. ODT keeps them.")),
        new(document => Paragraphs(document).Any(paragraph => paragraph.LineHeight?.Mode == LineHeightMode.Leading),
            Diagnostic.Info("DOCX012", "Line spacing by added leading has no Word equivalent and becomes single spacing.")),
        new(document => document.Styles.Paragraph.All.Any(style => style.Paragraph.PageStyleName is not null || style.Paragraph.PageNumberRestart is not null),
            Diagnostic.Warning("DOCX035", "Word has no page break with a page style or a page number restart in paragraph styles: they are lost in DOCX and kept on directly formatted paragraphs. ODT keeps them.")),
        new(document => document.Styles.Paragraph.All.Select(style => style.Paragraph.Hyphenation).Concat(Paragraphs(document).Select(paragraph => paragraph.Hyphenation))
                .Any(hyphenation => hyphenation is { Enabled: true } && hyphenation with { Enabled = true } != ParagraphHyphenation.On),
            Diagnostic.Info("DOCX036", "Word knows hyphenation only on or off per paragraph: minimum word length, characters before and after the break, consecutive hyphens and skipping capitalized words are lost in DOCX. ODT keeps them.")),
        new(document => UsedPageStyles(document).Where(style => _starts.Mark(style, document) is null).Any(style => style.Usage != PageUsage.All
                        || style.NextStyleName is { } next && next != style.Name && !(style.Name == DefaultStyleNames.FirstPage && next == DefaultStyleNames.Standard)),
            Diagnostic.Info("DOCX037", "Word sections have no left-only, right-only or mirrored page styles and no following page style: page usage and the next page style are lost in DOCX. ODT keeps them.")),
        new(document => Blocks(document.Body).OfType<SectionBlock>().Any(section => section.Columns.Count > 1 && !section.Columns.Balanced)
                        || UsedPageStyles(document).Any(style => style.Columns.Count > 1 && !style.Columns.Balanced),
            Diagnostic.Info("DOCX038", "Word balances section columns: columns set not to balance are balanced in DOCX. ODT keeps the setting.")),
        new(document => document.Styles.List.All.Any(style => style.Levels.Any(level => level.CharacterStyleName is not null)),
            Diagnostic.Info("DOCX039", "The character style of list numbers is not written to DOCX: numbers take the paragraph's formatting. ODT keeps it.")),
        new(document => document.Styles.Frame.All.Any(style => _frames.Value.Find(style.Name) is not { } standard || standard.Frame != style.Frame || standard.ParentName != style.ParentName),
            Diagnostic.Info("DOCX040", "Word has no frame and graphic styles: their wrap, background, borders and padding stay on each object only as direct formatting, and the styles are lost in DOCX. ODT keeps them.")),
        new(document => Paragraphs(document).Any(paragraph => paragraph.DropCap is { IsActive: true, WholeWord: true }),
            Diagnostic.Info("DOCX041", "Word drop caps count letters: a drop cap set to the whole first word becomes the word's current letter count in DOCX.")),
        new(document => _unusedHeaders.HiddenEvenShownByDocumentSetting(document),
            Diagnostic.Info("DOCX045", "Word turns different even-page headers on for the whole document: hidden even-page headers and footers of page styles that do not show them are not written to DOCX when another page style shows its own. ODT keeps them.")),
        new(document => !document.Notes.ContinuousEndnotes && document.Notes.OfKind(PulseOffice.Documents.Annotations.NoteKind.Endnote).Any(),
            Diagnostic.Info("DOCX115", "Word places endnotes right after the text: endnotes collected on their own pages are written to continue after the last paragraph in DOCX. ODT keeps the setting.")),
        new(document => document.Changes.All.OfType<PulseOffice.Documents.Annotations.Deletion>()
                .SelectMany(deletion => Blocks(deletion.Removed)).OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs)
                .Any(run => run.Field != TextFieldKind.None && _fields.Instruction(run, document.Variables) is null && _fields.Composite(run, document.Variables, _ => false) is null),
            Diagnostic.Warning("DOCX195", "Fields Word does not have, inside tracked deletions, are written to DOCX as deleted text: conditional hidden text and paragraphs lose their content and condition, counters and sender fields become plain text. Other fields keep their instruction. ODT keeps them all.")),
        new(document => EmptyInsertion(document),
            Diagnostic.Info("DOCX116", "Word tracks only inserted text: tracked insertions that contain no text, only a comment or a bookmark, are not written to DOCX and the comment stays as an ordinary one. ODT keeps them.")),
        new(document => ChangeAcrossStructure(document),
            Diagnostic.Warning("DOCX114", "Tracked deletions of paragraph breaks and tracked changes covering whole tables are written on their text only in DOCX: the paragraph breaks stay and the tables are no longer marked. ODT keeps them.")),
        new(document => Charts(document).Any(_charts.LosesType),
            Diagnostic.Warning("DOCX135", "Chart types that DrawingML does not have, such as stepped lines, waterfall, statistical and other special charts, are written as column charts in DOCX. ODT keeps them. PulseOffice reads the type back from an extension.")),
        new(document => Charts(document).Any(_charts.FlattensThreeDimensional),
            Diagnostic.Info("DOCX136", "DrawingML has no three-dimensional line or column-and-line charts: these 3D charts are written flat in DOCX. ODT keeps the 3D view.")),
    ]);

    private readonly OoxmlChartFidelity _charts = new();

    private IEnumerable<PulseOffice.Charts.ChartDefinition> Charts(TextDocument document) =>
        document.Objects.All.Select(item => item.Content).OfType<PulseOffice.Documents.Objects.EmbeddedContent>()
            .Select(content => content.Value).OfType<PulseOffice.Charts.ChartDefinition>();

    private bool EmptyInsertion(TextDocument document)
    {
        var insertions = document.Changes.All.OfType<PulseOffice.Documents.Annotations.Insertion>().Select(change => change.Id).ToHashSet(StringComparer.Ordinal);
        return ParagraphBlocks(document).Any(paragraph => paragraph.Marks.OfType<ChangeMark>().Where(mark => insertions.Contains(mark.ChangeId)).GroupBy(mark => mark.ChangeId)
            .Any(marks => marks.Any(mark => mark.Edge == MarkEdge.Point)
                          || marks.FirstOrDefault(mark => mark.Edge == MarkEdge.Start) is { } start && marks.FirstOrDefault(mark => mark.Edge == MarkEdge.End) is { } end && start.Offset == end.Offset));
    }

    private bool ChangeAcrossStructure(TextDocument document)
    {
        var open = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in Blocks(document.Body))
        {
            if (block is TableBlock && open.Count > 0) return true;
            if (block is not ParagraphBlock paragraph) continue;
            foreach (var mark in paragraph.Marks.OfType<ChangeMark>())
            {
                if (document.Changes.Find(mark.ChangeId) is not (PulseOffice.Documents.Annotations.Insertion or PulseOffice.Documents.Annotations.Deletion)) continue;
                if (mark.Edge == MarkEdge.Start) open.Add(mark.ChangeId);
                else if (mark.Edge == MarkEdge.End) open.Remove(mark.ChangeId);
            }
            if (open.Any(id => document.Changes.Find(id) is PulseOffice.Documents.Annotations.Deletion)) return true;
        }
        return false;
    }

    private bool SplitControls(TextDocument document)
    {
        var starts = new Dictionary<string, (ParagraphBlock Paragraph, int Offset)>(StringComparer.Ordinal);
        foreach (var paragraph in ParagraphBlocks(document))
        {
            foreach (var mark in paragraph.Marks.OfType<ContentControlMark>())
            {
                if (mark.Edge == MarkEdge.Start) starts[mark.ControlId] = (paragraph, mark.Offset);
                else if (mark.Edge == MarkEdge.End && starts.TryGetValue(mark.ControlId, out var start) && !ReferenceEquals(start.Paragraph, paragraph)
                         && (start.Offset != 0 || mark.Offset != paragraph.Length))
                    return true;
            }
        }
        return false;
    }

    private IEnumerable<Block> Blocks(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        SectionBlock section => [block, .. Blocks(section.Content)],
        TableBlock table => [block, .. table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Blocks(cell.Content))],
        _ => (IEnumerable<Block>)[block],
    });

    private IEnumerable<ParagraphBlock> ParagraphBlocks(TextDocument document) =>
        Blocks(document.Body)
            .Concat(document.Notes.All.SelectMany(note => Blocks(note.Content)))
            .Concat(document.Comments.All.SelectMany(comment => Blocks(comment.Content)))
            .OfType<ParagraphBlock>();

    private IEnumerable<InlineMark> Marks(TextDocument document) => ParagraphBlocks(document).SelectMany(paragraph => paragraph.Marks);

    private IEnumerable<TextParagraph> Paragraphs(TextDocument document) => ParagraphBlocks(document).Select(paragraph => paragraph.Content);

    private IEnumerable<TextRun> Runs(TextDocument document) => Paragraphs(document).SelectMany(paragraph => paragraph.Runs);

    private IEnumerable<PageStyle> UsedPageStyles(TextDocument document) =>
        PageStyleNames(document).Distinct(StringComparer.Ordinal).Select(name => document.Styles.Page.Find(name)).OfType<PageStyle>();

    private IEnumerable<string> PageStyleNames(TextDocument document) =>
        Paragraphs(document).SelectMany(paragraph => new[] { paragraph.PageStyleName, paragraph.NextPageStyleName })
            .Concat(Blocks(document.Body).OfType<TableBlock>().Select(table => table.PageStyleName))
            .OfType<string>().Append(document.InitialPageStyle);
}

public sealed class DocxWriter : ITextDocumentWriter
{
    private const string PageObjectCarrier = "pulseoffice-page-objects";

    private readonly OoxmlPendingImages _pendingImages = new();

    private readonly DocxFlavour _flavour;
    private readonly DocxStylesCodec _styles;
    private readonly DocxNumberingCodec _numbering;
    private readonly DocxBlockWriter _blocks;
    private readonly DocxSectionCodec _sections;
    private readonly DocxAnnotationsCodec _annotations;
    private readonly FidelityRuleSet<TextDocument> _fidelity;
    private readonly DocxEmbeddedFonts _fonts;
    private readonly DocxValues _values;
    private readonly DocxBibliography _bibliography;
    private readonly DocxStoredPageStyles _storedPages;
    private readonly DocxBlockProperties _blockProperties;

    public DocxWriter(DocxFlavour flavour, DocxStylesCodec styles, DocxNumberingCodec numbering, DocxBlockWriter blocks,
        DocxSectionCodec sections, DocxAnnotationsCodec annotations, DocxEmbeddedFonts fonts, DocxFidelityRules fidelity, DocxValues values, DocxBibliography bibliography,
        DocxStoredPageStyles storedPages, DocxBlockProperties blockProperties)
    {
        _storedPages = storedPages;
        _blockProperties = blockProperties;
        _bibliography = bibliography;
        _values = values;
        _fonts = fonts;
        _flavour = flavour;
        _styles = styles;
        _numbering = numbering;
        _blocks = blocks;
        _sections = sections;
        _annotations = annotations;
        _fidelity = fidelity.Create();
    }

    public TextDocumentFormat Format => _flavour.Format;

    public FidelityReport CheckFidelity(TextDocument document) => _fidelity.Check(document);

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using (var package = WordprocessingDocument.Create(stream, _flavour.Type, true))
            {
                var main = package.AddMainDocumentPart();
                var context = new DocxWriteContext(document, main);
                var numbering = main.AddNewPart<NumberingDefinitionsPart>();
                _styles.Bind(document.Styles, context.Styles);
                numbering.Numbering = _numbering.Write(numbering, document.Styles.List.All, document.Styles.Outline, context.Lists,
                    level => context.Styles.IdOf(StyleFamily.Paragraph, document.Styles.Paragraph.All.FirstOrDefault(style => style.OutlineLevel == level && style.ListStyleName is null)?.Name));
                main.AddNewPart<StyleDefinitionsPart>().Styles = _styles.Write(document.Styles, context.Styles, context.Lists);
                _annotations.WriteComments(main, context);
                var body = new W.Body();
                WriteBody(document, main, context, body);
                _storedPages.Write(main, context);
                _blockProperties.Write(main, context);
                main.Document = Background(document) is { } colour
                    ? new W.Document(new W.DocumentBackground { Color = colour }, body)
                    : new W.Document(body);
                _annotations.WriteNotes(main, context);
                _numbering.WriteRestarts(numbering.Numbering, context.Lists);
                main.AddNewPart<DocumentSettingsPart>().Settings = Settings(document);
                _fonts.Write(main, document.Fonts);
                _bibliography.Write(main, document);
                new OoxmlCorePropertiesMapper().Write(package, document.Properties);
                _pendingImages.Resolve(package);
            }

            return OperationResult<bool>.Ok(true, CheckFidelity(document).Losses);
        }
        catch (Exception ex) when (ex is IOException or OpenXmlPackageException or InvalidOperationException or ArgumentException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("DOCX106", $"The DOCX file could not be written: {ex.Message}").With(ex.Message));
        }
    }

    private sealed class BodyState(string style, int? restart)
    {
        public string Style { get; set; } = style;

        public int? Restart { get; set; } = restart;

        public bool StartsContinuous { get; set; }

        public bool OwnHeaders { get; set; }

        public void Defer(TextParagraph lead)
        {
            Style = lead.NextPageStyleName!;
            Restart = lead.PageNumberRestart;
            StartsContinuous = true;
            OwnHeaders = true;
        }

        public void Break(string style, int? restart)
        {
            Style = style;
            Restart = restart;
            StartsContinuous = false;
            OwnHeaders = false;
        }
    }

    private W.SectionProperties Section(TextDocument document, BodyState state, MainDocumentPart main, DocxWriteContext context) =>
        _sections.Write(PageStyle(document, state.Style), state.Restart, state.StartsContinuous, !state.StartsContinuous || state.OwnHeaders, main, context);

    private TextParagraph? Deferring(Block block) => block switch
    {
        ParagraphBlock { Content: { PageStyleName: null, NextPageStyleName: not null } content } => content,
        SectionBlock section when section.Content.Blocks.FirstOrDefault() is { } lead => Deferring(lead),
        _ => null,
    };

    private void WriteBody(TextDocument document, MainDocumentPart main, DocxWriteContext context, W.Body body)
    {
        var blocks = document.Body.Blocks;
        foreach (var item in document.Objects.All.Where(item => item.Anchor is PulseOffice.Documents.Objects.PageAnchor)) context.PendingPageObjects.Add(item.Id);
        var state = new BodyState(
            blocks.FirstOrDefault() switch
            {
                ParagraphBlock { Content.PageStyleName: { } first } => first,
                TableBlock { PageStyleName: { } table } => table,
                { } first when Deferring(first) is { NextPageStyleName: { } deferred } => deferred,
                _ => document.InitialPageStyle,
            },
            blocks.FirstOrDefault() is ParagraphBlock { Content.PageNumberRestart: { } start } ? start : null);
        for (var index = 0; index < blocks.Count; index++)
        {
            switch (blocks[index])
            {
                case ParagraphBlock { Content.PageStyleName: { } next } paragraph when index > 0:
                    Close(body, Section(document, state, main, context));
                    state.Break(next, paragraph.Content.PageNumberRestart);
                    body.Append(_blocks.Block(paragraph, context));
                    break;
                case ParagraphBlock paragraph when index > 0 && Deferring(paragraph) is { } lead:
                    Close(body, Section(document, state, main, context));
                    state.Defer(lead);
                    body.Append(_blocks.Block(paragraph, context));
                    break;
                case TableBlock table when index > 0 && (table.PageStyleName is not null || table.PageBreakBefore):
                    Close(body, Section(document, state, main, context));
                    state.Break(table.PageStyleName ?? state.Style, null);
                    body.Append(_blocks.Block(table, context));
                    break;
                case SectionBlock { Index: not null } generated when generated.Columns.Count <= 1:
                    body.Append(_blocks.Block(generated, context));
                    break;
                case SectionBlock section:
                    var opening = !body.HasChildren;
                    if (!opening)
                    {
                        Close(body, Section(document, state, main, context));
                        state.Restart = null;
                    }
                    var deferring = opening ? null : Deferring(section);
                    if (deferring is not null) state.Defer(deferring);

                    var inner = section.Index is null ? _blocks.Write(section.Content, context).ToList() : _blocks.Block(section, context).ToList();
                    body.Append(inner);
                    var columns = PageStyle(document, state.Style).Clone() as PageStyle;
                    columns!.Columns = section.Columns;
                    Close(body, _sections.Write(columns, state.Restart, true, opening || deferring is not null, main, context));
                    state.Restart = null;
                    state.StartsContinuous = true;
                    state.OwnHeaders = false;
                    break;
                default:
                    if (_blocks.Controlled(blocks, index, context) is { } controlled)
                    {
                        body.Append(controlled.Element);
                        index = controlled.Last;
                        break;
                    }
                    body.Append(_blocks.Block(blocks[index], context));
                    break;
            }
        }

        if (blocks.Count == 0 || context.PendingPageObjects.Count > 0)
            body.Append(_blocks.Block(new ParagraphBlock(PageObjectCarrier, string.Empty, DefaultStyleNames.Standard), context));
        body.Append(Section(document, state, main, context));
    }

    private void Close(W.Body body, W.SectionProperties section)
    {
        if (body.LastChild is W.Paragraph previous && previous.ParagraphProperties?.SectionProperties is null)
        {
            previous.ParagraphProperties ??= new W.ParagraphProperties();
            if (previous.ParagraphProperties.GetFirstChild<W.ParagraphPropertiesChange>() is { } change) change.InsertBeforeSelf(section);
            else previous.ParagraphProperties.Append(section);
        }
        else
        {
            body.Append(new W.Paragraph(new W.ParagraphProperties(section)));
        }
    }

    private PageStyle PageStyle(TextDocument document, string name) => document.Styles.Page.Find(name) ?? new PageStyle(name);

    private string? Background(TextDocument document) =>
        document.Styles.Page.Find(document.InitialPageStyle)?.Background is { } fill && fill >> 24 != 0
            ? _values.Color(fill)
            : null;

    private W.Settings Settings(TextDocument document)
    {
        var settings = new W.Settings();
        if (Background(document) is not null) settings.Append(new W.DisplayBackgroundShape());
        if (document.Fonts.EmbedSystemFonts || document.Fonts.HasFiles) settings.Append(new W.EmbedTrueTypeFonts());
        if (document.FormSettings.OpenInDesignMode) settings.Append(new W.FormsDesign());
        if (!document.Changes.ShowChanges) settings.Append(new W.RevisionView { Markup = false, DisplayRevision = false, Formatting = false });
        if (document.Changes.IsRecording) settings.Append(new W.TrackRevisions());
        if (new DocxProtectionCodec().Write(document) is { } protection) settings.Append(protection);
        settings.Append(new W.DefaultTabStop { Val = (short)Math.Clamp(Math.Round(document.Styles.DefaultTabInterval * 20), 0, short.MaxValue) });
        if (document.Styles.Page.All.Any(style => style.EvenPagesDifferent)) settings.Append(new W.EvenAndOddHeaders());
        settings.Append(new W.Compatibility(new W.CompatibilitySetting
        {
            Name = W.CompatSettingNameValues.CompatibilityMode,
            Uri = "http://schemas.microsoft.com/office/word",
            Val = "15",
        }));
        if (document.Variables.Count > 0)
            settings.Append(new W.DocumentVariables(document.Variables.Select(pair => new W.DocumentVariable { Name = pair.Key, Val = pair.Value })));
        return settings;
    }
}
