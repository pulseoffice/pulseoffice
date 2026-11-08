using System.Globalization;
using System.Text;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests.Odt;

internal sealed class TextDocumentDescriber
{
    private readonly DirectFormatting _direct = new();

    public string Describe(TextDocument document)
    {
        var text = new StringBuilder();
        text.AppendLine($"initial={document.InitialPageStyle}");
        text.AppendLine($"language={document.Language}");
        text.AppendLine($"title={document.Properties.Title} author={document.Properties.Author} keywords={string.Join(",", document.Properties.Keywords)}");
        foreach (var (key, value) in document.Variables.OrderBy(v => v.Key)) text.AppendLine($"var {key}={value}");
        text.AppendLine($"forms={document.FormSettings}");
        text.AppendLine($"changes recording={document.Changes.IsRecording} show={document.Changes.ShowChanges} key={Convert.ToBase64String(document.Changes.ProtectionKey ?? [])}");
        foreach (var change in document.Changes.All)
        {
            text.AppendLine($"change {change.Id} {change.Kind} {change.Author} {change.Date:O} {change.Comment}");
            if (change is Deletion deletion) Blocks(text, deletion.Removed, "  ");
        }
        Styles(text, document.Styles);
        text.AppendLine($"footnotes={document.Notes.FootnoteSettings}");
        text.AppendLine($"endnotes={document.Notes.EndnoteSettings}");
        text.AppendLine("body:");
        Blocks(text, document.Body, "  ");
        foreach (var note in document.Notes.All)
        {
            text.AppendLine($"note {note.Id} {note.Kind} {note.Citation}");
            Blocks(text, note.Content, "  ");
        }
        foreach (var comment in document.Comments.All)
        {
            text.AppendLine($"comment {comment.Id} {comment.Author} {comment.Initials} {comment.Date:O} parent={comment.ParentId} resolved={comment.Resolved}");
            Blocks(text, comment.Content, "  ");
        }
        foreach (var item in document.Objects.All.OrderBy(item => item.Id, StringComparer.Ordinal)) Object(text, item);
        foreach (var macro in document.Macros.All) text.AppendLine($"macro {macro.Name} {macro.Language} {macro.Source}");
        return text.ToString();
    }

    private void Object(StringBuilder text, AnchoredObject item)
    {
        text.AppendLine($"object {item.Id} {item.Anchor} {item.Size} {item.Placement} wrap={item.Wrap} {item.WrapSpacing} style={item.StyleName} name={item.Name} z={item.ZOrder} rot={item.RotationDegrees} bg={item.InBackground}");
        switch (item.Content)
        {
            case TextBoxContent box:
                text.AppendLine($"  box grow={box.AutoGrowHeight} next={box.NextFrameName}");
                Blocks(text, box.Content, "    ");
                break;
            case EmbeddedContent { Value: RasterImageObject image }:
                text.AppendLine($"  image {image.Id} {image.MediaType} {image.PixelWidth}x{image.PixelHeight} {Convert.ToBase64String(image.Data.Span)} {image.Name} {image.Title} {image.Description} {image.NaturalSize} filter={image.Filter} crop={image.Crop.Left:0.###},{image.Crop.Top:0.###},{image.Crop.Right:0.###},{image.Crop.Bottom:0.###} map={string.Join(";", image.Map.Areas.Select(a => $"{a.Shape}:{string.Join(",", a.Points.Select(p => $"{p.X:0.###}/{p.Y:0.###}"))}:{a.Target}:{a.Description}"))}");
                break;
            case EmbeddedContent { Value: PackagedObject packaged }:
                text.AppendLine($"  packaged {packaged.Id} {packaged.FileName} {packaged.MediaType} {packaged.Packaged} {Convert.ToBase64String(packaged.Data.Span)}");
                break;
        }
    }

