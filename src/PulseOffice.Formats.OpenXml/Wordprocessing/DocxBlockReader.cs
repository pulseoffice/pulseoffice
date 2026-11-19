using DocumentFormat.OpenXml;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxSectionSpan(int FirstBlock, W.SectionProperties Properties);

public sealed class DocxBlockReader(IDirectFormatting formatting, DocxParagraphCodec paragraphs, DocxInlineReader inlines, DocxValues values, DocxDropCaps drops, DocxPageBreaks breaks, DocxBorders borders, DocxCellFormulas formulas, ITableAppearance appearance, DocxIndexCodec indexes, DocxContentControls controls, DocxTableStyleParagraphs tableParagraphs, DocxTableStyleCodec tableStyles, DocxFloatingTables floatingTables, DocxParagraphMarkJoins markJoins, DocxParagraphFrames frames, DocxRowChanges rowChanges, DocxCellGeometry geometry, ICellInheritedEdges cellEdges)
{
    public List<Block> Read(IEnumerable<OpenXmlElement> elements, DocxReadContext context, List<DocxSectionSpan>? sections = null)
    {
        var blocks = new List<Block>();
        var sectionStart = 0;
        Collect(elements, context, blocks, sections, ref sectionStart);
        return blocks;
    }

    private sealed class OpenIndex(PulseOffice.Documents.Indexes.IndexDefinition definition, List<Block> outer, int columns)
    {
        public int Columns { get; } = columns;

        public PulseOffice.Documents.Indexes.IndexDefinition Definition { get; set; } = definition;

        public List<Block> Outer { get; } = outer;

        public List<Block> Content { get; } = [];
    }

    private void Collect(IEnumerable<OpenXmlElement> elements, DocxReadContext context, List<Block> blocks, List<DocxSectionSpan>? sections, ref int sectionStart)
    {
        OpenIndex? open = null;
        var breakNext = DocxPendingBreak.None;
        CollectWithIndexes(elements, context, blocks, sections, ref sectionStart, ref open, ref breakNext);
        if (open is not null) Close(open, context);
    }

    private void Close(OpenIndex open, DocxReadContext context)
    {
        var content = open.Content;
        if (content.Count == 0) content.Add(new ParagraphBlock(context.Document.NextId("p"), string.Empty, DefaultStyleNames.Standard));
        open.Outer.Add(new SectionBlock(context.Document.NextId("s"), "Index" + context.Document.NextId("i"), new BlockContainer(content))
        {
            Index = open.Definition,
            Protected = true,
            Columns = open.Columns > 1 ? new SectionColumns(open.Columns, new Twip(709)) : SectionColumns.Single,
        });
    }

    private void Opening(string instruction, string? data, DocxReadContext context, List<Block> blocks, ref OpenIndex? open)
    {
        if (open is not null) Close(open, context);
        var definition = indexes.Parse(instruction, data)!;
        open = new OpenIndex(definition, blocks, indexes.Columns(instruction));
        if (blocks.Count > 0 && blocks[^1] is ParagraphBlock title && indexes.IsTitle(definition, title.StyleName))
        {
            blocks.RemoveAt(blocks.Count - 1);
            open.Content.Add(title);
            open.Definition = definition with { Title = title.Text };
        }
    }

    private void KeepUnfolded(List<(ParagraphBlock Block, W.Paragraph Element)> folded, List<Block> target, DocxReadContext context, bool storyEnd)
    {
        var last = folded[^1];
        target.Add(folded.Count > 1 ? markJoins.Join(folded[..^1], last.Block, context) : last.Block);
        if (!storyEnd || last.Element.Parent is W.SdtContentBlock or W.CustomXmlBlock) context.Losses.Add("DOCX150");
        folded.Clear();
    }

