using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxSectionCodec(DocxBlockWriter writer, DocxBlockReader reader, DocxValues values, DocxBorders borders, DocxWatermarks watermarks, DocxHeaderFooterExtent extents, DocxSectionStarts starts, IUnusedHeaderVariants hiddenHeaders)
{
    public DocxSectionStarts Starts { get; } = starts;

    private const int SmallestPage = 144;
    private const int LargestPage = 158_400;
    private const int LargestColumnCount = SectionColumns.LargestCount;
    private const int LargestStart = 32_767;

    private readonly IReadOnlyDictionary<NumberFormat, W.NumberFormatValues> _formats = new Dictionary<NumberFormat, W.NumberFormatValues>
    {
        [NumberFormat.Arabic] = W.NumberFormatValues.Decimal,
        [NumberFormat.LowerLetter] = W.NumberFormatValues.LowerLetter,
        [NumberFormat.UpperLetter] = W.NumberFormatValues.UpperLetter,
        [NumberFormat.LowerRoman] = W.NumberFormatValues.LowerRoman,
        [NumberFormat.UpperRoman] = W.NumberFormatValues.UpperRoman,
        [NumberFormat.None] = W.NumberFormatValues.None,
    };

    public W.SectionProperties Write(PageStyle style, int? restart, bool continuous, MainDocumentPart main, DocxWriteContext context) =>
        Write(style, restart, continuous, !continuous, main, context);

    public W.SectionProperties Write(PageStyle style, int? restart, bool continuous, bool headers, MainDocumentPart main, DocxWriteContext context)
    {
        var section = new W.SectionProperties();
        context.WrittenPageStyles.Add(style.Name);
        if (headers)
        {
            References(style.Header, isHeader: true, style, main, context, section);
            References(style.Footer, isHeader: false, style, main, context, section);
        }
        var inset = style.Borders ?? new PulseOffice.Text.Rich.BoxBorders();

        if (continuous) section.Append(new W.SectionType { Val = W.SectionMarkValues.Continuous });
        else if (Starts.Mark(style, context.Document) is { } mark) section.Append(new W.SectionType { Val = mark });
        section.Append(new W.PageSize
        {
            Width = (uint)style.Width.Value,
            Height = (uint)style.Height.Value,
            Orient = style.Orientation == PageOrientation.Landscape ? W.PageOrientationValues.Landscape : null,
        });
        var headerExtent = extents.Of(context.Document, style.Header);
        var footerExtent = extents.Of(context.Document, style.Footer);
        section.Append(new W.PageMargin
        {
            Top = style.Margins.Top.Value + headerExtent + Twips(inset, PulseOffice.Text.Rich.BorderSide.Top),
            Bottom = style.Margins.Bottom.Value + footerExtent + Twips(inset, PulseOffice.Text.Rich.BorderSide.Bottom),
            Left = (uint)(style.Margins.Left.Value + Twips(inset, PulseOffice.Text.Rich.BorderSide.Left)),
            Right = (uint)(style.Margins.Right.Value + Twips(inset, PulseOffice.Text.Rich.BorderSide.Right)),
            Header = (uint)style.Margins.Top.Value,
            Footer = (uint)style.Margins.Bottom.Value,
            Gutter = 0,
        });
        if (style.Borders is { HasLines: true } box)
            section.Append(borders.Write(new W.PageBorders { OffsetFrom = W.PageBorderOffsetValues.Text }, box));
        if (context.Document.LineNumbering is { } lines)
            section.Append(new W.LineNumberType
            {
                CountBy = (short)Math.Clamp(lines.Increment, 1, 100),
                Distance = values.Twips(lines.Distance),
                Restart = lines.RestartEachPage ? W.LineNumberRestartValues.NewPage : W.LineNumberRestartValues.Continuous,
            });
        var numbering = new W.PageNumberType();
        if (style.NumberFormat != NumberFormat.Arabic && _formats.TryGetValue(style.NumberFormat, out var format)) numbering.Format = format;
        if (restart is { } start) numbering.Start = start;
        if (numbering.HasAttributes) section.Append(numbering);
        var columns = new W.Columns { Space = values.Text(style.Columns.Gap.Value) };
        if (style.Columns.Count > 1)
        {
            columns.ColumnCount = (short)style.Columns.Count;
            columns.Separator = style.Columns.Separator;
        }

        section.Append(columns);
        if (style.FirstPageDifferent) section.Append(new W.TitlePage());
        return section;
    }

    public PageStyle Read(string name, W.SectionProperties section, MainDocumentPart main, DocxReadContext context, out int? restart)
    {
        var style = new PageStyle(name);
        var size = section.GetFirstChild<W.PageSize>();
        var width = new Twip(values.Bounded(size?.Width?.Value ?? 11906, SmallestPage, LargestPage));
        var height = new Twip(values.Bounded(size?.Height?.Value ?? 16838, SmallestPage, LargestPage));
        var landscape = size?.Orient?.Value == W.PageOrientationValues.Landscape;
        style.Orientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        style.Size = landscape ? new DocumentSize(height, width) : new DocumentSize(width, height);
        style.FirstPageDifferent = section.GetFirstChild<W.TitlePage>() is { } title && values.On(title);
        if (section.GetFirstChild<W.VerticalTextAlignmentOnPage>()?.Val?.Value is { } alignment && alignment != W.VerticalJustificationValues.Top) context.Losses.Add("DOCX152");
        var headers = section.Elements<W.HeaderReference>().ToList();
        var footers = section.Elements<W.FooterReference>().ToList();
        context.Watermark = null;
        Content(style.Header, headers.Select(item => (item.Type?.Value, item.Id?.Value)), main, context, isHeader: true, style.FirstPageDifferent);
        style.Watermark = context.Watermark;
        context.Watermark = null;
        Content(style.Footer, footers.Select(item => (item.Type?.Value, item.Id?.Value)), main, context, isHeader: false, style.FirstPageDifferent);
        var margin = section.GetFirstChild<W.PageMargin>();
        var pageBorders = section.GetFirstChild<W.PageBorders>();
        var box = pageBorders is null ? null : borders.Read(pageBorders);
        if (box is { HasLines: true }) style.Borders = box;
        var fromPage = pageBorders?.OffsetFrom?.Value == W.PageBorderOffsetValues.Page;
        var top = values.Bounded(Math.Abs((long)(margin?.Top?.Value ?? 1440)), 0, LargestPage) - (fromPage ? 0 : Twips(box, PulseOffice.Text.Rich.BorderSide.Top));
        var bottom = values.Bounded(Math.Abs((long)(margin?.Bottom?.Value ?? 1440)), 0, LargestPage) - (fromPage ? 0 : Twips(box, PulseOffice.Text.Rich.BorderSide.Bottom));
        var headerDistance = values.Bounded(margin?.Header?.Value ?? 720, 0, LargestPage);
        var footerDistance = values.Bounded(margin?.Footer?.Value ?? 720, 0, LargestPage);
        if (style.Header.Enabled) ReachBody(style.Header, Math.Abs(top) - headerDistance);
        if (style.Footer.Enabled) ReachBody(style.Footer, Math.Abs(bottom) - footerDistance);
        style.Margins = new Margins(
            new Twip(values.Bounded(margin?.Left?.Value ?? 1440, 0, LargestPage) - (fromPage ? 0 : Twips(box, PulseOffice.Text.Rich.BorderSide.Left))),
            new Twip(style.Header.Enabled ? headerDistance : Math.Abs(top)),
            new Twip(values.Bounded(margin?.Right?.Value ?? 1440, 0, LargestPage) - (fromPage ? 0 : Twips(box, PulseOffice.Text.Rich.BorderSide.Right))),
            new Twip(style.Footer.Enabled ? footerDistance : Math.Abs(bottom)));
        style.Columns = Columns(section);
        if (section.GetFirstChild<W.LineNumberType>() is { CountBy: not null } lineNumbers)
            context.Document.LineNumbering = new LineNumbering(Math.Max(1, (int)lineNumbers.CountBy.Value))
            {
                Distance = values.Points(lineNumbers.Distance?.Value) ?? 0.5 * 72 / 2.54,
                RestartEachPage = lineNumbers.Restart?.Value == W.LineNumberRestartValues.NewPage,
            };
        var numbering = section.GetFirstChild<W.PageNumberType>();
        if (numbering?.Format?.Value is { } format)
            style.NumberFormat = _formats.FirstOrDefault(pair => pair.Value == format, new(NumberFormat.Arabic, W.NumberFormatValues.Decimal)).Key;
        restart = numbering?.Start?.Value is { } start ? values.Bounded(start, 0, LargestStart) : null;
        return style;
    }

    public SectionColumns Columns(W.SectionProperties section)
    {
        var columns = section.GetFirstChild<W.Columns>();
        var count = values.Bounded(columns?.ColumnCount?.Value ?? 1, 1, LargestColumnCount);
        return count > 1
            ? new SectionColumns(count, new Twip(values.Bounded(values.Integer(columns!.Space?.Value) ?? 720, 0, LargestPage))) { Separator = columns.Separator?.Value ?? false }
            : SectionColumns.Single;
    }

    public bool Continuous(W.SectionProperties section) =>
        section.GetFirstChild<W.SectionType>()?.Val?.Value == W.SectionMarkValues.Continuous;

    private int Twips(PulseOffice.Text.Rich.BoxBorders? borders, PulseOffice.Text.Rich.BorderSide side) =>
        borders is null ? 0 : Twip.FromPoints(borders.Inset(side)).Value;

    private void ReachBody(HeaderFooterContent content, int bodyOffset)
    {
        content.Spacing = Twip.Zero;
        content.MinimumHeight = new Twip(Math.Max(0, bodyOffset));
    }

    private void References(HeaderFooterContent content, bool isHeader, PageStyle style, MainDocumentPart main, DocxWriteContext context, W.SectionProperties section)
    {
        if (isHeader && style.Watermark is { } mark && !content.Enabled)
        {
            var only = main.AddNewPart<HeaderPart>();
            only.Header = new W.Header(watermarks.Paragraph(mark, style, ++context.Watermarks));
            section.Append(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(only) });
        }
        foreach (var (type, blocks) in Variants(content, style, context.Document))
        {
            var previous = context.Part;
            if (isHeader)
            {
                var part = main.AddNewPart<HeaderPart>();
                context.Part = part;
                part.Header = new W.Header(writer.Write(blocks, context));
                if (style.Watermark is { } watermark) part.Header.PrependChild(watermarks.Paragraph(watermark, style, ++context.Watermarks));
                section.Append(new W.HeaderReference { Type = type, Id = main.GetIdOfPart(part) });
            }
            else
            {
                var part = main.AddNewPart<FooterPart>();
                context.Part = part;
                part.Footer = new W.Footer(writer.Write(blocks, context));
                section.Append(new W.FooterReference { Type = type, Id = main.GetIdOfPart(part) });
            }

            context.Part = previous;
        }
    }

    private List<(W.HeaderFooterValues Type, BlockContainer Blocks)> Variants(HeaderFooterContent content, PageStyle style, PulseOffice.Documents.TextDocument document)
    {
        var variants = new List<(W.HeaderFooterValues Type, BlockContainer Blocks)>();
        if (content.Enabled) variants.Add((W.HeaderFooterValues.Default, content.Default));
        if (style.FirstPageDifferent)
        {
            if (content.Enabled) variants.Add((W.HeaderFooterValues.First, content.First ?? content.Default));
        }
        else if (content.First is { } hiddenFirst) variants.Add((W.HeaderFooterValues.First, hiddenFirst));
        if (style.EvenPagesDifferent)
        {
            if (content.Enabled && content.Even is { } even) variants.Add((W.HeaderFooterValues.Even, even));
        }
        else if (content.Even is { } hiddenEven && !hiddenHeaders.EvenPagesShownInDocument(document)) variants.Add((W.HeaderFooterValues.Even, hiddenEven));
        return variants;
    }

    private void Content(HeaderFooterContent content, IEnumerable<(W.HeaderFooterValues? Type, string? Id)> references, MainDocumentPart main, DocxReadContext context, bool isHeader, bool first)
    {
        foreach (var (type, id) in references)
        {
            if (id is null || main.TryGetPartById(id, out var part) is false || part is null) continue;
            OpenXmlElement? root = isHeader ? (part as HeaderPart)?.Header : (part as FooterPart)?.Footer;
            if (root is null) continue;
            if (isHeader)
            {
                root = (OpenXmlElement)root.CloneNode(true);
                if (watermarks.Extract(root) is { } watermark) context.Watermark ??= watermark;
                if (!root.ChildElements.Any(child => child is W.Paragraph or W.Table or W.SdtBlock)) continue;
            }
            var previous = context.Part;
            context.Part = part;
            var blocks = reader.Read(root.ChildElements, context);
            context.Part = previous;
            if (type != W.HeaderFooterValues.Even && (type != W.HeaderFooterValues.First || first)) content.Enabled = true;
            if (type == W.HeaderFooterValues.First) content.First = new BlockContainer(blocks);
            else if (type == W.HeaderFooterValues.Even) content.Even = new BlockContainer(blocks);
            else content.Default.Replace(blocks);
        }
        if (first && content.Enabled && content.First is null)
            content.First = new BlockContainer([new ParagraphBlock(context.Document.NextId("p"), string.Empty, isHeader ? DefaultStyleNames.Header : DefaultStyleNames.Footer)]);
    }
}
