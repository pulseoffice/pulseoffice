using System.Text.Json;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public sealed class StyleSheetConverter : SnapshotConverter<StyleSheet>
{
    protected override StyleSheet Read(SnapshotReader node)
    {
        var sheet = new StyleSheet();
        if (node.Get<CharacterProperties>("defaultCharacter") is { } character) sheet.DefaultCharacter = character;
        if (node.Get<ParagraphProperties>("defaultParagraph") is { } paragraph) sheet.DefaultParagraph = paragraph;
        sheet.Paragraph.ReplaceAll(node.Get<List<DocumentStyle>>("paragraph", []).OfType<ParagraphStyle>());
        sheet.Character.ReplaceAll(node.Get<List<DocumentStyle>>("character", []).OfType<CharacterStyle>());
        sheet.List.ReplaceAll(node.Get<List<DocumentStyle>>("list", []).OfType<ListStyle>());
        sheet.Page.ReplaceAll(node.Get<List<DocumentStyle>>("page", []).OfType<PageStyle>());
        sheet.Frame.ReplaceAll(node.Get<List<DocumentStyle>>("frame", []).OfType<FrameStyle>());
        sheet.Table.ReplaceAll(node.Get<List<DocumentStyle>>("table", []).OfType<TableStyle>());
        if (node.Get<DocumentStyle>("outline") is ListStyle outline) sheet.Outline = outline;
        sheet.DefaultTabInterval = node.Get("defaultTabInterval", StyleSheet.StandardTabInterval);
        sheet.TabsRelativeToIndent = node.Get("tabsRelativeToIndent", true);
        return sheet;
    }

    protected override void Write(StyleSheet value, SnapshotWriter node) =>
        node.Put("defaultCharacter", value.DefaultCharacter).Put("defaultParagraph", value.DefaultParagraph)
            .Put("paragraph", Styles(value.Paragraph.All)).Put("character", Styles(value.Character.All)).Put("list", Styles(value.List.All))
            .Put("page", Styles(value.Page.All)).Put("frame", Styles(value.Frame.All)).Put("table", Styles(value.Table.All))
            .Put("outline", (DocumentStyle)value.Outline).Put("defaultTabInterval", value.DefaultTabInterval).Put("tabsRelativeToIndent", value.TabsRelativeToIndent);

    private IReadOnlyList<DocumentStyle> Styles<T>(IReadOnlyList<T> styles) where T : DocumentStyle => [.. styles];
}

public sealed class DocumentStyleConverter : SnapshotConverter<DocumentStyle>
{
    protected override DocumentStyle Read(SnapshotReader node)
    {
        var name = node.Require<string>("name");
        DocumentStyle style = node.Require<StyleFamily>("family") switch
        {
            StyleFamily.Paragraph => new ParagraphStyle(name)
            {
                Paragraph = node.Get("paragraph", ParagraphProperties.None),
                Character = node.Get("character", CharacterProperties.None),
                NextStyleName = node.Get<string>("next"),
                OutlineLevel = node.Get<int?>("outlineLevel"),
                ListStyleName = node.Get<string>("listStyle"),
                ListLevel = node.Get<int?>("listLevel"),
                Category = node.Get("category", ParagraphStyleCategory.Text),
            },
            StyleFamily.Character => new CharacterStyle(name) { Character = node.Get("character", CharacterProperties.None) },
            StyleFamily.List => new ListStyle(name, node.Get<List<ListLevel>>("levels", [])),
            StyleFamily.Page => Page(name, node),
            StyleFamily.Frame => new FrameStyle(name) { Frame = node.Get("frame", FrameProperties.None) },
            StyleFamily.Table => new TableStyle(name) { Table = node.Get("table", TableProperties.None) },
            var other => throw new JsonException($"Unknown style family '{other}'."),
        };
        style.ParentName = node.Get<string>("parent");
        style.DisplayName = node.Get<string>("display");
        style.Hidden = node.Get("hidden", false);
        return style;
    }