    private void CollectWithIndexes(IEnumerable<OpenXmlElement> elements, DocxReadContext context, List<Block> blocks, List<DocxSectionSpan>? sections, ref int sectionStart, ref OpenIndex? open, ref DocxPendingBreak breakNext)
    {
        (ParagraphBlock Block, W.Paragraph Element)? pending = null;
        var floating = new List<(TableBlock Table, W.TablePositionProperties Position)>();
        var folded = new List<(ParagraphBlock Block, W.Paragraph Element)>();
        var framed = new List<(ParagraphBlock Block, W.Paragraph Element)>();
        var waitingFrames = new List<(IReadOnlyList<ParagraphBlock> Blocks, W.Paragraph First)>();
        void CloseFrame()
        {
            if (framed.Count == 0) return;
            waitingFrames.Add(([.. framed.Select(item => item.Block)], framed[0].Element));
            framed.Clear();
        }
        void AnchorFrames(ParagraphBlock anchor)
        {
            foreach (var (frameBlocks, first) in waitingFrames)
                context.Document.Objects.Add(frames.Build(frameBlocks, first, context.Document.NextId("frame"), anchor.Id, context));
            waitingFrames.Clear();
        }
        foreach (var element in elements)
        {
            if (framed.Count > 0 && (element is not W.Paragraph next || !frames.Frames(next, context) || frames.Key(next, context) != frames.Key(framed[0].Element, context)))
                CloseFrame();
            if (folded.Count > 0 && element is not W.Paragraph) KeepUnfolded(folded, open?.Content ?? blocks, context, storyEnd: false);
            if (pending is { } waiting && (element is not W.Paragraph || drops.IsDropCap((W.Paragraph)element)))
            {
                (open?.Content ?? blocks).Add(waiting.Block);
                pending = null;
            }
            switch (element)
            {
                case W.Paragraph positioned when pending is null && folded.Count == 0 && open is null && frames.Frames(positioned, context):
                    framed.Add((Broken(Paragraph(positioned, context), positioned, ref breakNext), positioned));
                    break;
                case W.Paragraph frame when drops.IsDropCap(frame) && frame.ParagraphProperties?.SectionProperties is null:
                    pending = (Broken(Paragraph(frame, context), frame, ref breakNext), frame);
                    break;
                case W.Paragraph paragraph:
                    var read = Broken(Paragraph(paragraph, context), paragraph, ref breakNext);
                    if (markJoins.Joins(paragraph))
                    {
                        if (pending is null && indexes.Scan(paragraph) is { Opening: null, Closes: false, Whole: null })
                        {
                            folded.Add((read, paragraph));
                            break;
                        }
                        context.Losses.Add("DOCX150");
                    }
                    if (folded.Count > 0)
                    {
                        read = markJoins.Join(folded, read, context);
                        folded.Clear();
                    }
                    var scan = indexes.Scan(paragraph);
                    if (scan.Whole is { } whole && open is null)
                    {
                        Opening(whole, scan.Data, context, blocks, ref open);
                        open!.Content.Add(read);
                        Close(open, context);
                        open = null;
                        pending = null;
                        break;
                    }
                    if (scan.Opening is { } instruction) Opening(instruction, scan.Data, context, blocks, ref open);
                    var added = pending is { } head ? drops.Merge(head.Block, head.Element, read) : read;
                    (open?.Content ?? blocks).Add(added);
                    Anchor(floating, added, context);
                    AnchorFrames(added);
                    pending = null;
                    if (open is not null && scan.Closes && scan.Opening is null)
                    {
                        Close(open, context);
                        open = null;
                    }
                    if (sections is not null && paragraph.ParagraphProperties?.SectionProperties is { } properties)
                    {
                        sections.Add(new DocxSectionSpan(sectionStart, properties));
                        sectionStart = blocks.Count;
                    }

                    break;
                case W.Table table when floatingTables.Position(table) is { } position:
                    var candidate = Table(table, context);
                    if (floatingTables.FitsOnOnePage(candidate)) floating.Add((candidate, position));
                    else
                    {
                        context.Losses.Add("DOCX172");
                        (open?.Content ?? blocks).Add(candidate);
                    }

                    break;
                case W.Table table:
                    (open?.Content ?? blocks).Add(Table(table, context));
                    break;
                case W.SdtBlock sdt:
                    var target = open?.Content ?? blocks;
                    var before = target.Count;
                    CollectWithIndexes(sdt.SdtContentBlock?.ChildElements ?? Enumerable.Empty<OpenXmlElement>(), context, blocks, sections, ref sectionStart, ref open, ref breakNext);
                    if (controls.IsContentControl(sdt.SdtProperties)) Wrap(sdt, target, before, context);
                    break;
                case W.CustomXmlBlock custom:
                    CollectWithIndexes(custom.ChildElements, context, blocks, sections, ref sectionStart, ref open, ref breakNext);
                    break;
                case W.SectionProperties final when sections is not null:
                    sections.Add(new DocxSectionSpan(sectionStart, final));
                    break;
            }
        }
        if (folded.Count > 0) KeepUnfolded(folded, open?.Content ?? blocks, context, storyEnd: true);
        if (pending is { } last)
        {
            (open?.Content ?? blocks).Add(last.Block);
            Anchor(floating, last.Block, context);
        }
        CloseFrame();
        if (floating.Count > 0 || waitingFrames.Count > 0)
        {
            var carrier = new ParagraphBlock(context.Document.NextId("p"), string.Empty, DefaultStyleNames.Standard);
            (open?.Content ?? blocks).Add(carrier);
            Anchor(floating, carrier, context);
            AnchorFrames(carrier);
        }
    }

