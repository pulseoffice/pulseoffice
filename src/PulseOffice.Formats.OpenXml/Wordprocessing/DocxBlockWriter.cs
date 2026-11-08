using DocumentFormat.OpenXml;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxBlockWriter(IDirectFormatting formatting, DocxParagraphCodec paragraphs, DocxInlineWriter inlines, DocxRevisionWriter revisions, DocxValues values, DocxDropCaps drops, DocxBorders borders, DocxCellFormulas formulas, DocxIndexCodec indexes,
    DocxContentControls controls, DocxTableStyleCodec tableStyles, DocxFloatingTables floatingTables, DocxListHeaderIndent listHeaders, DocxTabPositions tabs, DocxParagraphMarkSplits splits, DocxRowChanges rowChanges, DocxCellGeometry geometry)
{
    private sealed class Visitor(DocxBlockWriter owner, DocxWriteContext context) : IBlockVisitor<IEnumerable<OpenXmlElement>>
    {
        public IEnumerable<OpenXmlElement> VisitParagraph(ParagraphBlock paragraph) => owner.Paragraphs(paragraph, context);

        public IEnumerable<OpenXmlElement> VisitTable(TableBlock table) => owner.Table(table, context);

        public IEnumerable<OpenXmlElement> VisitSection(SectionBlock section) =>
            section.Index is { } index
                ? owner.Index(section, index, [.. section.Content.Blocks.SelectMany(block => block.Accept(this))])
                : owner.Sequence(section.Content.Blocks, context, this);
    }

    public IEnumerable<OpenXmlElement> Write(BlockContainer container, DocxWriteContext context)
    {
        var visitor = new Visitor(this, context);
        var elements = Sequence(container.Blocks, context, visitor);
        if (elements.Count == 0 || elements[^1] is not W.Paragraph) elements.Add(new W.Paragraph());
        return elements;
    }

    private List<OpenXmlElement> Sequence(IReadOnlyList<Block> blocks, DocxWriteContext context, IBlockVisitor<IEnumerable<OpenXmlElement>> visitor)
    {
        var elements = new List<OpenXmlElement>();
        for (var index = 0; index < blocks.Count; index++)
        {
            if (Controlled(blocks, index, context, visitor) is { } controlled)
            {
                elements.Add(controlled.Element);
                index = controlled.Last;
                continue;
            }
            elements.AddRange(blocks[index].Accept(visitor));
        }
        return elements;
    }

    public (OpenXmlElement Element, int Last)? Controlled(IReadOnlyList<Block> blocks, int index, DocxWriteContext context) =>
        Controlled(blocks, index, context, new Visitor(this, context));

    private (OpenXmlElement Element, int Last)? Controlled(IReadOnlyList<Block> blocks, int index, DocxWriteContext context, IBlockVisitor<IEnumerable<OpenXmlElement>> visitor)
    {
        if (blocks[index] is not ParagraphBlock paragraph || Opening(paragraph) is not { } opening || Closing(blocks, index, opening.ControlId) is not { } last) return null;
        for (var covered = index; covered <= last; covered++)
            if (blocks[covered] is SectionBlock || covered > index && (blocks[covered] is ParagraphBlock { Content.PageStyleName: not null } or ParagraphBlock { Content.NextPageStyleName: not null })) return null;
        context.BlockControls.Add(opening.ControlId);
        var inner = new List<OpenXmlElement>();
        for (var covered = index; covered <= last; covered++) inner.AddRange(blocks[covered].Accept(visitor));
        return (new W.SdtBlock(controls.Properties(opening.Control ?? new PulseOffice.Documents.Controls.ContentControl()), new W.SdtContentBlock(inner)), last);
    }

    private PulseOffice.Documents.Marks.ContentControlMark? Opening(ParagraphBlock paragraph) =>
        paragraph.Marks.OfType<PulseOffice.Documents.Marks.ContentControlMark>().FirstOrDefault(mark => mark.Edge == PulseOffice.Documents.Marks.MarkEdge.Start && mark.Offset == 0
            && (mark.Control?.Block == true || !paragraph.Marks.OfType<PulseOffice.Documents.Marks.ContentControlMark>().Any(other => other.ControlId == mark.ControlId && other.Edge == PulseOffice.Documents.Marks.MarkEdge.End)));

    private int? Closing(IReadOnlyList<Block> blocks, int first, string controlId)
    {
        for (var index = first; index < blocks.Count; index++)
        {
            if (blocks[index] is not ParagraphBlock paragraph) continue;
            if (paragraph.Marks.OfType<PulseOffice.Documents.Marks.ContentControlMark>().FirstOrDefault(mark => mark.ControlId == controlId && mark.Edge == PulseOffice.Documents.Marks.MarkEdge.End) is not { } end) continue;
            return end.Offset == paragraph.Length ? index : null;
        }
        return null;
    }

    public IEnumerable<OpenXmlElement> Block(Block block, DocxWriteContext context) => block.Accept(new Visitor(this, context));

    private IEnumerable<OpenXmlElement> Index(SectionBlock section, PulseOffice.Documents.Indexes.IndexDefinition index, IReadOnlyList<OpenXmlElement> written) =>
        indexes.Write(section, index, written);

    private IEnumerable<OpenXmlElement> Paragraphs(ParagraphBlock block, DocxWriteContext context)
    {
        var floating = Floating(block, context).ToList();
        if (splits.Split(block, context) is { } segments) return [.. floating, .. segments.Select(segment => Paragraph(segment.Block, context, segment.DeletedMark))];
        if (drops.Split(block) is not { } split) return [.. floating, Paragraph(block, context)];
        var head = Paragraph(split.Head, context);
        (head.ParagraphProperties ??= new W.ParagraphProperties()).FrameProperties = drops.Frame(split.Cap);
        return [.. floating, head, Paragraph(split.Tail, context)];
    }

    private IEnumerable<OpenXmlElement> Floating(ParagraphBlock block, DocxWriteContext context)
    {
        foreach (var item in context.Document.Objects.AnchoredTo(block.Id).Where(floatingTables.IsFloatingTable).ToList())
        {
            context.FloatingTables.Add(item.Id);
            yield return Table(floatingTables.Table(item), context, floatingTables.Write(item));
        }
    }

    public W.Paragraph Paragraph(ParagraphBlock block, DocxWriteContext context, PulseOffice.Documents.Annotations.Deletion? deletedMark = null)
    {
        var content = block.Content;
        var outlined = OutlineNumbered(content, context);
        var extras = new DocxParagraphExtras(
            context.Styles.IdOf(StyleFamily.Paragraph, content.StyleName),
            outlined ? context.Lists.IdOf(StyleSheet.OutlineStyleName) : ListId(content, context),
            outlined ? content.OutlineLevel!.Value - 1 : content.BulletLevel,
            content.OutlineLevel);
        var headed = listHeaders.Apply(content, context.Document.Styles);
        var positioned = tabs.Paragraph(formatting.Paragraph(headed), headed, context.Document.Styles);
        var properties = paragraphs.Elements(positioned.Properties, extras with { ClearedTabs = positioned.Cleared }).ToList();
        foreach (var mark in block.Marks.OfType<PulseOffice.Documents.Marks.ChangeMark>())
            if (context.Document.Changes.Find(mark.ChangeId) is PulseOffice.Documents.Annotations.ParagraphFormatChange change)
                properties.Add(revisions.ParagraphChange(change, context));
        var paragraph = new W.Paragraph();
        if (content.ColumnBreakBefore == true && content.PageBreakBefore != true)
            paragraph.Append(new W.Run(new W.Break { Type = W.BreakValues.Column }));
        paragraph.Append(inlines.Write(block, context, container => Write(container, context)).ToList());
        var paragraphMark = new List<OpenXmlElement>();
        if (context.OpenInsertion is { } open && context.Document.Changes.Find(open) is { } inserted) paragraphMark.Add(revisions.InsertedMark(inserted, context));
        if (deletedMark is not null) paragraphMark.Add(revisions.DeletedMark(deletedMark, context));
        paragraphMark.AddRange(inlines.MarkProperties(block, context));
        if (paragraphMark.Count > 0)
        {
            var before = properties.FindIndex(element => element is W.ParagraphPropertiesChange or W.SectionProperties);
            properties.Insert(before < 0 ? properties.Count : before, new W.ParagraphMarkRunProperties(paragraphMark));
        }
        if (properties.Count > 0) paragraph.PrependChild(new W.ParagraphProperties(properties));
        context.WrittenParagraphs.TryAdd(block.Id, paragraph);
        return paragraph;
    }

    private bool OutlineNumbered(PulseOffice.Text.Rich.TextParagraph content, DocxWriteContext context)
    {
        if (content.IsListItem || content.ListStyleName is not null || content.OutlineLevel is not { } level || level < 1 || level > ListStyle.LevelCount) return false;
        var sheet = context.Document.Styles;
        if (sheet.Outline.Level(level - 1).Format == NumberFormat.None) return false;
        var style = content.StyleName is { } name ? sheet.Paragraph.Find(name) : null;
        return style is null || style.OutlineLevel != level || style.ListStyleName is not null;
    }

    private int? ListId(PulseOffice.Text.Rich.TextParagraph content, DocxWriteContext context)
    {
        if (listHeaders.IsHeader(content, context.Document.Styles)) return 0;
        if (!content.IsListItem && content.ListStyleName is null) return null;
        if (content.ListStyleName is { } name && content.ListId is { } thread)
        {
            if (content.ListRestart is null && context.Lists.IdOf(name, thread) is { } continued) return continued;
            var first = content.ListRestart ?? context.Document.Styles.List.Find(name)?.Level(content.BulletLevel).StartAt ?? 1;
            return context.Lists.Restart(name, thread, content.BulletLevel, first);
        }
        if (content.ListRestart is { } start && content.ListStyleName is { } restarted) return context.Lists.Restart(restarted, content.BulletLevel, start);
        return context.Lists.IdOf(content.ListStyleName);
    }

    private IEnumerable<OpenXmlElement> Table(TableBlock table, DocxWriteContext context) => [Table(table, context, null)];

    private W.Table Table(TableBlock table, DocxWriteContext context, W.TablePositionProperties? position)
    {
        var element = new W.Table();
        var properties = new W.TableProperties();
        if (context.Styles.IdOf(StyleFamily.Table, table.StyleName) is { } style) properties.Append(new W.TableStyle { Val = style });
        if (position is not null) properties.Append(position);
        properties.Append(new W.TableWidth { Type = W.TableWidthUnitValues.Auto, Width = "0" });
        if (table.Alignment != TableAlignment.Left)
            properties.Append(new W.TableJustification { Val = table.Alignment == TableAlignment.Center ? W.TableRowAlignmentValues.Center : W.TableRowAlignmentValues.Right });
        if (table.Alignment == TableAlignment.Left && table.Indent.Value != 0)
            properties.Append(new W.TableIndentation { Width = table.Indent.Value, Type = W.TableWidthUnitValues.Dxa });
        if (tableStyles.Margins(table.Direct) is { } margins) properties.Append(margins);
        properties.Append(new W.TableLook
        {
            Val = table.HeaderRows > 0 ? "04A0" : "0400",
            FirstRow = table.HeaderRows > 0,
            LastRow = false,
            FirstColumn = false,
            LastColumn = false,
            NoHorizontalBand = false,
            NoVerticalBand = true,
        });
        if (table.Name is not null) properties.Append(new W.TableCaption { Val = table.Name });
        element.Append(properties);
        element.Append(new W.TableGrid(table.Columns.Select(width => new W.GridColumn { Width = values.Text(width.Value) })));
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var source = table.Rows[row];
            var target = new W.TableRow();
            var rowProperties = new W.TableRowProperties();
            if (!source.AllowBreakAcrossPages) rowProperties.Append(new W.CantSplit());
            if (source.Height is { } height)
                rowProperties.Append(new W.TableRowHeight
                {
                    Val = (uint)Math.Max(0, height.Value),
                    HeightType = source.MinimumHeight ? W.HeightRuleValues.AtLeast : W.HeightRuleValues.Exact,
                });
            if (row < table.HeaderRows) rowProperties.Append(new W.TableHeader());
            if (rowChanges.Write(source, context) is { } change) rowProperties.Append(change);
            if (rowProperties.HasChildren) target.Append(rowProperties);
            for (var column = 0; column < source.Cells.Count;)
            {
                var cell = source.Cells[column];
                if (!cell.Covered)
                {
                    target.Append(Cell(table, cell, column, cell.RowSpan > 1 ? W.MergedCellValues.Restart : null, context));
                    column += Math.Max(1, cell.ColumnSpan);
                    continue;
                }

                var origin = Origin(table, row, column);
                if (origin is null)
                {
                    column++;
                    continue;
                }

                target.Append(Cell(table, origin, column, W.MergedCellValues.Continue, context, empty: true));
                column += Math.Max(1, origin.ColumnSpan);
            }

            element.Append(target);
        }

        return element;
    }

    private TableCell? Origin(TableBlock table, int row, int column)
    {
        for (var above = row - 1; above >= 0; above--)
        {
            if (column >= table.Rows[above].Cells.Count) return null;
            var candidate = table.Rows[above].Cells[column];
            if (candidate.Covered) continue;
            return above + candidate.RowSpan > row ? candidate : null;
        }

        return null;
    }

    private W.TableCell Cell(TableBlock table, TableCell cell, int column, W.MergedCellValues? merge, DocxWriteContext context, bool empty = false)
    {
        var span = Math.Max(1, cell.ColumnSpan);
        var width = table.Columns.Skip(column).Take(span).Sum(item => item.Value);
        var properties = new W.TableCellProperties(new W.TableCellWidth { Type = W.TableWidthUnitValues.Dxa, Width = values.Text(width) });
        if (span > 1) properties.Append(new W.GridSpan { Val = span });
        if (merge is { } value) properties.Append(new W.VerticalMerge { Val = value });
        if (cell.Borders is { } own) properties.Append(borders.Write(new W.TableCellBorders(), own with { PaddingTop = 0, PaddingBottom = 0, PaddingLeft = 0, PaddingRight = 0 }));
        if (cell.Background is { } background)
            properties.Append(new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = values.Color(background) });
        if (geometry.Margins(cell) is { } margins) properties.Append(margins);
        if (geometry.Direction(cell) is { } direction) properties.Append(direction);
        if (cell.VerticalAlign != CellVerticalAlign.Top)
            properties.Append(new W.TableCellVerticalAlignment
            {
                Val = cell.VerticalAlign == CellVerticalAlign.Middle ? W.TableVerticalAlignmentValues.Center : W.TableVerticalAlignmentValues.Bottom,
            });
        var element = new W.TableCell(properties);
        element.Append(empty ? [new W.Paragraph()] : Write(cell.Content, context));
        if (!empty && cell.Formula is { } formula) formulas.Write(element, formula);
        return element;
    }
}
