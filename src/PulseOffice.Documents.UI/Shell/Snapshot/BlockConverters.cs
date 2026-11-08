using System.Text.Json;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public sealed class BlockContainerConverter : SnapshotConverter<BlockContainer>
{
    protected override BlockContainer Read(SnapshotReader node) => new(node.Get<List<Block>>("blocks", []));

    protected override void Write(BlockContainer value, SnapshotWriter node) => node.Put("blocks", value.Blocks);
}

public sealed class BlockConverter : SnapshotConverter<Block>
{
    private const string Paragraph = "paragraph";
    private const string Table = "table";
    private const string Section = "section";

    protected override Block Read(SnapshotReader node)
    {
        var id = node.Require<string>("id");
        return node.Require<string>("block") switch
        {
            Paragraph => new ParagraphBlock(id, node.Require<TextParagraph>("content")) { Marks = node.Get<List<InlineMark>>("marks", []) },
            Table => new TableBlock(id, node.Get<List<TableRow>>("rows", []), node.Get<List<Twip>>("columns", []))
            {
                Name = node.Get<string>("name"),
                StyleName = node.Get<string>("style"),
                HeaderRows = node.Get("headerRows", 0),
                RepeatHeaderRows = node.Get("repeatHeaderRows", true),
                Alignment = node.Get("alignment", TableAlignment.Left),
                Indent = node.Get("indent", Twip.Zero),
                Direct = node.Get("direct", PulseOffice.Documents.Styles.TableProperties.None),
                PageStyleName = node.Get<string>("pageStyle"),
                PageBreakBefore = node.Get("pageBreakBefore", false),
            },
            Section => new SectionBlock(id, node.Get("name", string.Empty), node.Get("content", new BlockContainer()))
            {
                Columns = node.Get("columns", SectionColumns.Single),
                Protected = node.Get("protected", false),
                ProtectionKey = node.Get<byte[]>("protectionKey"),
                Hidden = node.Get("hidden", false),
                Condition = node.Get<string>("condition"),
                LinkedFile = node.Get<string>("linkedFile"),
                Index = node.Get<PulseOffice.Documents.Indexes.IndexDefinition>("index"),
            },
            var other => throw new JsonException($"Unknown block kind '{other}'."),
        };
    }

    protected override void Write(Block value, SnapshotWriter node) => value.Accept(new BlockWriting(node.Put("id", value.Id)));

    private sealed class BlockWriting(SnapshotWriter node) : IBlockVisitor<SnapshotWriter>
    {
        public SnapshotWriter VisitParagraph(ParagraphBlock paragraph) =>
            node.Put("block", Paragraph).Put("content", paragraph.Content).Put("marks", paragraph.Marks);

        public SnapshotWriter VisitTable(TableBlock table) =>
            node.Put("block", Table).Put("rows", table.Rows).Put("columns", table.Columns).Put("name", table.Name)
                .Put("style", table.StyleName).Put("headerRows", table.HeaderRows).Put("repeatHeaderRows", table.RepeatHeaderRows)
                .Put("alignment", table.Alignment).Put("indent", table.Indent).Put("direct", table.Direct).Put("pageStyle", table.PageStyleName)
                .Put("pageBreakBefore", table.PageBreakBefore);

        public SnapshotWriter VisitSection(SectionBlock section) =>
            node.Put("block", Section).Put("name", section.Name).Put("content", section.Content).Put("columns", section.Columns)
                .Put("protected", section.Protected).Put("protectionKey", section.ProtectionKey).Put("hidden", section.Hidden).Put("condition", section.Condition).Put("linkedFile", section.LinkedFile).Put("index", section.Index);
    }
}

public sealed class TableRowConverter : SnapshotConverter<TableRow>
{
    protected override TableRow Read(SnapshotReader node) => new(node.Get<List<TableCell>>("cells", []))
    {
        Height = node.Get<Twip?>("height"),
        MinimumHeight = node.Get("minimumHeight", true),
        AllowBreakAcrossPages = node.Get("allowBreak", true),
        ChangeId = node.Get<string?>("change"),
    };

    protected override void Write(TableRow value, SnapshotWriter node) =>
        node.Put("cells", value.Cells).Put("height", value.Height).Put("minimumHeight", value.MinimumHeight).Put("allowBreak", value.AllowBreakAcrossPages).Put("change", value.ChangeId);
}

public sealed class TableCellConverter : SnapshotConverter<TableCell>
{
    protected override TableCell Read(SnapshotReader node) => new(node.Get("content", new BlockContainer()))
    {
        ColumnSpan = node.Get("columnSpan", 1),
        RowSpan = node.Get("rowSpan", 1),
        Covered = node.Get("covered", false),
        StyleName = node.Get<string>("style"),
        Background = node.Get<uint?>("background"),
        VerticalAlign = node.Get("verticalAlign", CellVerticalAlign.Top),
        Borders = node.Get<PulseOffice.Text.Rich.BoxBorders>("borders"),
        Formula = node.Get<string>("formula"),
        Protected = node.Get("protected", false),
        NumberFormat = node.Get<CellNumberFormat>("numberFormat"),
        TextDirection = node.Get("textDirection", CellTextDirection.Horizontal),
        Padding = node.Get<CellPadding>("padding"),
    };

    protected override void Write(TableCell value, SnapshotWriter node) =>
        node.Put("content", value.Content).Put("columnSpan", value.ColumnSpan).Put("rowSpan", value.RowSpan).Put("covered", value.Covered)
            .Put("style", value.StyleName).Put("background", value.Background).Put("verticalAlign", value.VerticalAlign)
            .Put("borders", value.Borders).Put("formula", value.Formula).Put("protected", value.Protected).Put("numberFormat", value.NumberFormat)
            .Put("textDirection", value.TextDirection).Put("padding", value.Padding);
}