    private void Anchor(List<(TableBlock Table, W.TablePositionProperties Position)> floating, ParagraphBlock anchor, DocxReadContext context)
    {
        foreach (var (table, position) in floating)
            context.Document.Objects.Add(floatingTables.Read(table, position, context.Document.NextId("frame"), anchor.Id));
        floating.Clear();
    }

    private void Wrap(W.SdtBlock sdt, List<Block> target, int before, DocxReadContext context) =>
        Wrap(sdt.SdtProperties, sdt.SdtContentBlock, target, before, context);

    private void Wrap(W.SdtProperties? properties, OpenXmlElement? content, List<Block> target, int before, DocxReadContext context)
    {
        if (target.Count <= before) return;
        if (target[before] is not ParagraphBlock first || target[^1] is not ParagraphBlock last)
        {
            context.Losses.Add("DOCX108");
            return;
        }
        var shown = string.Concat(content?.Descendants<W.Text>().Select(text => text.Text) ?? []);
        var control = controls.Read(properties, shown) with { Block = true };
        var id = context.Document.NextId("cc");
        first.Marks = [new PulseOffice.Documents.Marks.ContentControlMark(0, id, PulseOffice.Documents.Marks.MarkEdge.Start) { Control = control }, .. first.Marks];
        last.Marks = [.. last.Marks, new PulseOffice.Documents.Marks.ContentControlMark(last.Length, id, PulseOffice.Documents.Marks.MarkEdge.End)];
    }

    private ParagraphBlock Broken(ParagraphBlock block, W.Paragraph element, ref DocxPendingBreak breakNext)
    {
        var page = breaks.Place(element, W.BreakValues.Page);
        var column = breaks.Place(element, W.BreakValues.Column);
        if (breakNext == DocxPendingBreak.Page || page == DocxBreakPlace.Before)
            block.Content = block.Content with { PageBreakBefore = true };
        else if (breakNext == DocxPendingBreak.Column || column == DocxBreakPlace.Before)
            block.Content = block.Content with { ColumnBreakBefore = true };
        breakNext = page == DocxBreakPlace.After ? DocxPendingBreak.Page
            : column == DocxBreakPlace.After ? DocxPendingBreak.Column
            : DocxPendingBreak.None;
        return block;
    }

