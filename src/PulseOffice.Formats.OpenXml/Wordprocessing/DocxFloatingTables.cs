using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxFloatingTables
{
    private readonly Twip _minimumHeight = Twip.FromCentimeters(0.5);

    private readonly (HorizontalRelation Relation, W.HorizontalAnchorValues Word)[] _horizontal =
    [
        (HorizontalRelation.Paragraph, W.HorizontalAnchorValues.Text),
        (HorizontalRelation.PageArea, W.HorizontalAnchorValues.Margin),
        (HorizontalRelation.Page, W.HorizontalAnchorValues.Page),
    ];

    private readonly (VerticalRelation Relation, W.VerticalAnchorValues Word)[] _vertical =
    [
        (VerticalRelation.Paragraph, W.VerticalAnchorValues.Text),
        (VerticalRelation.PageArea, W.VerticalAnchorValues.Margin),
        (VerticalRelation.Page, W.VerticalAnchorValues.Page),
    ];

    private readonly double _largestFloatingHeightPoints = 560;
    private readonly double _estimatedLinePoints = 14;

    public bool FitsOnOnePage(TableBlock table) =>
        table.Rows.Sum(row => Math.Max(row.Height?.ToPoints() ?? 0, row.Cells.Select(cell => cell.Content.Blocks.Sum(Lines)).DefaultIfEmpty(1).Max() * _estimatedLinePoints))
        <= _largestFloatingHeightPoints;

    private int Lines(Block block) => block switch
    {
        ParagraphBlock paragraph => Math.Max(1, paragraph.Length / 60 + 1),
        TableBlock nested => nested.Rows.Sum(row => row.Cells.Select(cell => cell.Content.Blocks.Sum(Lines)).DefaultIfEmpty(1).Max()),
        _ => 1,
    };

    public W.TablePositionProperties? Position(W.Table element) => element.GetFirstChild<W.TableProperties>()?.TablePositionProperties;

    public AnchoredObject Read(TableBlock table, W.TablePositionProperties position, string objectId, string anchorParagraphId)
    {
        var width = new Twip(table.Columns.Sum(column => column.Value));
        var height = new Twip(Math.Max(_minimumHeight.Value, table.Rows.Sum(row => row.Height?.Value ?? 0)));
        return new AnchoredObject(objectId, new ParagraphAnchor(anchorParagraphId), new TextBoxContent(new BlockContainer([table])), new DocumentSize(width, height))
        {
            Placement = new ObjectPlacement(new Twip(position.TablePositionX?.Value ?? 0), new Twip(position.TablePositionY?.Value ?? 0))
            {
                Horizontal = _horizontal.FirstOrDefault(pair => pair.Word == position.HorizontalAnchor?.Value, (HorizontalRelation.PageArea, default)).Relation,
                Vertical = _vertical.FirstOrDefault(pair => pair.Word == position.VerticalAnchor?.Value, (VerticalRelation.PageArea, default)).Relation,
            },
            Wrap = WrapMode.Parallel,
            WrapSpacing = new Margins(
                new Twip(Math.Max(0, (int)(position.LeftFromText?.Value ?? 0))), new Twip(Math.Max(0, (int)(position.TopFromText?.Value ?? 0))),
                new Twip(Math.Max(0, (int)(position.RightFromText?.Value ?? 0))), new Twip(Math.Max(0, (int)(position.BottomFromText?.Value ?? 0)))),
        };
    }

    public bool IsFloatingTable(AnchoredObject item) =>
        item is { Anchor: ParagraphAnchor, Wrap: WrapMode.Parallel, RotationDegrees: 0, StyleName: null, Content: TextBoxContent { AutoGrowHeight: true, Content.Blocks: [TableBlock] } }
        && _horizontal.Any(pair => pair.Relation == item.Placement.Horizontal)
        && _vertical.Any(pair => pair.Relation == item.Placement.Vertical);

    public TableBlock Table(AnchoredObject item) => (TableBlock)((TextBoxContent)item.Content).Content.Blocks[0];

    public W.TablePositionProperties Write(AnchoredObject item) => new()
    {
        LeftFromText = (short)Math.Clamp(item.WrapSpacing.Left.Value, 0, short.MaxValue),
        RightFromText = (short)Math.Clamp(item.WrapSpacing.Right.Value, 0, short.MaxValue),
        TopFromText = (short)Math.Clamp(item.WrapSpacing.Top.Value, 0, short.MaxValue),
        BottomFromText = (short)Math.Clamp(item.WrapSpacing.Bottom.Value, 0, short.MaxValue),
        HorizontalAnchor = _horizontal.First(pair => pair.Relation == item.Placement.Horizontal).Word,
        VerticalAnchor = _vertical.First(pair => pair.Relation == item.Placement.Vertical).Word,
        TablePositionX = item.Placement.X.Value,
        TablePositionY = item.Placement.Y.Value,
    };
}