    private void Styles(StringBuilder text, StyleSheet sheet)
    {
        text.AppendLine($"default char={sheet.DefaultCharacter}");
        text.AppendLine($"default para={Paragraph(sheet.DefaultParagraph)}");
        foreach (var style in sheet.Paragraph.All.OrderBy(s => s.Name, StringComparer.Ordinal))
            text.AppendLine($"pstyle {Common(style)} next={style.NextStyleName} outline={style.OutlineLevel} list={style.ListStyleName} cat={style.Category} {Paragraph(style.Paragraph)} {style.Character}");
        foreach (var style in sheet.Character.All.OrderBy(s => s.Name, StringComparer.Ordinal))
            text.AppendLine($"cstyle {Common(style)} {style.Character}");
        foreach (var style in sheet.List.All.OrderBy(s => s.Name, StringComparer.Ordinal))
            text.AppendLine($"lstyle {Common(style)} {string.Join("|", style.Levels)}");
        foreach (var style in sheet.Frame.All.OrderBy(s => s.Name, StringComparer.Ordinal))
            text.AppendLine($"fstyle {Common(style)} {style.Frame}");
        foreach (var style in sheet.Table.All.OrderBy(s => s.Name, StringComparer.Ordinal))
            text.AppendLine($"tstyle {Common(style)} {style.Table}");
        foreach (var style in sheet.Page.All.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            text.AppendLine($"page {Common(style)} {style.Size} {style.Orientation} {style.Margins} {style.Columns} {style.Usage} next={style.NextStyleName} bg={style.Background} first={style.FirstPageDifferent} even={style.EvenPagesDifferent} num={style.NumberFormat}");
            Region(text, "header", style.Header);
            Region(text, "footer", style.Footer);
        }
    }

    private void Region(StringBuilder text, string kind, HeaderFooterContent region)
    {
        text.AppendLine($"  {kind} enabled={region.Enabled} spacing={region.Spacing} min={region.MinimumHeight} first={region.First is not null} even={region.Even is not null}");
        Blocks(text, region.Default, "    ");
        if (region.First is { } first) Blocks(text, first, "    first ");
        if (region.Even is { } even) Blocks(text, even, "    even ");
    }

    private string Common(DocumentStyle style) => $"{style.Name} parent={style.ParentName} display={style.DisplayName} hidden={style.Hidden}";

    private string Paragraph(ParagraphProperties properties) =>
        properties with { TabStops = null } + " tabs=" + (properties.TabStops is null ? "null" : string.Join(";", properties.TabStops));

    private void Blocks(StringBuilder text, BlockContainer container, string indent)
    {
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    var content = paragraph.Content;
                    text.AppendLine($"{indent}P {paragraph.Id} style={content.StyleName} outline={content.OutlineLevel} list={content.ListStyleName}/{content.BulletLevel}/{content.Bullet}/{content.Numbered} {Paragraph(_direct.Paragraph(content))} [{content.Text}]");
                    foreach (var run in content.Runs)
                        text.AppendLine($"{indent}  R [{run.Text}] field={run.Field}:{run.FieldArgument} style={run.StyleName} link={run.Hyperlink} {_direct.Run(run)}");
                    foreach (var mark in paragraph.Marks) text.AppendLine($"{indent}  M {mark}");
                    break;
                case TableBlock table:
                    text.AppendLine($"{indent}T {table.Id} name={table.Name} style={table.StyleName} header={table.HeaderRows} repeat={table.RepeatHeaderRows} cols={string.Join(",", table.Columns)}");
                    foreach (var row in table.Rows)
                    {
                        text.AppendLine($"{indent}  row h={row.Height} min={row.MinimumHeight} break={row.AllowBreakAcrossPages}");
                        foreach (var cell in row.Cells)
                        {
                            text.AppendLine($"{indent}    cell span={cell.ColumnSpan}x{cell.RowSpan} covered={cell.Covered} style={cell.StyleName} bg={cell.Background} v={cell.VerticalAlign}");
                            Blocks(text, cell.Content, indent + "      ");
                        }
                    }
                    break;
                case SectionBlock section:
                    text.AppendLine($"{indent}S {section.Id} {section.Name} {section.Columns} prot={section.Protected} hidden={section.Hidden} cond={section.Condition} link={section.LinkedFile}");
                    Blocks(text, section.Content, indent + "  ");
                    break;
            }
        }
    }
}