    public ParagraphBlock Paragraph(W.Paragraph element, DocxReadContext context)
    {
        var container = element.ParagraphProperties;
        var extras = paragraphs.Extras(container);
        var styleName = context.Styles.NameOf(extras.StyleId) ?? (extras.StyleId is null ? DefaultStyleNames.Standard : null);
        var id = context.Document.NextId("p");
        var content = inlines.Read(element, context, id, elements => Read(elements, context));
        var paragraph = Listed(new TextParagraph(string.Empty) { StyleName = styleName }, extras, context);
        paragraph = paragraph with { ListRestart = context.Lists.TakeRestart(extras.ListId) };
        paragraph = formatting.Set(paragraph, paragraphs.Read(container)).WithRuns(content.Runs);
        if (context.TableParagraphDefaults.TryPeek(out var tableDefaults) && tableDefaults is not null
            && tableParagraphs.Missing(tableDefaults, container, extras.StyleId, context) is { } inherited)
            paragraph = formatting.Set(paragraph, paragraphs.Read(inherited));
        var marks = content.Marks.ToList();
        if (container?.GetFirstChild<W.ParagraphPropertiesChange>() is { } change)
        {
            var before = change.ParagraphPropertiesExtended;
            var previousExtras = paragraphs.Extras(before);
            var previous = formatting.Set(Listed(new TextParagraph(paragraph.Text) { StyleName = context.Styles.NameOf(previousExtras.StyleId) ?? DefaultStyleNames.Standard },
                previousExtras, context), paragraphs.Read(before)).WithRuns(content.Runs);
            var tracked = new PulseOffice.Documents.Annotations.ParagraphFormatChange(context.Document.NextId("change"), change.Author?.Value ?? string.Empty,
                change.Date?.Value is { } date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)) : DateTimeOffset.MinValue, previous);
            context.Document.Changes.Add(tracked);
            marks.Insert(0, new PulseOffice.Documents.Marks.ChangeMark(0, tracked.Id, PulseOffice.Documents.Marks.MarkEdge.Point));
        }

        return new ParagraphBlock(id, paragraph) { Marks = marks };
    }

    private TextParagraph Listed(TextParagraph paragraph, DocxParagraphExtras extras, DocxReadContext context)
    {
        var listName = context.Lists.NameOf(extras.ListId);
        var level = listName is null ? null : context.Document.Styles.List.Find(listName)?.Level(extras.ListLevel);
        return paragraph with
        {
            ListStyleName = listName,
            ListSuppressed = listName is null && extras.ListId == 0,
            BulletLevel = listName is null ? 0 : extras.ListLevel,
            ListId = listName is null ? null : context.Lists.ThreadOf(extras.ListId),
            Bullet = level?.Format == NumberFormat.Bullet,
            Numbered = level is not null && level.Format is not NumberFormat.Bullet and not NumberFormat.None,
            OutlineLevel = extras.OutlineLevel,
        };
    }

    private TableBlock Table(W.Table element, DocxReadContext context)
    {
        context.TableParagraphDefaults.Push(tableParagraphs.Defaults(element.GetFirstChild<W.TableProperties>()?.TableStyle?.Val?.Value, context));
        try
        {
            return TableWithDefaults(element, context);
        }
        finally
        {
            context.TableParagraphDefaults.Pop();
        }
    }

    private const int LargestGridSpan = PulseOffice.Formats.ImportedTableLimits.TextTableColumns;
    private const int LargestLength = 158_400;

    private IEnumerable<T> Unwrapped<T>(IEnumerable<OpenXmlElement> elements) where T : OpenXmlElement
    {
        foreach (var element in elements)
        {
            if (element is T found) yield return found;
            else if (element is W.SdtRow or W.SdtCell or W.SdtContentRow or W.SdtContentCell or W.CustomXmlRow or W.CustomXmlCell)
            {
                foreach (var inner in Unwrapped<T>(element.ChildElements)) yield return inner;
            }
        }
    }

    private TableBlock TableWithDefaults(W.Table element, DocxReadContext context)
    {
        var columns = element.GetFirstChild<W.TableGrid>()?.Elements<W.GridColumn>()
            .Take(LargestGridSpan).Select(column => new Twip(values.Bounded(values.Integer(column.Width?.Value) ?? 0, 0, LargestLength))).ToList() ?? [];
        var rows = new List<TableRow>();
        var own = new List<(TableCell Cell, W.TableCellBorders Element)>();
        var closings = new List<(TableCell Origin, W.TableCellBorders Element)>();
        var margins = new List<(TableCell Cell, W.TableCellMargin Element)>();
        var headerRows = 0;
        var countingHeader = true;
        var widest = columns.Count;
        var open = new List<(TableCell Origin, int Column)?>();
        var skipped = new List<TableCell>();
        var cellWidths = new List<(int Column, int Span, W.TableCellWidth Width)>();
        foreach (var rowElement in Unwrapped<W.TableRow>(element.ChildElements))
        {
            var cells = new List<TableCell>();
            var reaching = new List<(TableCell Origin, int Column)?>();
            var before = Math.Clamp(rowElement.TableRowProperties?.GetFirstChild<W.GridBefore>()?.Val?.Value ?? 0, 0, Math.Max(0, (columns.Count > 0 ? columns.Count : LargestGridSpan) - 1));
            for (var index = 0; index < before; index++)
            {
                var placeholder = new TableCell(new BlockContainer());
                skipped.Add(placeholder);
                cells.Add(placeholder);
                reaching.Add((placeholder, index));
            }
            foreach (var cellElement in Unwrapped<W.TableCell>(rowElement.ChildElements))
            {
                if (cells.Count >= LargestGridSpan)
                {
                    context.Losses.Add(TruncatedTable);
                    break;
                }
                var properties = cellElement.TableCellProperties;
                var span = Math.Clamp(properties?.GridSpan?.Val?.Value ?? 1, 1, Math.Max(1, (columns.Count > 0 ? columns.Count : LargestGridSpan) - cells.Count));
                var merge = properties?.VerticalMerge;
                var column = cells.Count;
                if (merge is not null && merge.Val?.Value != W.MergedCellValues.Restart && Continued(open, column, span) is { } origin)
                {
                    origin.RowSpan++;
                    if (properties?.TableCellBorders?.GetFirstChild<W.BottomBorder>() is not null) closings.Add((origin, properties.TableCellBorders));
                    for (var index = 0; index < span; index++)
                    {
                        cells.Add(new TableCell(new BlockContainer()) { Covered = true });
                        reaching.Add((origin, column));
                    }
                    continue;
                }

                var cellBlocks = Read(cellElement.ChildElements, context).ToList();
                if (cellElement.Parent is W.SdtContentCell { Parent: W.SdtCell wrapper } && controls.IsContentControl(wrapper.SdtProperties))
                    Wrap(wrapper.SdtProperties, cellElement, cellBlocks, 0, context);
                var cell = new TableCell(new BlockContainer(cellBlocks))
                {
                    ColumnSpan = span,
                    Background = values.Color(properties?.Shading?.Fill?.Value),
                    VerticalAlign = properties?.TableCellVerticalAlignment?.Val?.Value is { } align
                        ? align == W.TableVerticalAlignmentValues.Center ? CellVerticalAlign.Middle
                        : align == W.TableVerticalAlignmentValues.Bottom ? CellVerticalAlign.Bottom
                        : CellVerticalAlign.Top
                        : CellVerticalAlign.Top,
                    Formula = formulas.Read(cellElement),
                    TextDirection = geometry.Direction(properties?.TextDirection),
                };
                if (properties?.TableCellWidth is { } cellWidth) cellWidths.Add((column, span, cellWidth));
                if (properties?.TableCellMargin is { HasChildren: true } cellMargins) margins.Add((cell, cellMargins));
                if (properties?.TableCellBorders is { HasChildren: true } edges) own.Add((cell, edges));
                cells.Add(cell);
                reaching.Add((cell, column));
                for (var index = 1; index < span; index++)
                {
                    cells.Add(new TableCell(new BlockContainer()) { Covered = true });
                    reaching.Add((cell, column));
                }
            }

            if ((long)(rows.Count + 1) * Math.Max(1, Math.Max(widest, cells.Count)) > PulseOffice.Formats.ImportedTableLimits.TextTableCells)
            {
                context.Losses.Add(TruncatedTable);
                break;
            }
            widest = Math.Max(widest, cells.Count);
            var rowProperties = rowElement.TableRowProperties;
            var height = rowProperties?.GetFirstChild<W.TableRowHeight>();
            var row = new TableRow(cells)
            {
                Height = height?.Val?.Value is { } twips ? new Twip(values.Bounded(twips, 0, LargestLength)) : null,
                MinimumHeight = height?.HeightType?.Value != W.HeightRuleValues.Exact,
                AllowBreakAcrossPages = !values.Present(rowProperties?.GetFirstChild<W.CantSplit>()),
                ChangeId = rowChanges.Read(rowProperties, context),
            };
            if (countingHeader && values.Present(rowProperties?.GetFirstChild<W.TableHeader>())) headerRows++;
            else countingHeader = false;
            rows.Add(row);
            open = reaching;
        }

        var width = Math.Max(columns.Count, rows.Count == 0 ? 0 : rows.Max(row => row.Cells.Count));
        var granted = context.TableCells.Rows(rows.Count, Math.Max(1, width));
        if (granted < rows.Count)
        {
            context.Losses.Add(TruncatedTable);
            rows.RemoveRange(granted, rows.Count - granted);
            for (var r = 0; r < rows.Count; r++)
                foreach (var cell in rows[r].Cells) cell.RowSpan = Math.Min(cell.RowSpan, rows.Count - r);
            headerRows = Math.Min(headerRows, granted);
        }
        Grid(columns, width, cellWidths, element.GetFirstChild<W.TableProperties>()?.TableWidth);
        foreach (var row in rows)
            while (row.Cells.Count < width) row.Cells.Add(new TableCell(new BlockContainer([new ParagraphBlock(context.Document.NextId("p"), string.Empty, DefaultStyleNames.Standard)])));
        foreach (var row in rows)
            foreach (var cell in row.Cells.Where(cell => !cell.Covered && cell.Content.Count == 0))
                cell.Content.Add(new ParagraphBlock(context.Document.NextId("p"), string.Empty, DefaultStyleNames.Standard));

        var tableProperties = element.GetFirstChild<W.TableProperties>();
        var table = new TableBlock(context.Document.NextId("t"), rows, columns.Take(width))
        {
            StyleName = context.Styles.NameOf(tableProperties?.TableStyle?.Val?.Value ?? tableParagraphs.DefaultTableStyle(context)),
            Name = tableProperties?.TableCaption?.Val?.Value,
            HeaderRows = headerRows,
            Banding = _looks.Read(tableProperties?.GetFirstChild<W.TableLook>()),
            Direct = tableStyles.Padding(tableProperties?.TableCellMarginDefault),
            Alignment = Alignment(tableProperties?.TableJustification?.Val?.Value),
        };
        PreferredWidth(table, tableProperties?.TableWidth);
        if (table.Alignment == TableAlignment.Left && tableProperties?.TableIndentation is { } indentation && indentation.Type?.Value != W.TableWidthUnitValues.Pct)
            table.Indent = new Twip(values.Bounded(indentation.Width?.Value ?? 0, -LargestLength, LargestLength));
        var styled = appearance.Style(context.Document.Styles, table);
        if (headerRows == 0 && rows.Count > 1 && LooksHeaded(tableProperties?.GetFirstChild<W.TableLook>())
            && (styled.HeaderBackground is not null || styled.HeaderColor is not null || styled.HeaderBold == true))
        {
            table.HeaderRows = 1;
            table.RepeatHeaderRows = false;
        }
        table.Direct = table.Direct with
        {
            CellPadding = styled.CellPadding is null ? 5.4 : table.Direct.CellPadding,
            VerticalCellPadding = styled.VerticalCellPadding is null ? 0 : table.Direct.VerticalCellPadding,
        };
        Edges(table, own, tableStyles.Borders(tableProperties?.GetFirstChild<W.TableBorders>()), context);
        Closings(table, closings, context);
        foreach (var placeholder in skipped) placeholder.Borders = new PulseOffice.Text.Rich.BoxBorders();
        if (margins.Count > 0)
        {
            var padded = appearance.Style(context.Document.Styles, table);
            var side = padded.CellPadding ?? 5.4;
            foreach (var (cell, margin) in margins) cell.Padding = geometry.Padding(margin, side, padded.VerticalCellPadding ?? side);
        }
        return table;
    }

    private void Grid(List<Twip> columns, int width, List<(int Column, int Span, W.TableCellWidth Width)> cellWidths, W.TableWidth? tableWidth)
    {
        while (columns.Count < width) columns.Add(new Twip(0));
        if (tableWidth?.Type?.Value == W.TableWidthUnitValues.Auto) PreferredCells(columns, cellWidths);
        if (columns.All(column => column.Value > 0)) return;
        var total = GridTotal(columns, tableWidth);
        int Dxa(W.TableCellWidth cell) =>
            cell.Type?.Value == W.TableWidthUnitValues.Dxa ? values.Integer(cell.Width?.Value) ?? 0
            : cell.Type?.Value == W.TableWidthUnitValues.Pct && total > 0 && Percent(cell.Width?.Value) is { } percent ? (int)(percent * total / 100)
            : 0;
        foreach (var (column, span, cell) in cellWidths.Where(entry => entry.Column < columns.Count && columns[entry.Column].Value == 0).OrderBy(entry => entry.Span))
        {
            var cells = Enumerable.Range(column, Math.Min(span, columns.Count - column)).ToList();
            var unknown = cells.Where(index => columns[index].Value == 0).ToList();
            var rest = Dxa(cell) - cells.Sum(index => columns[index].Value);
            if (unknown.Count == 0 || rest <= 0) continue;
            foreach (var index in unknown) columns[index] = new Twip(values.Bounded(rest / unknown.Count, 1, LargestLength));
        }
        var missing = columns.Count(column => column.Value == 0);
        if (missing == 0) return;
        var left = (tableWidth?.Type?.Value == W.TableWidthUnitValues.Dxa || tableWidth?.Type?.Value == W.TableWidthUnitValues.Pct ? total : 0) - columns.Sum(column => column.Value);
        var share = left > 0 ? values.Bounded(left / missing, 1, LargestLength) : Twip.FromCentimeters(2).Value;
        for (var index = 0; index < columns.Count; index++)
            if (columns[index].Value == 0) columns[index] = new Twip(share);
    }

    private const int NominalTextWidth = 9360;

    private int GridTotal(List<Twip> columns, W.TableWidth? tableWidth)
    {
        if (tableWidth?.Type?.Value == W.TableWidthUnitValues.Dxa) return values.Integer(tableWidth.Width?.Value) ?? 0;
        if (tableWidth?.Type?.Value == W.TableWidthUnitValues.Pct) return Percent(tableWidth.Width?.Value) is { } percent and > 0 ? (int)(NominalTextWidth * Math.Clamp(percent, 1, 1000) / 100) : NominalTextWidth;
        var known = columns.Sum(column => column.Value);
        return known > 0 ? known : NominalTextWidth;
    }

    private void PreferredCells(List<Twip> columns, List<(int Column, int Span, W.TableCellWidth Width)> cellWidths)
    {
        var grid = GridTotal(columns, null);
        foreach (var (column, _, cell) in cellWidths.Where(entry => entry.Span == 1 && entry.Column < columns.Count))
        {
            var preferred = cell.Type?.Value == W.TableWidthUnitValues.Dxa ? values.Integer(cell.Width?.Value)
                : cell.Type?.Value == W.TableWidthUnitValues.Pct && Percent(cell.Width?.Value) is { } percent ? (int)(percent * grid / 100)
                : null;
            if (columns[column].Value > 0 && preferred is { } wanted && wanted > columns[column].Value)
                columns[column] = new Twip(values.Bounded(wanted, 1, LargestLength));
        }
    }

    private void PreferredWidth(TableBlock table, W.TableWidth? width)
    {
        if (width is null) return;
        if (width.Type?.Value == W.TableWidthUnitValues.Auto) table.AutoWidth = true;
        else if (width.Type?.Value == W.TableWidthUnitValues.Pct && Percent(width.Width?.Value) is { } percent and > 0) table.WidthPercent = Math.Clamp(percent, 1, 1000);
    }

    private double? Percent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.EndsWith('%'))
            return double.TryParse(text.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var stated) ? stated : null;
        return values.Integer(text) is { } fiftieths ? fiftieths / 50.0 : null;
    }

    public const string TruncatedTable = "DOCX222";

    private readonly DocxTableLooks _looks = new();

    private bool LooksHeaded(W.TableLook? look) => _looks.Headed(look);

    private TableAlignment Alignment(W.TableRowAlignmentValues? value) =>
        value == W.TableRowAlignmentValues.Center ? TableAlignment.Center
        : value == W.TableRowAlignmentValues.Right ? TableAlignment.Right
        : TableAlignment.Left;

    private void Closings(TableBlock table, List<(TableCell Origin, W.TableCellBorders Element)> closings, DocxReadContext context)
    {
        if (closings.Count == 0) return;
        var style = appearance.Style(context.Document.Styles, table);
        var last = new Dictionary<TableCell, W.TableCellBorders>(ReferenceEqualityComparer.Instance);
        foreach (var (origin, element) in closings) last[origin] = element;
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                var cell = table.Rows[r].Cells[c];
                if (!last.TryGetValue(cell, out var element)) continue;
                cell.Borders = (cell.Borders ?? appearance.StyleBorders(table, style, r, c)) with { Bottom = borders.Read(element).Bottom };
            }
        }
    }

    private void Edges(TableBlock table, List<(TableCell Cell, W.TableCellBorders Element)> own, TableProperties direct, DocxReadContext context)
    {
        var directLines = direct.BorderWidth is not null;
        if (own.Count == 0 && !directLines) return;
        var style = direct.Over(appearance.Style(context.Document.Styles, table));
        var first = new Dictionary<TableCell, W.TableCellBorders>(ReferenceEqualityComparer.Instance);
        foreach (var (owner, edges) in own) first.TryAdd(owner, edges);
        var explicitSides = new Dictionary<(TableCell, BorderSide), bool>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                var cell = table.Rows[r].Cells[c];
                if (cell.Covered) continue;
                var inherited = appearance.StyleBorders(table, style, r, c);
                if (!first.TryGetValue(cell, out var element))
                {
                    if (directLines) cell.Borders = inherited;
                    continue;
                }
                var read = borders.Read(element);
                explicitSides.Add((cell, BorderSide.Left), element.GetFirstChild<W.LeftBorder>() is not null || element.GetFirstChild<W.StartBorder>() is not null);
                explicitSides.Add((cell, BorderSide.Right), element.GetFirstChild<W.RightBorder>() is not null || element.GetFirstChild<W.EndBorder>() is not null);
                explicitSides.Add((cell, BorderSide.Top), element.GetFirstChild<W.TopBorder>() is not null);
                explicitSides.Add((cell, BorderSide.Bottom), element.GetFirstChild<W.BottomBorder>() is not null);
                cell.Borders = new PulseOffice.Text.Rich.BoxBorders
                {
                    Top = element.GetFirstChild<W.TopBorder>() is null ? inherited.Top : read.Top,
                    Bottom = element.GetFirstChild<W.BottomBorder>() is null ? inherited.Bottom : read.Bottom,
                    Left = element.GetFirstChild<W.LeftBorder>() is null && element.GetFirstChild<W.StartBorder>() is null ? inherited.Left : read.Left,
                    Right = element.GetFirstChild<W.RightBorder>() is null && element.GetFirstChild<W.EndBorder>() is null ? inherited.Right : read.Right,
                };
            }
        }
        cellEdges.SuppressInherited(table, (cell, side) => explicitSides.TryGetValue((cell, side), out var set) && set);
    }

    private TableCell? Continued(List<(TableCell Origin, int Column)?> open, int column, int span) =>
        column < open.Count && open[column] is { } above && above.Column == column && above.Origin.ColumnSpan == span ? above.Origin : null;
}