    protected override void Write(DocumentStyle value, SnapshotWriter node)
    {
        node.Put("family", value.Family).Put("name", value.Name).Put("parent", value.ParentName).Put("display", value.DisplayName).Put("hidden", value.Hidden);
        switch (value)
        {
            case ParagraphStyle paragraph:
                node.Put("paragraph", paragraph.Paragraph).Put("character", paragraph.Character).Put("next", paragraph.NextStyleName)
                    .Put("outlineLevel", paragraph.OutlineLevel).Put("listStyle", paragraph.ListStyleName).Put("listLevel", paragraph.ListLevel).Put("category", paragraph.Category);
                break;
            case CharacterStyle character:
                node.Put("character", character.Character);
                break;
            case ListStyle list:
                node.Put("levels", list.Levels);
                break;
            case PageStyle page:
                node.Put("size", page.Size).Put("orientation", page.Orientation).Put("margins", page.Margins).Put("columns", page.Columns)
                    .Put("usage", page.Usage).Put("next", page.NextStyleName).Put("background", page.Background)
                    .Put("header", page.Header).Put("footer", page.Footer).Put("firstPageDifferent", page.FirstPageDifferent)
                    .Put("evenPagesDifferent", page.EvenPagesDifferent).Put("numberFormat", page.NumberFormat)
                    .Put("borders", page.Borders).Put("watermark", page.Watermark);
                break;
            case FrameStyle frame:
                node.Put("frame", frame.Frame);
                break;
            case TableStyle table:
                node.Put("table", table.Table);
                break;
        }
    }

    private PageStyle Page(string name, SnapshotReader node)
    {
        var page = new PageStyle(name)
        {
            Orientation = node.Get("orientation", PageOrientation.Portrait),
            Columns = node.Get("columns", SectionColumns.Single),
            Usage = node.Get("usage", PageUsage.All),
            NextStyleName = node.Get<string>("next"),
            Background = node.Get<uint?>("background"),
            FirstPageDifferent = node.Get("firstPageDifferent", false),
            EvenPagesDifferent = node.Get("evenPagesDifferent", false),
            NumberFormat = node.Get("numberFormat", NumberFormat.Arabic),
            Borders = node.Get<PulseOffice.Text.Rich.BoxBorders>("borders"),
            Watermark = node.Get<PageWatermark>("watermark"),
        };
        if (node.Get<DocumentSize?>("size") is { } size) page.Size = size;
        if (node.Get<Margins?>("margins") is { } margins) page.Margins = margins;
        if (node.Get<HeaderFooterContent>("header") is { } header) Fill(page.Header, header);
        if (node.Get<HeaderFooterContent>("footer") is { } footer) Fill(page.Footer, footer);
        return page;
    }

    private void Fill(HeaderFooterContent target, HeaderFooterContent source)
    {
        target.Enabled = source.Enabled;
        target.Default.Replace(source.Default.Blocks);
        target.First = source.First;
        target.Even = source.Even;
        target.Spacing = source.Spacing;
        target.MinimumHeight = source.MinimumHeight;
    }
}

public sealed class HeaderFooterConverter : SnapshotConverter<HeaderFooterContent>
{
    protected override HeaderFooterContent Read(SnapshotReader node) => new()
    {
        Enabled = node.Get("enabled", false),
        Default = node.Get("default", new BlockContainer()),
        First = node.Get<BlockContainer>("first"),
        Even = node.Get<BlockContainer>("even"),
        Spacing = node.Get("spacing", Twip.Zero),
        MinimumHeight = node.Get("minimumHeight", Twip.Zero),
    };

    protected override void Write(HeaderFooterContent value, SnapshotWriter node) =>
        node.Put("enabled", value.Enabled).Put("default", value.Default).Put("first", value.First).Put("even", value.Even)
            .Put("spacing", value.Spacing).Put("minimumHeight", value.MinimumHeight);
}

public sealed class IndexLevelStylesConverter : SnapshotConverter<PulseOffice.Documents.Indexes.IndexLevelStyles>
{
    protected override PulseOffice.Documents.Indexes.IndexLevelStyles Read(SnapshotReader node) =>
        node.Get("entries", new Dictionary<int, string>())
            .Aggregate(PulseOffice.Documents.Indexes.IndexLevelStyles.Defaults.WithHeading(node.Get<string>("heading")), (styles, pair) => styles.WithEntry(pair.Key, pair.Value));

    protected override void Write(PulseOffice.Documents.Indexes.IndexLevelStyles value, SnapshotWriter node) =>
        node.Put("heading", value.Heading).Put("entries", value.Entries.ToDictionary(pair => pair.Key, pair => pair.Value));
}
